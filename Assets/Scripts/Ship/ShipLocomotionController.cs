using Plunderlust.Core.Data;
using Plunderlust.Core.Interfaces;
using Plunderlust.Wind;
using UnityEngine;

namespace Plunderlust.Ship
{
    /// <summary>
    /// Naval locomotion controller driving 3D physics-based sailing, rudder dynamics,
    /// and sail state machine transitions with localized subsystem and manpower penalties.
    /// Operates with zero heap allocations in Update and FixedUpdate.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(0)]
    public class ShipLocomotionController : MonoBehaviour
    {
        [Header("Hull Profile")]
        [SerializeField] private ShipHullDataSO _hullData;

        [Header("Active Sail State")]
        [SerializeField] private SailState _currentSailState = SailState.Anchored;

        [Header("Propulsion Limits (Overridden by HullData if set)")]
        [SerializeField] private float _fullCanvasMaxSpeed = 13.5f;
        [SerializeField] private float _battleSailsMaxSpeed = 7.0f;
        [SerializeField] private float _rowingSpeed = 4.0f;
        [SerializeField] private float _forwardAcceleration = 4.5f;
        [SerializeField] private float _reverseSpeed = 2.5f;
        [SerializeField] private float _reverseAcceleration = 3.5f;

        [Header("Steering & Rudder")]
        [SerializeField] private float _rudderTorque = 8.0f;
        [SerializeField] private float _rudderBattleSailsMultiplier = 1.35f;
        [SerializeField] private float _turnBankingFactor = 0.35f;
        [Tooltip("Strength of keel lateral resistance opposing sideways drift.")]
        [SerializeField] private float _keelAntiSlipFactor = 5.0f;

        [Header("Rudder Damage Failure Noise")]
        [Tooltip("Max angular torque injected when rudder health is below 25%.")]
        [SerializeField] private float _criticalRudderWobbleTorque = 12f;
        [SerializeField] private float _criticalRudderWobbleSpeed = 1.8f;

        [Header("Debug Controls / Live Telemetry")]
        [Range(-1f, 1f)]
        [SerializeField] private float _steerInput = 0f;
        [Range(-1f, 1f)]
        [SerializeField] private float _throttleInput = 0f;
        [SerializeField] private float _currentSpeedKnots = 0f;
        [SerializeField] private float _effectiveThrust = 0f;
        [SerializeField] private float _effectiveRudderTorque = 0f;
        [SerializeField] private float _windEfficiency = 1f;

        private Rigidbody _rb;
        private IShipSubsystemProvider _subsystemProvider;
        private ICrewEfficiencyProvider _crewProvider;
        private Plunderlust.Ocean.BuoyancyBody _buoyancy;

        private ICrewEfficiencyProvider CrewEfficiencyProvider
        {
            get
            {
                if (_crewProvider == null)
                {
                    _crewProvider = GetComponent<ICrewEfficiencyProvider>() ?? GetComponentInParent<ICrewEfficiencyProvider>();
                }
                return _crewProvider;
            }
        }

        // Pre-allocated cached math vectors
        private Vector3 _forwardThrustForce;
        private Vector3 _lateralKeelForce;
        private Vector3 _yawTorqueVector;
        private Vector3 _heelingTorqueVector;

        public SailState CurrentSailState => _currentSailState;
        public ShipHullDataSO HullData => _hullData;
        public float CurrentSpeedKnots => _currentSpeedKnots;
        public float WindEfficiency => _windEfficiency;
        public float SteerInput
        {
            get => _steerInput;
            set => _steerInput = Mathf.Clamp(value, -1f, 1f);
        }
        public float ThrottleInput
        {
            get => _throttleInput;
            set => _throttleInput = Mathf.Clamp(value, -1f, 1f);
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _subsystemProvider = GetComponent<IShipSubsystemProvider>() ?? GetComponentInParent<IShipSubsystemProvider>();
            _crewProvider = GetComponent<ICrewEfficiencyProvider>() ?? GetComponentInParent<ICrewEfficiencyProvider>();

            ApplyHullData();
        }

        public void SetHullData(ShipHullDataSO hullData)
        {
            _hullData = hullData;
            ApplyHullData();
        }

        private void ApplyHullData()
        {
            if (_hullData == null) return;

            _fullCanvasMaxSpeed = _hullData.FullCanvasMaxSpeed;
            _battleSailsMaxSpeed = _hullData.BattleSailsMaxSpeed;
            _rowingSpeed = _hullData.RowingSpeed;
            _forwardAcceleration = _hullData.ForwardAcceleration;
            _reverseSpeed = _hullData.ReverseSpeed;
            _rudderTorque = _hullData.RudderTorque;
            _rudderBattleSailsMultiplier = _hullData.RudderResponsivenessBattleSails;
            _turnBankingFactor = _hullData.TurnBankingFactor;

            // Set default sail state based on ship type
            if (_hullData.SupportsRowing && !_hullData.SupportsSails)
            {
                _currentSailState = SailState.Rowing;
            }
        }

        /// <summary>
        /// Transitions the sail state machine. Enforces subsystem lockouts (e.g. crippled sails lock out Full Canvas).
        /// </summary>
        public bool SetSailState(SailState newState)
        {
            if (_subsystemProvider != null && _subsystemProvider.IsSailsCrippled && newState == SailState.FullCanvas)
            {
                // Cannot raise full canvas when rigging is critically broken
                return false;
            }

            _currentSailState = newState;
            return true;
        }

        /// <summary>
        /// Convenience method to cycle sail states up (Anchored -> Battle Sails -> Full Canvas).
        /// </summary>
        public void RaiseSails()
        {
            switch (_currentSailState)
            {
                case SailState.Anchored:
                    SetSailState(_hullData != null && _hullData.SupportsRowing && !_hullData.SupportsSails ? SailState.Rowing : SailState.BattleSails);
                    break;
                case SailState.Rowing:
                    if (_hullData != null && _hullData.SupportsSails) SetSailState(SailState.BattleSails);
                    break;
                case SailState.BattleSails:
                    SetSailState(SailState.FullCanvas);
                    break;
            }
        }

        /// <summary>
        /// Convenience method to cycle sail states down (Full Canvas -> Battle Sails -> Anchored).
        /// </summary>
        public void LowerSails()
        {
            switch (_currentSailState)
            {
                case SailState.FullCanvas:
                    SetSailState(SailState.BattleSails);
                    break;
                case SailState.BattleSails:
                    SetSailState(SailState.Anchored);
                    break;
                case SailState.Rowing:
                    SetSailState(SailState.Anchored);
                    break;
            }
        }

        private void Update()
        {
            // Calculate speed in knots (1 knot approx 0.514 m/s)
            _currentSpeedKnots = Vector3.Dot(_rb.linearVelocity, transform.forward) * 1.94384f;

            // Safety check: if sails become crippled while under Full Canvas, drop down to Battle Sails
            if (_currentSailState == SailState.FullCanvas && _subsystemProvider != null && _subsystemProvider.IsSailsCrippled)
            {
                _currentSailState = SailState.BattleSails;
            }
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            // 1. Evaluate Subsystem & Crew Efficiency Multipliers
            float sailsSubsystemEfficiency = _subsystemProvider != null ? _subsystemProvider.SailsEfficiency : 1.0f;
            float sailCrewEfficiency = CrewEfficiencyProvider != null ? CrewEfficiencyProvider.SailHandlingEfficiency : 1.0f;

            float rudderSubsystemEfficiency = _subsystemProvider != null ? _subsystemProvider.RudderEfficiency : 1.0f;
            float rudderCrewEfficiency = CrewEfficiencyProvider != null ? CrewEfficiencyProvider.RudderHandlingEfficiency : 1.0f;

            // 2. Compute Wind Polar Efficiency & Dynamic Wind Speed Multiplier
            if (WindManager.Instance != null)
            {
                float polar = WindManager.Instance.GetWindPolarEfficiency(transform.forward);
                // Wind strength scalar: 0.75x in calm breeze up to 1.35x in high gales
                float windSpeedMultiplier = Mathf.Lerp(0.75f, 1.35f, WindManager.Instance.WindStrength);

                if (_currentSailState == SailState.FullCanvas)
                {
                    // Full canvas catches wind polar with a guaranteed minimum floor of 0.40x so speed never stalls to 0
                    _windEfficiency = Mathf.Max(0.40f, polar * windSpeedMultiplier);
                }
                else if (_currentSailState == SailState.BattleSails)
                {
                    // Battle sails offer stable cruising with guaranteed 0.45x baseline
                    _windEfficiency = Mathf.Max(0.45f, (0.45f + (0.55f * polar)) * windSpeedMultiplier);
                }
                else if (_currentSailState == SailState.Rowing)
                {
                    // Rowboat oar propulsion: manual human muscle always drives forward, slightly assisted or hindered by wind
                    float dot = Vector3.Dot(transform.forward, WindManager.Instance.WindDirection);
                    _windEfficiency = Mathf.Clamp(1.0f + (dot * 0.25f * WindManager.Instance.WindStrength), 0.75f, 1.25f);
                }
                else
                {
                    _windEfficiency = 1.0f;
                }
            }
            else
            {
                _windEfficiency = 1.0f;
            }

            // 3. Evaluate Target Speed & Thrust Force
            float targetSpeed = 0f;
            float stateThrustScale = 0f;

            switch (_currentSailState)
            {
                case SailState.Anchored:
                    // When anchored or backing up, reverse throttle drives reverse motion (backing sails or poles)
                    if (_throttleInput <= -0.05f)
                    {
                        targetSpeed = -_reverseSpeed * Mathf.Abs(_throttleInput) * sailCrewEfficiency;
                        stateThrustScale = Mathf.Abs(_throttleInput) * sailCrewEfficiency;
                    }
                    else
                    {
                        targetSpeed = 0f;
                        stateThrustScale = 0f;
                    }
                    break;

                case SailState.Rowing:
                    // Dynamic Manual Rowing:
                    // When W is pressed, throttle is positive (forward oars)
                    // When S is pressed, throttle is negative (reverse oars)
                    // If no throttle is pressed but steering is active, single oar imparts gentle forward motion
                    float effectiveDemand = _throttleInput;
                    if (Mathf.Abs(effectiveDemand) < 0.05f && Mathf.Abs(_steerInput) > 0.05f)
                    {
                        effectiveDemand = Mathf.Abs(_steerInput) * 0.4f;
                    }

                    if (effectiveDemand >= 0.05f)
                    {
                        targetSpeed = _rowingSpeed * effectiveDemand * _windEfficiency * sailCrewEfficiency;
                        stateThrustScale = effectiveDemand * sailCrewEfficiency;
                    }
                    else if (effectiveDemand <= -0.05f)
                    {
                        targetSpeed = -_reverseSpeed * Mathf.Abs(effectiveDemand) * sailCrewEfficiency;
                        stateThrustScale = Mathf.Abs(effectiveDemand) * sailCrewEfficiency;
                    }
                    else
                    {
                        targetSpeed = 0f;
                        stateThrustScale = 0f;
                    }
                    break;

                case SailState.BattleSails:
                    if (_throttleInput <= -0.05f)
                    {
                        // Backing down under battle sails (backing the yards)
                        targetSpeed = -_reverseSpeed * Mathf.Abs(_throttleInput) * sailCrewEfficiency;
                        stateThrustScale = Mathf.Abs(_throttleInput) * sailCrewEfficiency;
                    }
                    else
                    {
                        float fwdScale = Mathf.Clamp01(_throttleInput > 0.01f ? _throttleInput : 0.75f);
                        targetSpeed = _battleSailsMaxSpeed * _windEfficiency * sailsSubsystemEfficiency * sailCrewEfficiency * fwdScale;
                        stateThrustScale = Mathf.Max(0.35f, _windEfficiency * 0.75f) * sailsSubsystemEfficiency * sailCrewEfficiency * fwdScale;
                    }
                    break;

                case SailState.FullCanvas:
                    if (_throttleInput <= -0.05f)
                    {
                        // Backing yards even under full canvas
                        targetSpeed = -_reverseSpeed * Mathf.Abs(_throttleInput) * sailCrewEfficiency;
                        stateThrustScale = Mathf.Abs(_throttleInput) * sailCrewEfficiency;
                    }
                    else
                    {
                        float fwdScale = Mathf.Clamp01(_throttleInput > 0.01f ? _throttleInput : 1.0f);
                        targetSpeed = _fullCanvasMaxSpeed * _windEfficiency * sailsSubsystemEfficiency * sailCrewEfficiency * fwdScale;
                        // Forward thrust scale directly follows wind efficiency (guaranteed at least 0.35 when in irons for steady mobility)
                        stateThrustScale = Mathf.Max(0.35f, _windEfficiency) * sailsSubsystemEfficiency * sailCrewEfficiency * fwdScale;
                    }
                    break;
            }

            // Project forward vector strictly onto horizontal sea plane (ZERO vertical thrust)
            Vector3 horizontalForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (horizontalForward.sqrMagnitude > 0.001f)
            {
                horizontalForward.Normalize();
            }
            else
            {
                horizontalForward = transform.forward;
                horizontalForward.y = 0f;
                horizontalForward.Normalize();
            }

            float currentForwardSpeed = Vector3.Dot(_rb.linearVelocity, horizontalForward);

            // Water contact verification: if airborne, sails and oars produce 0 thrust
            if (_buoyancy == null) _buoyancy = GetComponent<Plunderlust.Ocean.BuoyancyBody>() ?? GetComponentInParent<Plunderlust.Ocean.BuoyancyBody>();
            bool isInWater = _buoyancy == null || _buoyancy.SubmergedProbeCount > 0;

            if (isInWater && stateThrustScale > 0.01f)
            {
                if (targetSpeed > 0.05f)
                {
                    // Forward thrust demand
                    float forwardDeficit = targetSpeed - currentForwardSpeed;
                    if (forwardDeficit > 0.05f)
                    {
                        float thrustFactor = Mathf.Clamp01(forwardDeficit / 1.5f);
                        _effectiveThrust = thrustFactor * _forwardAcceleration * stateThrustScale * _rb.mass;
                        _forwardThrustForce = horizontalForward * _effectiveThrust;
                        _forwardThrustForce.y = 0f; // STRICTLY ZERO vertical thrust
                        _rb.AddForce(_forwardThrustForce, ForceMode.Force);
                    }
                    else
                    {
                        _effectiveThrust = 0f;
                        _forwardThrustForce = Vector3.zero;
                    }
                }
                else if (targetSpeed < -0.05f)
                {
                    // Reverse thrust demand: targetSpeed is negative
                    float reverseDeficit = currentForwardSpeed - targetSpeed;
                    if (reverseDeficit > 0.05f)
                    {
                        float thrustFactor = Mathf.Clamp01(reverseDeficit / 1.5f);
                        _effectiveThrust = thrustFactor * _reverseAcceleration * stateThrustScale * _rb.mass;
                        _forwardThrustForce = -horizontalForward * _effectiveThrust;
                        _forwardThrustForce.y = 0f;
                        _rb.AddForce(_forwardThrustForce, ForceMode.Force);
                    }
                    else
                    {
                        _effectiveThrust = 0f;
                        _forwardThrustForce = Vector3.zero;
                    }
                }
                else
                {
                    _effectiveThrust = 0f;
                    _forwardThrustForce = Vector3.zero;
                }
            }
            else
            {
                _effectiveThrust = 0f;
                _forwardThrustForce = Vector3.zero;
                // Natural water drag when coasting under oars or at anchor
                if (Mathf.Abs(currentForwardSpeed) > 0.05f && isInWater)
                {
                    _rb.AddForce(-horizontalForward * (currentForwardSpeed * _rb.mass * 2.2f), ForceMode.Force);
                }
            }

            // 4. Keel Anti-Slip Resistance & Aerodynamic Leeway
            Vector3 horizontalRight = Vector3.ProjectOnPlane(transform.right, Vector3.up);
            if (horizontalRight.sqrMagnitude > 0.001f) horizontalRight.Normalize();
            else horizontalRight = transform.right;

            // Natural aerodynamic leeway drift on hull & rigging (pushes laterally across the keel)
            if (isInWater && WindManager.Instance != null && _rb != null)
            {
                float canvasFactor = (_currentSailState == SailState.FullCanvas) ? 1.0f : (_currentSailState == SailState.BattleSails) ? 0.65f : 0.25f;
                
                // Lateral leeway: wind pushes the vessel sideways along its beam (horizontalRight), NOT backwards into engine thrust
                Vector3 lateralWind = Vector3.Project(WindManager.Instance.WindDirection, horizontalRight);
                Vector3 windDrift = lateralWind * (WindManager.Instance.WindStrength * canvasFactor * 0.45f * _rb.mass);

                // When completely idling / anchored with 0 throttle, allow gentle downwind drift
                if (Mathf.Abs(_throttleInput) < 0.05f)
                {
                    Vector3 idleDrift = WindManager.Instance.WindDirection * (WindManager.Instance.WindStrength * 0.20f * _rb.mass);
                    windDrift += idleDrift;
                }

                _rb.AddForce(windDrift, ForceMode.Force);
            }

            float lateralSlipSpeed = Vector3.Dot(_rb.linearVelocity, horizontalRight);
            _lateralKeelForce = -horizontalRight * (lateralSlipSpeed * _rb.mass * _keelAntiSlipFactor);
            _lateralKeelForce.y = 0f;
            _rb.AddForce(_lateralKeelForce, ForceMode.Force);

            // 5. Rudder Steering & Turning Dynamics
            float rudderStateMultiplier = (_currentSailState == SailState.BattleSails) ? _rudderBattleSailsMultiplier : 1.0f;
            _effectiveRudderTorque = _rudderTorque * rudderStateMultiplier * rudderSubsystemEfficiency * rudderCrewEfficiency;

            // Speed-based rudder authority: ship turns better when water flows over the rudder, with guaranteed 45% base authority
            float rudderFlowFactor = Mathf.Clamp(Mathf.Abs(currentForwardSpeed) / 2.5f + 0.45f, 0.45f, 1.0f);
            float finalYawTorque = _steerInput * _effectiveRudderTorque * rudderFlowFactor * _rb.mass;

            // Inject periodic angular noise if rudder is critically broken (< 25% health)
            if (_subsystemProvider != null && _subsystemProvider.IsRudderCritical)
            {
                float wobbleNoise = Mathf.Sin(Time.time * _criticalRudderWobbleSpeed) * _criticalRudderWobbleTorque * _rb.mass;
                finalYawTorque += wobbleNoise;
            }

            _yawTorqueVector.x = 0f;
            _yawTorqueVector.y = finalYawTorque;
            _yawTorqueVector.z = 0f;
            _rb.AddTorque(_yawTorqueVector, ForceMode.Force);

            // 6. Realistic Turn Banking / Heeling Roll
            float heelingRoll = -_steerInput * _turnBankingFactor * currentForwardSpeed * _rb.mass;
            _heelingTorqueVector = horizontalForward * heelingRoll;
            _rb.AddTorque(_heelingTorqueVector, ForceMode.Force);
        }

        private void OnCollisionStay(Collision collision)
        {
            // If colliding with static shore landmarks (island rocks, docks, watchtower)
            if (collision.rigidbody == null)
            {
                // Prevent vertical ramping up rock slopes
                if (_rb.linearVelocity.y > 0f)
                {
                    Vector3 vel = _rb.linearVelocity;
                    vel.y = 0f;
                    _rb.linearVelocity = vel;
                }

                // Repel horizontally away from obstacle surfaces
                foreach (ContactPoint contact in collision.contacts)
                {
                    Vector3 flatNormal = Vector3.ProjectOnPlane(contact.normal, Vector3.up);
                    if (flatNormal.sqrMagnitude > 0.05f)
                    {
                        flatNormal.Normalize();

                        // Cancel any velocity heading directly into the obstacle
                        float intoObstacleSpeed = Vector3.Dot(_rb.linearVelocity, -flatNormal);
                        if (intoObstacleSpeed > 0f)
                        {
                            _rb.linearVelocity -= (-flatNormal) * intoObstacleSpeed;
                        }

                        // Apply robust horizontal pushback away from barrier
                        _rb.AddForce(flatNormal * (_rb.mass * 15.0f), ForceMode.Force);
                    }
                }
            }
        }
    }
}
