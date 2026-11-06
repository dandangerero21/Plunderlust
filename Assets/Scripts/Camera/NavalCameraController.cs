using UnityEngine;

namespace Plunderlust.CameraControl
{
    /// <summary>
    /// Angled top-down naval camera inspired by classic tactical pirate RPGs (Sid Meier's Pirates!).
    /// Decouples from wave-induced ship roll and pitch to eliminate motion sickness and jitter,
    /// while smoothly tracking ship heading, velocity lead, and height.
    /// Operates with zero heap allocations during LateUpdate.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class NavalCameraController : MonoBehaviour
    {
        public static NavalCameraController Instance { get; private set; }

        [Header("Target Tracking")]
        [Tooltip("The ship or boat transform to follow.")]
        [SerializeField] private Transform _target;
        [Tooltip("Optional Rigidbody of target to calculate forward velocity lead.")]
        [SerializeField] private Rigidbody _targetRigidbody;

        [Header("Angled Top-Down Geometry")]
        [Tooltip("Downward pitch angle in degrees (e.g. 50 deg for classic angled top-down).")]
        [Range(30f, 75f)]
        [SerializeField] private float _pitchAngle = 52f;
        [Tooltip("Nominal distance from the ship along the camera view line.")]
        [SerializeField] private float _distance = 18f;
        [Tooltip("Height offset above the water plane target center.")]
        [SerializeField] private float _targetHeightOffset = 1.0f;

        [Header("Smooth Following & Damping")]
        [Tooltip("Position tracking smoothing speed.")]
        [SerializeField] private float _positionFollowSpeed = 6.0f;
        [Tooltip("Rotation yaw tracking smoothing speed. Set to 0 for locked North-up compass view.")]
        [SerializeField] private float _yawFollowSpeed = 2.5f;
        [Tooltip("Lead distance ahead of the ship based on forward velocity.")]
        [SerializeField] private float _velocityLeadMultiplier = 0.6f;
        [Tooltip("Maximum lead distance in meters.")]
        [SerializeField] private float _maxVelocityLead = 8.0f;

        [Header("Zoom Constraints")]
        [SerializeField] private float _minDistance = 9f;
        [SerializeField] private float _maxDistance = 32f;
        [SerializeField] private float _zoomSpeed = 4f;

        [Header("Cinematic Combat Shake & Trauma")]
        [SerializeField] private float _traumaDecayRate = 1.4f;
        [SerializeField] private float _maxShakeOffset = 0.65f;
        [SerializeField] private float _maxShakeAngle = 2.5f;

        // Internal cached tracking vectors (zero heap allocation)
        private Vector3 _currentFollowPos;
        private float _currentYaw;
        private Vector3 _desiredPosition;
        private Quaternion _desiredRotation;
        private Vector3 _targetLeadOffset;
        private float _currentTrauma = 0f;

        // Cinematic Cutscene Override State
        private bool _isCinematicOverride = false;
        private Vector3 _cinematicTargetPos;
        private Quaternion _cinematicTargetRot;
        private float _cinematicBlendSpeed = 3.5f;

        public bool IsCinematicOverride => _isCinematicOverride;

        public void SetCinematicPose(Vector3 worldPos, Quaternion worldRot, float blendSpeed = 3.5f)
        {
            _isCinematicOverride = true;
            _cinematicTargetPos = worldPos;
            _cinematicTargetRot = worldRot;
            _cinematicBlendSpeed = blendSpeed;
        }

        public void ReleaseCinematicPose()
        {
            _isCinematicOverride = false;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void AddTrauma(float amount)
        {
            _currentTrauma = Mathf.Clamp01(_currentTrauma + amount);
        }

        public Transform Target
        {
            get => _target;
            set
            {
                _target = value;
                if (_target != null)
                {
                    _targetRigidbody = _target.GetComponent<Rigidbody>();
                    SnapToTarget();
                }
            }
        }

        public float PitchAngle
        {
            get => _pitchAngle;
            set => _pitchAngle = Mathf.Clamp(value, 30f, 75f);
        }

        public float Distance
        {
            get => _distance;
            set => _distance = Mathf.Clamp(value, _minDistance, _maxDistance);
        }

        private void Start()
        {
            if (_target != null)
            {
                if (_targetRigidbody == null)
                {
                    _targetRigidbody = _target.GetComponent<Rigidbody>();
                }
                SnapToTarget();
            }
        }

        /// <summary>
        /// Immediately teleports the camera to the ideal angled top-down pose without smoothing.
        /// </summary>
        public void SnapToTarget()
        {
            if (_target == null) return;

            _currentFollowPos = _target.position;
            _currentYaw = _target.eulerAngles.y;

            ComputeTransformState(_currentFollowPos, _currentYaw, 0f, out _desiredPosition, out _desiredRotation);
            transform.position = _desiredPosition;
            transform.rotation = _desiredRotation;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;

            if (_isCinematicOverride)
            {
                transform.position = Vector3.Lerp(transform.position, _cinematicTargetPos, _cinematicBlendSpeed * dt);
                transform.rotation = Quaternion.Slerp(transform.rotation, _cinematicTargetRot, _cinematicBlendSpeed * dt);
                return;
            }

            if (_target == null) return;

            // 1. Zoom Support: Mouse Scroll Wheel (Desktop/Editor) + Two-Finger Pinch (Mobile)
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                _distance = Mathf.Clamp(_distance - (scroll * _zoomSpeed * 10f), _minDistance, _maxDistance);
            }
            else if (Input.touchCount >= 2)
            {
                Touch t0 = Input.GetTouch(0);
                Touch t1 = Input.GetTouch(1);
                Vector2 prevPos0 = t0.position - t0.deltaPosition;
                Vector2 prevPos1 = t1.position - t1.deltaPosition;
                float prevDist = (prevPos0 - prevPos1).magnitude;
                float curDist = (t0.position - t1.position).magnitude;
                float delta = curDist - prevDist;
                if (Mathf.Abs(delta) > 0.1f)
                {
                    _distance = Mathf.Clamp(_distance - (delta * 0.04f), _minDistance, _maxDistance);
                }
            }

            // 2. Velocity-based lead lookahead
            float forwardSpeed = 0f;
            if (_targetRigidbody != null)
            {
                forwardSpeed = Vector3.Dot(_targetRigidbody.linearVelocity, _target.forward);
            }
            float leadDist = Mathf.Clamp(forwardSpeed * _velocityLeadMultiplier, -2f, _maxVelocityLead);

            // 3. Smooth position tracking (locks height to average water plane offset, preventing wave bob jitter)
            Vector3 targetCenter = _target.position;
            targetCenter.y = _targetHeightOffset; // Keeps camera altitude stable regardless of wave pitch

            _currentFollowPos = Vector3.Lerp(_currentFollowPos, targetCenter, _positionFollowSpeed * dt);

            // 4. Smooth Yaw Tracking (decoupled from ship roll/pitch heeling)
            if (_yawFollowSpeed > 0.001f)
            {
                float targetYaw = _target.eulerAngles.y;
                _currentYaw = Mathf.LerpAngle(_currentYaw, targetYaw, _yawFollowSpeed * dt);
            }

            // 5. Evaluate final camera position and rotation
            ComputeTransformState(_currentFollowPos, _currentYaw, leadDist, out _desiredPosition, out _desiredRotation);

            // 6. Apply Non-Linear Perlin Screen Shake (Trauma)
            if (_currentTrauma > 0.001f)
            {
                _currentTrauma = Mathf.Max(0f, _currentTrauma - (_traumaDecayRate * dt));
                float shake = _currentTrauma * _currentTrauma; // Quadratic falloff
                float timeSample = Time.time * 28f;

                float offsetX = (Mathf.PerlinNoise(timeSample, 0f) - 0.5f) * 2f * _maxShakeOffset * shake;
                float offsetY = (Mathf.PerlinNoise(0f, timeSample) - 0.5f) * 2f * _maxShakeOffset * shake;
                float rotZ = (Mathf.PerlinNoise(timeSample, timeSample) - 0.5f) * 2f * _maxShakeAngle * shake;

                _desiredPosition += transform.right * offsetX + transform.up * offsetY;
                _desiredRotation *= Quaternion.Euler(0f, 0f, rotZ);
            }

            transform.position = _desiredPosition;
            transform.rotation = _desiredRotation;
        }

        private void ComputeTransformState(Vector3 center, float yaw, float lead, out Vector3 pos, out Quaternion rot)
        {
            rot = Quaternion.Euler(_pitchAngle, yaw, 0f);

            // Calculate directional offset from ship center
            Vector3 forwardPlanar = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 targetWithLead = center + (forwardPlanar * lead);

            // Position camera behind and above the ship along viewing axis
            pos = targetWithLead - (rot * Vector3.forward * _distance);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_target == null) return;

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, _target.position + Vector3.up * _targetHeightOffset);
            Gizmos.DrawWireSphere(_target.position + Vector3.up * _targetHeightOffset, 0.5f);
        }
#endif
    }
}
