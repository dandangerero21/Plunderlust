using UnityEngine;

namespace Plunderlust.Wind
{
    /// <summary>
    /// Global Naval Wind Manager.
    /// Smoothly rotates the global wind vector, models gust turbulence,
    /// and provides zero-allocation aerodynamic sail polar efficiency curves.
    /// </summary>
    [DefaultExecutionOrder(-250)]
    public class WindManager : MonoBehaviour
    {
        private static WindManager _instance;

        public static WindManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<WindManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("WindManager");
                        _instance = go.AddComponent<WindManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Wind Evolution Settings")]
        [Tooltip("Compass heading towards which the wind is blowing (0 = North, 90 = East, 180 = South, 270 = West).")]
        [Range(0f, 360f)]
        [SerializeField] private float _baseHeadingDegrees = 65f;
        [Tooltip("Speed in degrees per second at which base wind heading naturally shifts.")]
        [SerializeField] private float _headingShiftRate = 0.4f;
        [Tooltip("Max degrees the wind fluctuates around base heading.")]
        [SerializeField] private float _fluctuationMaxAngle = 20f;
        [Tooltip("Speed of heading fluctuation oscillations.")]
        [SerializeField] private float _fluctuationFrequency = 0.08f;

        [Header("Wind Strength & Gusts")]
        [Tooltip("Base wind strength scalar [0.2 .. 1.0].")]
        [Range(0.2f, 1.0f)]
        [SerializeField] private float _baseWindStrength = 0.75f;
        [Tooltip("Maximum gust amplitude added to base strength.")]
        [Range(0.0f, 0.4f)]
        [SerializeField] private float _gustAmplitude = 0.2f;
        [Tooltip("Frequency of gust occurrences.")]
        [SerializeField] private float _gustFrequency = 0.15f;
        [Tooltip("Max physical wind speed in meters per second.")]
        [SerializeField] private float _maxPhysicalWindSpeed = 16f;

        // Current evaluated wind state (cached for zero allocation)
        private float _currentHeadingDegrees = 65f;
        private Vector3 _currentWindDirection = new Vector3(0.906f, 0f, 0.422f);
        private float _currentWindStrength = 0.75f;
        private Vector3 _currentWindVelocity = new Vector3(10.87f, 0f, 5.06f);

        /// <summary>
        /// Normalized direction vector on the XZ plane towards which the wind is blowing.
        /// </summary>
        public Vector3 WindDirection => _currentWindDirection;

        /// <summary>
        /// Normalized wind strength scalar [0.2 .. 1.0].
        /// </summary>
        public float WindStrength => _currentWindStrength;

        /// <summary>
        /// Compass heading degrees [0 .. 360].
        /// </summary>
        public float WindHeadingDegrees => _currentHeadingDegrees;

        /// <summary>
        /// Real-world physical wind velocity vector (Direction * Strength * MaxSpeed).
        /// </summary>
        public Vector3 WindVelocity => _currentWindVelocity;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _currentHeadingDegrees = _baseHeadingDegrees;
            EvaluateWind(0f);
        }

        private void Update()
        {
            EvaluateWind(Time.time);
        }

        private void EvaluateWind(float time)
        {
            // Evolve base heading slowly over time
            _baseHeadingDegrees = Mathf.Repeat(_baseHeadingDegrees + (_headingShiftRate * Time.deltaTime), 360f);

            // Add smooth dual-harmonic fluctuation
            float fluctuation = Mathf.Sin(time * _fluctuationFrequency) * _fluctuationMaxAngle +
                                Mathf.Cos(time * _fluctuationFrequency * 2.3f) * (_fluctuationMaxAngle * 0.35f);

            _currentHeadingDegrees = Mathf.Repeat(_baseHeadingDegrees + fluctuation, 360f);

            // Convert heading degrees to XZ unit vector
            float rad = _currentHeadingDegrees * Mathf.Deg2Rad;
            _currentWindDirection.x = Mathf.Sin(rad);
            _currentWindDirection.y = 0f;
            _currentWindDirection.z = Mathf.Cos(rad);

            // Evaluate wind gust strength [0.2 .. 1.0]
            float gust = (Mathf.Sin(time * _gustFrequency) * 0.6f + Mathf.Cos(time * _gustFrequency * 1.7f) * 0.4f) * _gustAmplitude;
            _currentWindStrength = Mathf.Clamp(_baseWindStrength + gust, 0.2f, 1.0f);

            _currentWindVelocity.x = _currentWindDirection.x * (_currentWindStrength * _maxPhysicalWindSpeed);
            _currentWindVelocity.y = 0f;
            _currentWindVelocity.z = _currentWindDirection.z * (_currentWindStrength * _maxPhysicalWindSpeed);
        }

        /// <summary>
        /// Calculates aerodynamic sail polar efficiency [0.05 .. 1.0] based on the ship's forward vector.
        /// Models naval physics: Broad Reach & Beam Reach deliver max speed; In Irons (<35 deg into wind) stalls.
        /// </summary>
        /// <param name="shipForward">Ship's normalized forward vector.</param>
        /// <returns>Efficiency multiplier [0.05 .. 1.0].</returns>
        public float GetWindPolarEfficiency(Vector3 shipForward)
        {
            // Dot product between ship forward and wind vector:
            // +1.0 = Sailing directly with the wind (Running / Tailwind)
            //  0.0 = Sailing 90 degrees across the wind (Beam Reach)
            // -1.0 = Sailing directly into the teeth of the wind (In Irons)
            float dot = shipForward.x * _currentWindDirection.x + shipForward.z * _currentWindDirection.z;

            // In Irons (dot < -0.70, heading within ~45 degrees opposing wind)
            // Ensures ships always have mobility (42% to 58% speed) rather than stalling dead to zero
            if (dot < -0.70f)
            {
                float t = (dot + 1.0f) / 0.30f; // 0 at dot = -1.0, 1 at dot = -0.70
                return Mathf.Lerp(0.42f, 0.58f, t);
            }

            // Close-Hauled (-0.70 <= dot < 0.0, i.e. 45 to 90 degrees to wind)
            if (dot < 0.0f)
            {
                float t = (dot + 0.70f) / 0.70f;
                return Mathf.Lerp(0.58f, 0.92f, t);
            }

            // Beam Reach to Broad Reach (0.0 <= dot <= 0.70, i.e. 90 to 135 degrees)
            if (dot <= 0.70f)
            {
                float t = dot / 0.70f;
                return Mathf.Lerp(0.92f, 1.20f, t);
            }

            // Running before the wind (0.70 < dot <= 1.0, fast following wind)
            float runningT = (dot - 0.70f) / 0.30f;
            return Mathf.Lerp(1.20f, 1.08f, runningT);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Vector3 origin = transform.position + Vector3.up * 5f;
            Gizmos.DrawRay(origin, _currentWindDirection * 10f * _currentWindStrength);
        }
#endif
    }
}
