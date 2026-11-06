using Plunderlust.Core.Data;
using Plunderlust.Core.Interfaces;
using UnityEngine;

namespace Plunderlust.Ocean
{
    /// <summary>
    /// Multi-probe hydrodynamics and Archimedes buoyancy physics controller.
    /// Simulates buoyant lift, dynamic bilge water flooding mass, listing torque,
    /// and velocity-opposing water drag with zero GC allocations in FixedUpdate.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(-100)]
    public class BuoyancyBody : MonoBehaviour
    {
        [Header("Hull Data Profile (Optional)")]
        [Tooltip("If assigned, overrides mass and buoyancy settings from the ScriptableObject profile.")]
        [SerializeField] private ShipHullDataSO _hullData;

        [Header("Buoyancy Probes")]
        [Tooltip("Array of probe transforms (e.g. Bow, Stern, Port, Starboard, Mid-Bow, Mid-Stern).")]
        [SerializeField] private Transform[] _probes;

        [Header("Hydrodynamic Parameters")]
        [Tooltip("Nominal depth below surface at which a probe produces 100% buoyant lift.")]
        [SerializeField] private float _submersionDepth = 1.2f;
        [Tooltip("Upward Archimedes force in Newtons per probe when fully submerged.")]
        [SerializeField] private float _buoyantForcePerProbe = 8500f;
        [Tooltip("Water linear drag opposing probe movement through the water.")]
        [SerializeField] private float _probeWaterDrag = 350f;
        [Tooltip("Rotational damping applied when probes are submerged.")]
        [SerializeField] private float _angularWaterDamping = 2.5f;
        [Tooltip("Vertical trim offset in meters. Positive values elevate the hull to prevent deck water clipping.")]
        [SerializeField] private float _waterlineOffset = 0.0f;
        [Tooltip("Multiplier for emergency reserve buoyancy when wave crests rise above probe submersion depth.")]
        [SerializeField] private float _reserveBuoyancyScale = 2.5f;
        [Tooltip("Torque aligning ship up-vector with wave normal. Keeps small craft cresting over waves like a cork.")]
        [SerializeField] private float _surfaceAlignmentTorque = 6.0f;

        [Header("Mass & Bilge Flooding")]
        [SerializeField] private float _baseMass = 2400f;
        [Tooltip("Additional mass added at 100% bilge flooding ratio.")]
        [SerializeField] private float _maxFloodingMass = 3600f;
        [Range(0f, 1f)]
        [Tooltip("Remaining buoyancy factor when ship is 100% flooded.")]
        [SerializeField] private float _sinkingBuoyancyRetention = 0.25f;
        [Tooltip("Center of mass offset relative to transform origin.")]
        [SerializeField] private Vector3 _centerOfMassOffset = new Vector3(0f, -0.6f, 0f);

        [Header("Real-time State (Read-Only)")]
        [Range(0f, 1f)]
        [SerializeField] private float _floodingRatio = 0f;
        [SerializeField] private int _submergedProbeCount = 0;
        [SerializeField] private bool _isSunk = false;

        private Rigidbody _rb;
        private IShipSubsystemProvider _subsystemProvider;
        private Vector3[] _fallbackProbeLocalOffsets;
        private bool _usingFallbackProbes = false;

        // Pre-allocated cache vectors to eliminate heap allocation
        private Vector3 _cachedProbePos;
        private Vector3 _cachedProbeVelocity;
        private Vector3 _cachedTotalForce;

        /// <summary>
        /// Current flooding ratio [0.0 .. 1.0]. Driven by Hull breaches or set externally.
        /// </summary>
        public float FloodingRatio
        {
            get => _floodingRatio;
            set => _floodingRatio = Mathf.Clamp01(value);
        }

        public bool IsSunk => _isSunk;
        public int SubmergedProbeCount => _submergedProbeCount;
        public Rigidbody Rigidbody => _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _subsystemProvider = GetComponent<IShipSubsystemProvider>() ?? GetComponentInParent<IShipSubsystemProvider>();

            ApplyHullDataSettings();

            _rb.centerOfMass = _centerOfMassOffset;

            // Generate fallback probe offsets if none were assigned in the inspector
            if (_probes == null || _probes.Length == 0)
            {
                _usingFallbackProbes = true;
                _fallbackProbeLocalOffsets = new Vector3[]
                {
                    new Vector3(0f, 0f, 2.5f),   // Bow
                    new Vector3(0f, 0f, -2.5f),  // Stern
                    new Vector3(-1.2f, 0f, 0f),  // Port
                    new Vector3(1.2f, 0f, 0f)    // Starboard
                };
            }
        }

        public void SetHullData(ShipHullDataSO hullData)
        {
            _hullData = hullData;
            ApplyHullDataSettings();
            if (_rb != null)
            {
                _rb.centerOfMass = _centerOfMassOffset;
            }
        }

        private void ApplyHullDataSettings()
        {
            if (_hullData == null) return;

            _baseMass = _hullData.BaseMass;
            _maxFloodingMass = _hullData.MaxFloodingMass;
            _submersionDepth = _hullData.ProbeSubmersionDepth;
            _buoyantForcePerProbe = _hullData.BuoyantForcePerProbe;
            _sinkingBuoyancyRetention = _hullData.SinkingBuoyancyRetention;
            _waterlineOffset = _hullData.WaterlineOffset;
            _reserveBuoyancyScale = _hullData.ReserveBuoyancyScale;
            _surfaceAlignmentTorque = _hullData.SurfaceAlignmentTorque;
            _centerOfMassOffset = _hullData.CenterOfMassOffset;
            _rb.linearDamping = _hullData.LinearDrag;
            _rb.angularDamping = _hullData.AngularDrag;
        }

        private void FixedUpdate()
        {
            float currentTime = Time.time;

            bool isSinking = (_subsystemProvider != null && _subsystemProvider.IsSinking) || _isSunk;

            // Sync flooding ratio if a subsystem provider is attached
            if (_subsystemProvider != null)
            {
                _floodingRatio = isSinking ? 1.0f : _subsystemProvider.FloodingRatio;
            }

            // Dynamic mass scaling: leaking ships absorb water and become heavier
            float targetMass = _baseMass + (_maxFloodingMass * _floodingRatio);
            _rb.mass = targetMass;

            // Buoyancy retention scales down as hull floods, reaching 0 on sinking
            float buoyancyScalar = isSinking ? 0.0f : Mathf.Lerp(1.0f, _sinkingBuoyancyRetention, _floodingRatio);

            if (_floodingRatio >= 0.99f && !_isSunk)
            {
                _isSunk = true;
            }

            int probeCount = _usingFallbackProbes ? _fallbackProbeLocalOffsets.Length : _probes.Length;
            _submergedProbeCount = 0;

            for (int i = 0; i < probeCount; i++)
            {
                if (_usingFallbackProbes)
                {
                    _cachedProbePos = transform.TransformPoint(_fallbackProbeLocalOffsets[i]);
                }
                else
                {
                    Transform probeTransform = _probes[i];
                    if (probeTransform == null) continue;
                    _cachedProbePos = probeTransform.position;
                }

                float waveHeight = OceanWaveManager.GetWaveHeight(_cachedProbePos.x, _cachedProbePos.z, currentTime);
                float depth = (waveHeight + _waterlineOffset) - _cachedProbePos.y;

                if (depth > 0f)
                {
                    _submergedProbeCount++;

                    // Archimedes lift: proportional to submerged depth fraction (0 if sinking)
                    float submersionRatio = Mathf.Clamp01(depth / Mathf.Max(0.1f, _submersionDepth));
                    float upwardForce = isSinking ? 0.0f : (_buoyantForcePerProbe * submersionRatio * buoyancyScalar);

                    // Non-linear reserve buoyancy: disabled when sinking
                    if (!isSinking && depth > _submersionDepth && _reserveBuoyancyScale > 0.01f)
                    {
                        float excessDepthRatio = (depth - _submersionDepth) / Mathf.Max(0.1f, _submersionDepth);
                        upwardForce += _buoyantForcePerProbe * excessDepthRatio * _reserveBuoyancyScale * buoyancyScalar;
                    }

                    // Water drag: opposes linear velocity of the probe
                    _cachedProbeVelocity = _rb.GetPointVelocity(_cachedProbePos);
                    float dragFactor = _probeWaterDrag * submersionRatio;

                    _cachedTotalForce.x = -_cachedProbeVelocity.x * dragFactor;
                    _cachedTotalForce.y = upwardForce - (_cachedProbeVelocity.y * dragFactor * 0.5f);
                    _cachedTotalForce.z = -_cachedProbeVelocity.z * dragFactor;

                    _rb.AddForceAtPosition(_cachedTotalForce, _cachedProbePos, ForceMode.Force);
                }
            }

            // Apply global rotational damping, surface normal alignment, or sinking capsize forces
            if (_submergedProbeCount > 0)
            {
                float submergedFraction = (float)_submergedProbeCount / probeCount;

                // Angular damping against water
                _rb.AddTorque(-_rb.angularVelocity * (_angularWaterDamping * submergedFraction), ForceMode.Acceleration);

                if (isSinking)
                {
                    // SINKING DYNAMICS: Continuous downward gravitational plunge force
                    _rb.AddForce(Vector3.down * (_rb.mass * 7.5f), ForceMode.Force);

                    // Heavy listing roll and pitch torque as sea pours into the hull
                    Vector3 capsizeTorque = (transform.forward * 1.6f + transform.right * 0.7f) * (_rb.mass * 2.8f);
                    _rb.AddTorque(capsizeTorque, ForceMode.Force);
                }
                else
                {
                    // ponytail: smoothly align ship orientation with wave normal so it pitches and rolls with swells
                    Vector3 shipPos = _rb.position;
                    OceanWaveManager.GetWaveHeightAndNormal(shipPos.x, shipPos.z, currentTime, out float _, out Vector3 waveNormal);
                    Quaternion waveRot = Quaternion.FromToRotation(transform.up, waveNormal) * _rb.rotation;
                    _rb.rotation = Quaternion.Slerp(_rb.rotation, waveRot, Time.fixedDeltaTime * 6.0f);

                    // Flooding List & Trim: Flooded ships list sideways and dip stern
                    if (_floodingRatio > 0.15f)
                    {
                        // Asymmetrical bilge water slosh simulation
                        float listIntensity = _floodingRatio * 0.8f;
                        Vector3 listTorque = (transform.forward * (Mathf.Sin(currentTime * 0.5f) * 0.35f + 0.65f) * listIntensity * 1200f) +
                                             (-transform.right * listIntensity * 900f);

                        _rb.AddTorque(listTorque * submergedFraction, ForceMode.Force);
                    }
                }
            }

            // -------------------------------------------------------------
            // SEA SURFACE BOUNDARY ENFORCEMENT & ANTI-AIRBORNE STABILIZATION
            // -------------------------------------------------------------
            Vector3 centerPos = _rb.position;
            float localWaveHeight = OceanWaveManager.GetWaveHeight(centerPos.x, centerPos.z, currentTime);

            // ponytail: gentle draft submersion (bottom 5-15cm in water) leaving ample freeboard so waves never breach deck
            float draft = Mathf.Clamp(_submersionDepth * 0.15f, 0.04f, 0.20f);
            float targetWaterlineY = localWaveHeight - draft;
            float altitudeAboveSea = centerPos.y - targetWaterlineY;

            if (!isSinking)
            {
                // ponytail: floor clamp prevents ships from sinking below their natural draft
                if (centerPos.y < targetWaterlineY)
                {
                    Vector3 pos = _rb.position;
                    pos.y = Mathf.Lerp(pos.y, targetWaterlineY, Time.fixedDeltaTime * 14.0f);
                    if (pos.y < targetWaterlineY - 0.05f) pos.y = targetWaterlineY - 0.05f;
                    _rb.position = pos;

                    if (_rb.linearVelocity.y < 0f)
                    {
                        Vector3 vel = _rb.linearVelocity;
                        vel.y = Mathf.Max(0f, vel.y * 0.1f);
                        _rb.linearVelocity = vel;
                    }
                }

                // 1. If ship is elevated above the sea surface, apply progressive gravity pull-down
                if (altitudeAboveSea > 0.15f)
                {
                    float extraGravityMultiplier = Mathf.Clamp(altitudeAboveSea * 4.0f, 1.5f, 8.0f);
                    _rb.AddForce(Vector3.down * (_rb.mass * 9.81f * extraGravityMultiplier), ForceMode.Force);

                    // Strongly damp any upward vertical momentum so the ship cannot launch or fly
                    if (_rb.linearVelocity.y > 0f)
                    {
                        Vector3 vel = _rb.linearVelocity;
                        vel.y *= Mathf.Clamp01(1.0f - (Time.fixedDeltaTime * 12.0f));
                        _rb.linearVelocity = vel;
                    }
                }

                // 2. Hard Sea Ceiling Clamp: Under NO circumstance may a ship elevate more than 0.9m above sea level
                if (altitudeAboveSea > 0.9f)
                {
                    Vector3 clampedPos = _rb.position;
                    clampedPos.y = localWaveHeight + _waterlineOffset + 0.9f;
                    _rb.position = clampedPos;

                    Vector3 vel = _rb.linearVelocity;
                    vel.y = Mathf.Min(0f, vel.y);
                    _rb.linearVelocity = vel;
                }

                // 3. Pitch and Roll Righting Torque (Self-Righting Metacentric Stability)
                // Pitch: angle of forward vector with the horizontal plane
                Vector3 flatFwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (flatFwd.sqrMagnitude > 0.001f)
                {
                    float pitchAngle = Vector3.SignedAngle(flatFwd, transform.forward, transform.right);
                    float maxAllowedPitch = 28f; // ponytail: allow riding steep wave slopes
                    if (Mathf.Abs(pitchAngle) > maxAllowedPitch)
                    {
                        float pitchCorrection = Mathf.Sign(pitchAngle) * (Mathf.Abs(pitchAngle) - maxAllowedPitch);
                        Vector3 rightingTorque = -transform.right * (pitchCorrection * _rb.mass * 12.0f);
                        _rb.AddTorque(rightingTorque, ForceMode.Force);

                        // Damp pitch angular velocity
                        Vector3 localAngVel = transform.InverseTransformDirection(_rb.angularVelocity);
                        localAngVel.x *= 0.8f;
                        _rb.angularVelocity = transform.TransformDirection(localAngVel);
                    }
                }

                // Roll: angle of right vector with the horizontal plane
                Vector3 flatRight = Vector3.ProjectOnPlane(transform.right, Vector3.up);
                if (flatRight.sqrMagnitude > 0.001f)
                {
                    float rollAngle = Vector3.SignedAngle(flatRight, transform.right, transform.forward);
                    float maxAllowedRoll = 22f; // Ships cannot list more than 22 degrees unless capsized
                    if (Mathf.Abs(rollAngle) > maxAllowedRoll)
                    {
                        float rollCorrection = Mathf.Sign(rollAngle) * (Mathf.Abs(rollAngle) - maxAllowedRoll);
                        Vector3 rightingTorque = -transform.forward * (rollCorrection * _rb.mass * 12.0f);
                        _rb.AddTorque(rightingTorque, ForceMode.Force);

                        // Damp roll angular velocity
                        Vector3 localAngVel = transform.InverseTransformDirection(_rb.angularVelocity);
                        localAngVel.z *= 0.8f;
                        _rb.angularVelocity = transform.TransformDirection(localAngVel);
                    }
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            if (_probes != null && _probes.Length > 0)
            {
                foreach (var probe in _probes)
                {
                    if (probe != null)
                    {
                        Gizmos.DrawWireSphere(probe.position, 0.3f);
                        Gizmos.DrawLine(probe.position, probe.position - Vector3.up * _submersionDepth);
                    }
                }
            }
            else
            {
                Vector3 p1 = transform.TransformPoint(new Vector3(0f, 0f, 2.5f));
                Vector3 p2 = transform.TransformPoint(new Vector3(0f, 0f, -2.5f));
                Vector3 p3 = transform.TransformPoint(new Vector3(-1.2f, 0f, 0f));
                Vector3 p4 = transform.TransformPoint(new Vector3(1.2f, 0f, 0f));

                Gizmos.DrawWireSphere(p1, 0.3f);
                Gizmos.DrawWireSphere(p2, 0.3f);
                Gizmos.DrawWireSphere(p3, 0.3f);
                Gizmos.DrawWireSphere(p4, 0.3f);
            }
        }
#endif
    }
}
