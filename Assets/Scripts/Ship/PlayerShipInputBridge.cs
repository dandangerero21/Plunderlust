using Plunderlust.Core.Data;
using UnityEngine;

namespace Plunderlust.Ship
{
    /// <summary>
    /// Development and runtime input bridge for the player's flagship.
    /// Reads inputs (keyboard, gamepads, or on-screen touch commands) and
    /// feeds steer values and sail state machine transitions to ShipLocomotionController.
    /// </summary>
    [RequireComponent(typeof(ShipLocomotionController))]
    public class PlayerShipInputBridge : MonoBehaviour
    {
        [Header("Keyboard Controls Guide")]
        [Tooltip("A/D or Left/Right: Steer Helm\nW / Up: Raise Sails (Anchored -> Battle -> Full)\nS / Down: Lower Sails (Full -> Battle -> Anchored)")]
        [SerializeField] private bool _enableKeyboardInput = true;

        [Header("Smoothing")]
        [SerializeField] private float _steerSmoothSpeed = 5.0f;
        [SerializeField] private float _throttleSmoothSpeed = 8.0f;

        private ShipLocomotionController _locomotion;
        private Plunderlust.Combat.NavalCombatController _combat;
        private float _targetSteer = 0f;
        private float _smoothedSteer = 0f;
        private float _targetThrottle = 0f;
        private float _smoothedThrottle = 0f;
        private float _reverseHoldTimer = 0f;
        private float _forwardHoldTimer = 0f;

        private void Awake()
        {
            _locomotion = GetComponent<ShipLocomotionController>();
            _combat = GetComponent<Plunderlust.Combat.NavalCombatController>();
        }

        private void Update()
        {
            // If the MobileInputBridge is active, it handles steering, throttle, and keyboard overrides
            if (MobileInputBridge.Instance != null) return;
            if (!_enableKeyboardInput || _locomotion == null) return;

            float dt = Time.deltaTime;

            // 1. Steering Input (A / D or Left / Right)
            _targetSteer = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                _targetSteer -= 1f;
            }
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                _targetSteer += 1f;
            }

            _smoothedSteer = Mathf.MoveTowards(_smoothedSteer, _targetSteer, _steerSmoothSpeed * dt);
            _locomotion.SteerInput = _smoothedSteer;

            // 2. Throttle & Propulsion Controls
            bool isRowboat = _locomotion.HullData != null && _locomotion.HullData.SupportsRowing && !_locomotion.HullData.SupportsSails;

            if (isRowboat)
            {
                // Pure Oar-Driven Rowboat (Hold W to row forward, Hold S to back-paddle reverse, Release to coast)
                _targetThrottle = 0f;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                {
                    _locomotion.SetSailState(SailState.Rowing);
                    _targetThrottle = 1.0f;
                }
                else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                {
                    _locomotion.SetSailState(SailState.Rowing);
                    _targetThrottle = -0.85f;
                }

                _smoothedThrottle = Mathf.MoveTowards(_smoothedThrottle, _targetThrottle, _throttleSmoothSpeed * 2.0f * dt);
                _locomotion.ThrottleInput = _smoothedThrottle;
            }
            else
            {
                // Sailing ships with wind-driven canvas & backing yards
                bool pressingForward = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
                bool tappedForward = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
                bool pressingReverse = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
                bool tappedReverse = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);

                // A. FORWARD / ACCELERATE
                if (pressingForward)
                {
                    _reverseHoldTimer = 0f;
                    _forwardHoldTimer += dt;

                    // If ship is currently anchored or in reverse, forward input immediately deploys BattleSails!
                    if (_locomotion.CurrentSailState == SailState.Anchored || _smoothedThrottle < -0.05f)
                    {
                        _locomotion.RaiseSails();
                        _targetThrottle = 1.0f;
                        _smoothedThrottle = Mathf.Max(0.5f, _smoothedThrottle); // Instant forward impulse
                    }
                    else if (tappedForward || _forwardHoldTimer > 0.40f)
                    {
                        // Holding forward continuously or tapping W steps sails up to Full Canvas
                        if (_locomotion.CurrentSailState == SailState.BattleSails)
                        {
                            _locomotion.RaiseSails();
                        }
                    }

                    _targetThrottle = 1.0f;
                }
                else
                {
                    _forwardHoldTimer = 0f;
                }

                // B. BRAKE / LOWER SAILS / REVERSE (Holding or tapping S ALWAYS slows/reverses, never accelerates)
                if (pressingReverse)
                {
                    _reverseHoldTimer += dt;

                    // If under Full Canvas, immediately drop to Battle Sails to bleed speed
                    if (_locomotion.CurrentSailState == SailState.FullCanvas)
                    {
                        _locomotion.LowerSails();
                    }
                    // If continuing to hold reverse or speed is low, drop to Anchored for backing yards
                    else if (_locomotion.CurrentSailState == SailState.BattleSails && (_reverseHoldTimer > 0.30f || tappedReverse))
                    {
                        _locomotion.LowerSails();
                    }

                    // Direct reverse demand: applies active braking then backing thrust
                    _targetThrottle = -0.85f;
                }
                else
                {
                    _reverseHoldTimer = 0f;
                }

                // C. NEUTRAL / CRUISE
                if (!pressingForward && !pressingReverse)
                {
                    if (_locomotion.CurrentSailState == SailState.Anchored)
                    {
                        _targetThrottle = 0f;
                    }
                    else if (_locomotion.CurrentSailState == SailState.BattleSails)
                    {
                        _targetThrottle = 0.70f; // Cruise steadily under battle sails
                    }
                    else // Full Canvas
                    {
                        _targetThrottle = 1.0f; // Cruise steadily under full canvas
                    }

                    // If was in reverse and released, snap to zero promptly
                    if (_smoothedThrottle < -0.05f)
                    {
                        _smoothedThrottle = Mathf.MoveTowards(_smoothedThrottle, 0f, _throttleSmoothSpeed * 3.0f * dt);
                    }
                }

                float smoothRate = (pressingReverse && _smoothedThrottle > 0f) ? _throttleSmoothSpeed * 2.5f : _throttleSmoothSpeed;
                _smoothedThrottle = Mathf.MoveTowards(_smoothedThrottle, _targetThrottle, smoothRate * dt);
                _locomotion.ThrottleInput = _smoothedThrottle;
            }

            // Quick Emergency Stop / Drop Anchor (X key)
            if (Input.GetKeyDown(KeyCode.X))
            {
                _locomotion.SetSailState(SailState.Anchored);
                _targetThrottle = 0f;
                _smoothedThrottle = 0f;
                _locomotion.ThrottleInput = 0f;
            }

            // 3. Broadside Combat & Ordnance (Q = Port, E = Starboard, Space = Swivel/Fire)
            if (_combat != null)
            {
                if (Input.GetKeyDown(KeyCode.Q))
                {
                    _combat.FirePortBroadside();
                }
                if (Input.GetKeyDown(KeyCode.E))
                {
                    _combat.FireStarboardBroadside();
                }
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    if (!_combat.FireForwardSwivel())
                    {
                        // If no swivel gun, fire ready broadside
                        if (!_combat.FirePortBroadside())
                        {
                            _combat.FireStarboardBroadside();
                        }
                    }
                }

                // Elevation adjustments (R = Pitch Up at Sails, F = Pitch Down at Hull)
                if (Input.GetKey(KeyCode.R)) _combat.AimPitchAngle = Mathf.Min(22f, _combat.AimPitchAngle + (15f * dt));
                if (Input.GetKey(KeyCode.F)) _combat.AimPitchAngle = Mathf.Max(-4f, _combat.AimPitchAngle - (15f * dt));
            }

            // Direct Number Key Overrides
            if (Input.GetKeyDown(KeyCode.Alpha0)) _locomotion.SetSailState(SailState.Anchored);
            if (Input.GetKeyDown(KeyCode.Alpha1)) _locomotion.SetSailState(SailState.Rowing);
            if (Input.GetKeyDown(KeyCode.Alpha2)) _locomotion.SetSailState(SailState.BattleSails);
            if (Input.GetKeyDown(KeyCode.Alpha3)) _locomotion.SetSailState(SailState.FullCanvas);
        }
    }
}
