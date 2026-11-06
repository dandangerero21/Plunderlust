using System.Runtime.CompilerServices;
using UnityEngine;

namespace Plunderlust.Ocean
{
    /// <summary>
    /// Deterministic, analytical wave simulation engine.
    /// Provides zero-allocation static wave height, normal, and displacement queries
    /// shared identically between C# buoyancy physics and URP ocean shaders.
    /// Fully supports Floating Origin shifts via WorldOffset.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class OceanWaveManager : MonoBehaviour
    {
        public static OceanWaveManager Instance { get; private set; }

        /// <summary>
        /// Global translation accumulated from FloatingOrigin shifts.
        /// Applied to all coordinate queries to prevent wave phase popping.
        /// </summary>
        public static Vector3 WorldOffset = Vector3.zero;

        // Wave 1: Primary Swell
        [Header("Primary Swell")]
        [SerializeField] private Vector2 _wave1Direction = new Vector2(1.0f, 0.35f);
        [SerializeField] private float _wave1Wavelength = 36.0f;
        [SerializeField] private float _wave1Amplitude = 0.12f;
        [SerializeField] private float _wave1Speed = 1.8f;
        [Range(0f, 1f)]
        [SerializeField] private float _wave1Steepness = 0.05f;

        // Wave 2: Secondary Cross-Chop
        [Header("Secondary Cross-Chop")]
        [SerializeField] private Vector2 _wave2Direction = new Vector2(-0.4f, 0.9f);
        [SerializeField] private float _wave2Wavelength = 15.0f;
        [SerializeField] private float _wave2Amplitude = 0.05f;
        [SerializeField] private float _wave2Speed = 1.3f;
        [Range(0f, 1f)]
        [SerializeField] private float _wave2Steepness = 0.03f;

        // Wave 3: Micro Ripple
        [Header("Micro Ripple")]
        [SerializeField] private Vector2 _wave3Direction = new Vector2(0.7f, -0.7f);
        [SerializeField] private float _wave3Wavelength = 6.0f;
        [SerializeField] private float _wave3Amplitude = 0.02f;
        [SerializeField] private float _wave3Speed = 1.0f;
        [Range(0f, 1f)]
        [SerializeField] private float _wave3Steepness = 0.02f;

        // Precalculated Static Parameters for zero runtime division
        private static float s_K1, s_Omega1, s_A1, s_D1x, s_D1z, s_Q1;
        private static float s_K2, s_Omega2, s_A2, s_D2x, s_D2z, s_Q2;
        private static float s_K3, s_Omega3, s_A3, s_D3x, s_D3z, s_Q3;
        private static bool s_IsInitialized = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            UpdateWaveParameters();
        }

        private void OnValidate()
        {
            UpdateWaveParameters();
        }

        /// <summary>
        /// Recalculates wave constants. Can be called when ocean parameters are altered at runtime.
        /// </summary>
        public void UpdateWaveParameters()
        {
            Vector2 d1 = _wave1Direction.sqrMagnitude > 0.001f ? _wave1Direction.normalized : new Vector2(1f, 0f);
            s_D1x = d1.x;
            s_D1z = d1.y;
            s_K1 = 2.0f * Mathf.PI / Mathf.Max(1.0f, _wave1Wavelength);
            s_Omega1 = s_K1 * _wave1Speed;
            s_A1 = _wave1Amplitude;
            s_Q1 = _wave1Steepness / Mathf.Max(0.001f, s_K1 * s_A1 * 3.0f);

            Vector2 d2 = _wave2Direction.sqrMagnitude > 0.001f ? _wave2Direction.normalized : new Vector2(0f, 1f);
            s_D2x = d2.x;
            s_D2z = d2.y;
            s_K2 = 2.0f * Mathf.PI / Mathf.Max(1.0f, _wave2Wavelength);
            s_Omega2 = s_K2 * _wave2Speed;
            s_A2 = _wave2Amplitude;
            s_Q2 = _wave2Steepness / Mathf.Max(0.001f, s_K2 * s_A2 * 3.0f);

            Vector2 d3 = _wave3Direction.sqrMagnitude > 0.001f ? _wave3Direction.normalized : new Vector2(0.7f, -0.7f);
            s_D3x = d3.x;
            s_D3z = d3.y;
            s_K3 = 2.0f * Mathf.PI / Mathf.Max(1.0f, _wave3Wavelength);
            s_Omega3 = s_K3 * _wave3Speed;
            s_A3 = _wave3Amplitude;
            s_Q3 = _wave3Steepness / Mathf.Max(0.001f, s_K3 * s_A3 * 3.0f);

            s_IsInitialized = true;
        }

        private static void EnsureDefaultInitialization()
        {
            if (s_IsInitialized) return;

            // Robust fallback defaults if no OceanWaveManager GameObject exists in the scene
            Vector2 d1 = new Vector2(1.0f, 0.35f).normalized;
            s_D1x = d1.x;
            s_D1z = d1.y;
            s_K1 = 2.0f * Mathf.PI / 36.0f;
            s_Omega1 = s_K1 * 1.8f;
            s_A1 = 0.12f;
            s_Q1 = 0.05f / (s_K1 * s_A1 * 3.0f);

            Vector2 d2 = new Vector2(-0.4f, 0.9f).normalized;
            s_D2x = d2.x;
            s_D2z = d2.y;
            s_K2 = 2.0f * Mathf.PI / 15.0f;
            s_Omega2 = s_K2 * 1.3f;
            s_A2 = 0.05f;
            s_Q2 = 0.03f / (s_K2 * s_A2 * 3.0f);

            Vector2 d3 = new Vector2(0.7f, -0.7f).normalized;
            s_D3x = d3.x;
            s_D3z = d3.y;
            s_K3 = 2.0f * Mathf.PI / 6.0f;
            s_Omega3 = s_K3 * 1.0f;
            s_A3 = 0.02f;
            s_Q3 = 0.02f / (s_K3 * s_A3 * 3.0f);

            s_IsInitialized = true;
        }

        /// <summary>
        /// Returns the exact surface wave height (Y) at world position (X, Z) at the given time.
        /// Zero allocations, deterministic, thread-safe.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetWaveHeight(float worldX, float worldZ, float time)
        {
            EnsureDefaultInitialization();

            float sampleX = worldX + WorldOffset.x;
            float sampleZ = worldZ + WorldOffset.z;

            float phase1 = s_K1 * (s_D1x * sampleX + s_D1z * sampleZ) - s_Omega1 * time;
            float phase2 = s_K2 * (s_D2x * sampleX + s_D2z * sampleZ) - s_Omega2 * time;
            float phase3 = s_K3 * (s_D3x * sampleX + s_D3z * sampleZ) - s_Omega3 * time;

            return s_A1 * Mathf.Sin(phase1) +
                   s_A2 * Mathf.Sin(phase2) +
                   s_A3 * Mathf.Sin(phase3);
        }

        /// <summary>
        /// Computes wave surface height and analytical surface normal at world position (X, Z).
        /// Zero allocations, accurate, eliminates finite-difference approximations.
        /// </summary>
        public static void GetWaveHeightAndNormal(float worldX, float worldZ, float time, out float height, out Vector3 normal)
        {
            EnsureDefaultInitialization();

            float sampleX = worldX + WorldOffset.x;
            float sampleZ = worldZ + WorldOffset.z;

            float phase1 = s_K1 * (s_D1x * sampleX + s_D1z * sampleZ) - s_Omega1 * time;
            float phase2 = s_K2 * (s_D2x * sampleX + s_D2z * sampleZ) - s_Omega2 * time;
            float phase3 = s_K3 * (s_D3x * sampleX + s_D3z * sampleZ) - s_Omega3 * time;

            float sin1 = Mathf.Sin(phase1);
            float cos1 = Mathf.Cos(phase1);
            float sin2 = Mathf.Sin(phase2);
            float cos2 = Mathf.Cos(phase2);
            float sin3 = Mathf.Sin(phase3);
            float cos3 = Mathf.Cos(phase3);

            height = s_A1 * sin1 + s_A2 * sin2 + s_A3 * sin3;

            // Partial derivatives with respect to X and Z
            float dYdX = s_A1 * s_K1 * s_D1x * cos1 +
                         s_A2 * s_K2 * s_D2x * cos2 +
                         s_A3 * s_K3 * s_D3x * cos3;

            float dYdZ = s_A1 * s_K1 * s_D1z * cos1 +
                         s_A2 * s_K2 * s_D2z * cos2 +
                         s_A3 * s_K3 * s_D3z * cos3;

            // Surface normal N = (-dY/dX, 1, -dY/dZ) normalized
            normal.x = -dYdX;
            normal.y = 1.0f;
            normal.z = -dYdZ;

            float sqrLen = normal.x * normal.x + normal.y * normal.y + normal.z * normal.z;
            if (sqrLen > 0.0001f)
            {
                float invLen = 1.0f / Mathf.Sqrt(sqrLen);
                normal.x *= invLen;
                normal.y *= invLen;
                normal.z *= invLen;
            }
            else
            {
                normal = Vector3.up;
            }
        }

        /// <summary>
        /// Returns full 3D Gerstner displacement offset (X, Y, Z) from flat rest plane.
        /// </summary>
        public static Vector3 GetDisplacement(float worldX, float worldZ, float time)
        {
            EnsureDefaultInitialization();

            float sampleX = worldX + WorldOffset.x;
            float sampleZ = worldZ + WorldOffset.z;

            float phase1 = s_K1 * (s_D1x * sampleX + s_D1z * sampleZ) - s_Omega1 * time;
            float phase2 = s_K2 * (s_D2x * sampleX + s_D2z * sampleZ) - s_Omega2 * time;
            float phase3 = s_K3 * (s_D3x * sampleX + s_D3z * sampleZ) - s_Omega3 * time;

            float cos1 = Mathf.Cos(phase1);
            float cos2 = Mathf.Cos(phase2);
            float cos3 = Mathf.Cos(phase3);

            float dispX = -s_Q1 * s_A1 * s_D1x * cos1 - s_Q2 * s_A2 * s_D2x * cos2 - s_Q3 * s_A3 * s_D3x * cos3;
            float dispZ = -s_Q1 * s_A1 * s_D1z * cos1 - s_Q2 * s_A2 * s_D2z * cos2 - s_Q3 * s_A3 * s_D3z * cos3;
            float dispY = s_A1 * Mathf.Sin(phase1) + s_A2 * Mathf.Sin(phase2) + s_A3 * Mathf.Sin(phase3);

            return new Vector3(dispX, dispY, dispZ);
        }

        /// <summary>
        /// Returns approximate horizontal surface orbital velocity of the wave.
        /// Useful for simulating water current pushing ships and floating cargo.
        /// </summary>
        public static Vector3 GetWaterVelocity(float worldX, float worldZ, float time)
        {
            EnsureDefaultInitialization();

            float sampleX = worldX + WorldOffset.x;
            float sampleZ = worldZ + WorldOffset.z;

            float phase1 = s_K1 * (s_D1x * sampleX + s_D1z * sampleZ) - s_Omega1 * time;
            float phase2 = s_K2 * (s_D2x * sampleX + s_D2z * sampleZ) - s_Omega2 * time;

            float vel1 = s_Omega1 * s_A1 * Mathf.Cos(phase1);
            float vel2 = s_Omega2 * s_A2 * Mathf.Cos(phase2);

            return new Vector3(
                s_D1x * vel1 + s_D2x * vel2,
                0f,
                s_D1z * vel1 + s_D2z * vel2
            );
        }
    }
}
