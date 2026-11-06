using System;
using UnityEngine;

namespace Plunderlust.World
{
    /// <summary>
    /// Master 24-Hour Day/Night Simulation Engine.
    /// Drives dynamic celestial sun/moon arcs, atmospheric illumination, and temporal game events
    /// with zero runtime allocations.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class DayNightController : MonoBehaviour
    {
        public static DayNightController Instance { get; private set; }

        [Header("Time Configuration")]
        [Tooltip("Length of a full 24-hour day/night cycle in real-world seconds (e.g. 720s = 12 minutes).")]
        [SerializeField] private float _fullDayDurationSeconds = 720f;
        [Tooltip("Current hour of the day [0.0 .. 24.0]. 12.0 = Noon, 0.0 = Midnight.")]
        [Range(0f, 24f)]
        [SerializeField] private float _currentHour = 10.5f; // Starts mid-morning

        [Header("Celestial Lighting")]
        [SerializeField] private Light _sunLight;
        [SerializeField] private Light _moonLight;

        [Header("Light Color Gradients")]
        [SerializeField] private Color _dawnSunColor = new Color(1.0f, 0.65f, 0.35f);
        [SerializeField] private Color _noonSunColor = new Color(1.0f, 0.96f, 0.88f);
        [SerializeField] private Color _duskSunColor = new Color(1.0f, 0.42f, 0.22f);
        [SerializeField] private Color _nightMoonColor = new Color(0.45f, 0.62f, 0.85f);

        // Cached properties for zero-allocation access
        public float CurrentHour => _currentHour;
        public float NormalizedTime => _currentHour / 24.0f;
        public bool IsDay => _currentHour >= 5.5f && _currentHour < 18.5f;
        public bool IsNight => !IsDay;
        public bool IsMidnightFogEvent => _currentHour >= 0.0f && _currentHour < 4.0f;

        public static event Action<float> OnHourChanged; // (currentHour)
        public static event Action<bool> OnMidnightGhostEventChanged; // (isActive)

        private bool _wasMidnightActive = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureCelestialLights();
        }

        private void EnsureCelestialLights()
        {
            if (_sunLight == null)
            {
                // Find existing directional light in scene
                Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i].type == LightType.Directional)
                    {
                        _sunLight = lights[i];
                        break;
                    }
                }

                if (_sunLight == null)
                {
                    GameObject sunGo = new GameObject("Celestial_SunLight");
                    _sunLight = sunGo.AddComponent<Light>();
                    _sunLight.type = LightType.Directional;
                    _sunLight.shadows = LightShadows.Soft;
                }
            }

            if (_moonLight == null)
            {
                GameObject moonGo = new GameObject("Celestial_MoonLight");
                _moonLight = moonGo.AddComponent<Light>();
                _moonLight.type = LightType.Directional;
                _moonLight.color = _nightMoonColor;
                _moonLight.intensity = 0.25f;
                _moonLight.shadows = LightShadows.None;
            }
        }

        private void Update()
        {
            AdvanceTime(Time.deltaTime);
            UpdateCelestialLighting();
            CheckMidnightFogEvent();
        }

        public void SetTime(float hour)
        {
            _currentHour = Mathf.Repeat(hour, 24.0f);
            UpdateCelestialLighting();
            CheckMidnightFogEvent();
            OnHourChanged?.Invoke(_currentHour);
        }

        private void AdvanceTime(float deltaSeconds)
        {
            if (_fullDayDurationSeconds <= 0f) _fullDayDurationSeconds = 720f;
            float hoursPerSecond = 24.0f / _fullDayDurationSeconds;
            _currentHour += deltaSeconds * hoursPerSecond;
            if (_currentHour >= 24.0f)
            {
                _currentHour -= 24.0f;
            }
        }

        private void UpdateCelestialLighting()
        {
            // Calculate sun pitch (rising at 06:00, overhead at 12:00, setting at 18:00)
            float sunAngle = ((_currentHour - 6.0f) / 12.0f) * 180.0f;
            if (_sunLight != null)
            {
                _sunLight.transform.rotation = Quaternion.Euler(sunAngle, 65f, 0f);

                // Sun intensity & color lerp
                if (_currentHour >= 5.0f && _currentHour <= 19.0f)
                {
                    float sunHeight = Mathf.Sin(((_currentHour - 5.0f) / 14.0f) * Mathf.PI);
                    _sunLight.intensity = Mathf.Max(0f, sunHeight * 1.25f);

                    if (_currentHour < 8.0f)
                    {
                        float t = (_currentHour - 5.0f) / 3.0f;
                        _sunLight.color = Color.Lerp(_dawnSunColor, _noonSunColor, t);
                    }
                    else if (_currentHour > 16.0f)
                    {
                        float t = (_currentHour - 16.0f) / 3.0f;
                        _sunLight.color = Color.Lerp(_noonSunColor, _duskSunColor, t);
                    }
                    else
                    {
                        _sunLight.color = _noonSunColor;
                    }

                    _sunLight.enabled = true;
                }
                else
                {
                    _sunLight.intensity = 0f;
                    _sunLight.enabled = false;
                }
            }

            // Moon lighting (opposing angle)
            if (_moonLight != null)
            {
                float moonAngle = ((_currentHour + 6.0f) / 12.0f) * 180.0f;
                _moonLight.transform.rotation = Quaternion.Euler(moonAngle, 245f, 0f);

                if (!IsDay)
                {
                    _moonLight.enabled = true;
                    _moonLight.intensity = 0.25f;
                    _moonLight.color = _nightMoonColor;
                }
                else
                {
                    _moonLight.intensity = 0f;
                    _moonLight.enabled = false;
                }
            }
        }

        private void CheckMidnightFogEvent()
        {
            bool isMidnight = IsMidnightFogEvent;
            if (isMidnight != _wasMidnightActive)
            {
                _wasMidnightActive = isMidnight;
                OnMidnightGhostEventChanged?.Invoke(isMidnight);
                if (isMidnight)
                {
                    Debug.Log("<color=green>[DayNightController]</color> Midnight Fog Event Initiated (00:00 - 04:00). Spectral presence stirs in the mists...");
                }
            }
        }
    }
}
