using System.Collections;
using Plunderlust.Wind;
using UnityEngine;

namespace Plunderlust.World
{
    /// <summary>
    /// Master Environmental Visuals & Atmosphere Director.
    /// Drives dynamic panoramic skybox exposure, wind-driven cloud rotation,
    /// horizon-matched distance fog, and procedural supernatural midnight encounters.
    /// </summary>
    [DefaultExecutionOrder(-190)]
    public class EnvironmentVisualsManager : MonoBehaviour
    {
        public static EnvironmentVisualsManager Instance { get; private set; }

        [Header("Kenney Skybox Textures")]
        [SerializeField] private Texture2D _skyboxMorningTex;
        [SerializeField] private Texture2D _skyboxDayTex;
        [SerializeField] private Texture2D _skyboxNightTex;

        [Header("Skybox Material Settings")]
        [SerializeField] private Material _skyboxRuntimeMaterial;
        [SerializeField] private float _baseRotationSpeed = 0.8f;
        [SerializeField] private float _windSpeedMultiplier = 2.5f;

        [Header("Atmospheric Fog Configuration")]
        [SerializeField] private bool _enableFog = true;
        [SerializeField] private float _dayFogDensity = 0.0016f;
        [SerializeField] private float _nightFogDensity = 0.0022f;
        [SerializeField] private float _midnightGhostFogDensity = 0.0045f;

        [Header("Horizon Fog Colors")]
        [SerializeField] private Color _dawnFogColor = new Color(0.85f, 0.62f, 0.45f);
        [SerializeField] private Color _noonFogColor = new Color(0.48f, 0.70f, 0.82f);
        [SerializeField] private Color _duskFogColor = new Color(0.76f, 0.42f, 0.32f);
        [SerializeField] private Color _nightFogColor = new Color(0.04f, 0.08f, 0.14f);
        [SerializeField] private Color _spectralGhostFogColor = new Color(0.06f, 0.24f, 0.18f);

        [Header("Midnight Ghost Encounter")]
        [SerializeField] private bool _enableMidnightGhostShip = true;
        [SerializeField] private float _ghostSpawnDistance = 140f;

        private float _currentSkyRotation = 0f;
        private Texture2D _currentAppliedTexture;
        private GameObject _activeGhostShip;
        private Transform _playerTransform;

        // Shader property IDs (zero-allocation)
        private static readonly int PropMainTex = Shader.PropertyToID("_MainTex");
        private static readonly int PropExposure = Shader.PropertyToID("_Exposure");
        private static readonly int PropRotation = Shader.PropertyToID("_Rotation");
        private static readonly int PropTint = Shader.PropertyToID("_Tint");

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureSkyboxMaterial();
            SetupFog();
        }

        private void Start()
        {
            FindPlayer();

            if (DayNightController.Instance != null)
            {
                DayNightController.OnMidnightGhostEventChanged += HandleMidnightGhostEventChanged;
            }
        }

        private void OnDestroy()
        {
            if (DayNightController.Instance != null)
            {
                DayNightController.OnMidnightGhostEventChanged -= HandleMidnightGhostEventChanged;
            }
        }

        private void FindPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerTransform = player.transform;
            }
        }

        private void EnsureSkyboxMaterial()
        {
            if (_skyboxMorningTex == null)
                _skyboxMorningTex = Resources.Load<Texture2D>("Skyboxes/skybox-morning");
            if (_skyboxDayTex == null)
                _skyboxDayTex = Resources.Load<Texture2D>("Skyboxes/skybox-day");
            if (_skyboxNightTex == null)
                _skyboxNightTex = Resources.Load<Texture2D>("Skyboxes/skybox-night");

#if UNITY_EDITOR
            if (_skyboxMorningTex == null)
                _skyboxMorningTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/kenney_skyboxes/Skyboxes/skybox-morning.png");
            if (_skyboxDayTex == null)
                _skyboxDayTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/kenney_skyboxes/Skyboxes/skybox-day.png");
            if (_skyboxNightTex == null)
                _skyboxNightTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/kenney_skyboxes/Skyboxes/skybox-night.png");
#endif
            if (_skyboxRuntimeMaterial == null)
            {
                Shader panShader = Shader.Find("Skybox/Panoramic");
                if (panShader == null) panShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (panShader == null) panShader = Shader.Find("Standard");

                _skyboxRuntimeMaterial = new Material(panShader)
                {
                    name = "M_RuntimeSkybox"
                };
            }

            RenderSettings.skybox = _skyboxRuntimeMaterial;
        }

        private void SetupFog()
        {
            if (!_enableFog) return;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = _dayFogDensity;
            RenderSettings.fogColor = _noonFogColor;
        }

        private void Update()
        {
            if (_playerTransform == null) FindPlayer();

            float hour = DayNightController.Instance != null ? DayNightController.Instance.CurrentHour : 12f;
            bool isMidnight = DayNightController.Instance != null && DayNightController.Instance.IsMidnightFogEvent;

            UpdateSkyboxProperties(hour, isMidnight);
            UpdateAtmosphericFog(hour, isMidnight);
        }

        private void UpdateSkyboxProperties(float hour, bool isMidnight)
        {
            if (_skyboxRuntimeMaterial == null) return;

            // 1. Wind-synced continuous rotation
            float windFactor = (WindManager.Instance != null) ? WindManager.Instance.WindStrength : 0.75f;
            float rotSpeed = _baseRotationSpeed + (windFactor * _windSpeedMultiplier);
            _currentSkyRotation = Mathf.Repeat(_currentSkyRotation + (rotSpeed * Time.deltaTime), 360f);
            _skyboxRuntimeMaterial.SetFloat(PropRotation, _currentSkyRotation);

            // 2. Select texture based on time of day
            Texture2D targetTex = _skyboxDayTex;
            if (hour >= 5.0f && hour < 8.5f)
            {
                targetTex = _skyboxMorningTex != null ? _skyboxMorningTex : _skyboxDayTex;
            }
            else if (hour >= 8.5f && hour < 17.5f)
            {
                targetTex = _skyboxDayTex;
            }
            else
            {
                targetTex = _skyboxNightTex != null ? _skyboxNightTex : _skyboxDayTex;
            }

            if (targetTex != null && targetTex != _currentAppliedTexture)
            {
                _currentAppliedTexture = targetTex;
                _skyboxRuntimeMaterial.SetTexture(PropMainTex, targetTex);
            }

            // 3. Exposure curve
            float targetExposure = 1.1f;
            if (hour >= 6.0f && hour <= 18.0f)
            {
                float t = Mathf.Sin(((hour - 6.0f) / 12.0f) * Mathf.PI);
                targetExposure = Mathf.Lerp(0.75f, 1.15f, t);
            }
            else
            {
                targetExposure = isMidnight ? 0.35f : 0.42f;
            }
            _skyboxRuntimeMaterial.SetFloat(PropExposure, targetExposure);

            // 4. Sky tint gradient
            Color tint = Color.white;
            if (hour >= 5.0f && hour < 8.0f)
            {
                float t = (hour - 5.0f) / 3.0f;
                tint = Color.Lerp(new Color(1f, 0.8f, 0.65f), Color.white, t);
            }
            else if (hour >= 16.5f && hour < 19.5f)
            {
                float t = (hour - 16.5f) / 3.0f;
                tint = Color.Lerp(Color.white, new Color(1f, 0.6f, 0.45f), t);
            }
            else if (hour >= 19.5f || hour < 5.0f)
            {
                tint = isMidnight ? new Color(0.55f, 0.85f, 0.70f) : new Color(0.6f, 0.7f, 0.85f);
            }

            _skyboxRuntimeMaterial.SetColor(PropTint, tint);
        }

        private void UpdateAtmosphericFog(float hour, bool isMidnight)
        {
            if (!_enableFog) return;

            Color targetFogColor;
            float targetFogDensity;

            if (isMidnight)
            {
                targetFogColor = _spectralGhostFogColor;
                targetFogDensity = _midnightGhostFogDensity;
            }
            else if (hour >= 5.0f && hour < 8.0f)
            {
                float t = (hour - 5.0f) / 3.0f;
                targetFogColor = Color.Lerp(_dawnFogColor, _noonFogColor, t);
                targetFogDensity = Mathf.Lerp(_nightFogDensity, _dayFogDensity, t);
            }
            else if (hour >= 8.0f && hour < 16.5f)
            {
                targetFogColor = _noonFogColor;
                targetFogDensity = _dayFogDensity;
            }
            else if (hour >= 16.5f && hour < 19.5f)
            {
                float t = (hour - 16.5f) / 3.0f;
                targetFogColor = Color.Lerp(_noonFogColor, _duskFogColor, t);
                targetFogDensity = Mathf.Lerp(_dayFogDensity, _nightFogDensity, t);
            }
            else
            {
                targetFogColor = _nightFogColor;
                targetFogDensity = _nightFogDensity;
            }

            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFogColor, Time.deltaTime * 1.5f);
            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, targetFogDensity, Time.deltaTime * 1.5f);
        }

        // --- Midnight Ghost Encounter (00:00 - 04:00) ---

        private void HandleMidnightGhostEventChanged(bool isMidnight)
        {
            if (isMidnight)
            {
                SpawnMidnightGhostShip();
            }
            else
            {
                DespawnMidnightGhostShip();
            }
        }

        private void SpawnMidnightGhostShip()
        {
            if (!_enableMidnightGhostShip || _activeGhostShip != null) return;
            if (_playerTransform == null) FindPlayer();
            if (_playerTransform == null) return;

            // Spawn ghost ship 140m ahead or across the wind
            Vector3 windDir = WindManager.Instance != null ? WindManager.Instance.WindDirection : Vector3.forward;
            Vector3 spawnOffset = (windDir + _playerTransform.forward).normalized * _ghostSpawnDistance;
            Vector3 spawnPos = _playerTransform.position + spawnOffset;
            spawnPos.y = 0f;

            _activeGhostShip = new GameObject("Encounter_CursedGhostGalleon");
            _activeGhostShip.transform.position = spawnPos;
            _activeGhostShip.transform.rotation = Quaternion.LookRotation(-spawnOffset);

            // Spawn ghost ship model with spectral green material
            Material ghostMat = PrefabRegistrySO.Instance.GhostShipMaterial;
            GameObject ghostModel = PrefabRegistrySO.Instance.Spawn(
                "ship-ghost",
                _activeGhostShip.transform,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one * 1.5f,
                ghostMat,
                addMeshCollider: true
            );

            // Add ghostly bow lantern
            GameObject lantern = new GameObject("GhostLantern");
            lantern.transform.SetParent(_activeGhostShip.transform, false);
            lantern.transform.localPosition = new Vector3(0f, 4.5f, 6.0f);
            Light pLight = lantern.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.color = new Color(0.2f, 1.0f, 0.65f);
            pLight.intensity = 3.5f;
            pLight.range = 35f;

            // Attach ghost encounter behavior
            _activeGhostShip.AddComponent<GhostShipEncounter>();

            Debug.Log($"<color=lime>[EnvironmentVisualsManager]</color> The Cursed Ghost Galleon has emerged from the midnight sea mist at {spawnPos}!");
        }

        private void DespawnMidnightGhostShip()
        {
            if (_activeGhostShip != null)
            {
                StartCoroutine(FadeOutGhostShip(_activeGhostShip));
                _activeGhostShip = null;
            }
        }

        private IEnumerator FadeOutGhostShip(GameObject shipGo)
        {
            if (shipGo == null) yield break;

            float elapsed = 0f;
            float duration = 3.5f;
            Vector3 startScale = shipGo.transform.localScale;

            while (elapsed < duration && shipGo != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                shipGo.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }

            if (shipGo != null)
            {
                Destroy(shipGo);
            }
        }
    }

    /// <summary>
    /// Ghostly phantom motion and spectral reward behavior for the Midnight Ghost Ship encounter.
    /// </summary>
    public class GhostShipEncounter : MonoBehaviour
    {
        private Transform _playerTransform;
        private float _bobTimer = 0f;
        private float _patrolSpeed = 4.5f;
        private bool _isHarvested = false;

        private void Start()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _playerTransform = player.transform;
        }

        private void Update()
        {
            // Phantom floating motion
            _bobTimer += Time.deltaTime * 1.2f;
            float bobY = Mathf.Sin(_bobTimer) * 0.45f;
            float rollZ = Mathf.Sin(_bobTimer * 0.8f) * 3.5f;

            transform.Translate(Vector3.forward * (_patrolSpeed * Time.deltaTime), Space.Self);
            Vector3 pos = transform.position;
            pos.y = bobY;
            transform.position = pos;
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, rollZ);

            // Proximity check with player (if player closes within 25m, claim ghostly treasure)
            if (!_isHarvested && _playerTransform != null)
            {
                float dist = Vector3.Distance(transform.position, _playerTransform.position);
                if (dist < 26.0f)
                {
                    HarvestGhostSalvage();
                }
            }
        }

        private void HarvestGhostSalvage()
        {
            _isHarvested = true;
            Debug.Log("<color=lime>[GhostEncounter]</color> Intercepted the Ghost Ship! Salvaged 500 Cursed Gold & Legendary Contraband!");

            Plunderlust.Cargo.ShipCargoHold cargo = _playerTransform.GetComponent<Plunderlust.Cargo.ShipCargoHold>();
            if (cargo != null)
            {
                cargo.AddCargo(Plunderlust.Core.Data.CargoItemType.Gold, 500, out _);
                cargo.AddCargo(Plunderlust.Core.Data.CargoItemType.Spices, 6, out _);
                cargo.AddCargo(Plunderlust.Core.Data.CargoItemType.Silk, 4, out _);
            }
        }
    }
}
