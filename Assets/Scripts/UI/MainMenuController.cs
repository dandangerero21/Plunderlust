using System;
using System.Collections;
using Plunderlust.Audio;
using Plunderlust.Core.Data;
using Plunderlust.Core.Persistence;
using Plunderlust.Ocean;
using Plunderlust.Ship;
using Plunderlust.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Plunderlust.UI
{
    public enum MainMenuState
    {
        TitleScreen,
        NewVoyageChristening,
        ShipyardShowcase,
        SettingsModal,
        Settings = SettingsModal,
        TransitioningToGame
    }

    /// <summary>
    /// Dedicated 3D Live Main Menu Diorama, Shipyard Turntable & Audio Coordinator.
    /// Operates in the dedicated MainMenu scene with a live 3D harbor diorama (orbiting camera,
    /// moored brigantine, Gerstner ocean waves, wooden pier props), interactive 6-tier ship
    /// turntable showcase with touch drag-to-spin, flagship christening, and volume/resolution settings.
    /// On embark, fades smoothly and loads the naval combat arena scene (SampleScene).
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        public static MainMenuController Instance { get; private set; }

        private const string PrefRenderScale = "Plunderlust_RenderScale";
        private const string GameplaySceneName = "SampleScene";

        [Header("Diorama Coordinates & Camera Framing")]
        [SerializeField] private Vector3 _dioramaCenter = new Vector3(85f, 0f, 65f);
        // Wide orbit radius and elevated camera so tall masts and flags never clip
        [SerializeField] private float _dioramaOrbitRadius = 26.0f;
        [SerializeField] private float _dioramaOrbitHeight = 7.8f;
        [SerializeField] private float _dioramaOrbitSpeed = 1.9f;
        [SerializeField] private float _dioramaVerticalLookTarget = 4.2f;

        [Header("Turntable Showcase Coordinates")]
        [SerializeField] private Vector3 _turntableCenter = new Vector3(85f, 0f, 65f);
        [SerializeField] private float _turntableSpinSpeed = 10f;
        [SerializeField] private float _turntableDragSensitivity = 0.45f;

        private MainMenuState _currentState = MainMenuState.TitleScreen;

        // Diorama 3D GameObjects
        private GameObject _dioramaRoot;
        private Transform _mooredShipTransform;
        private Transform _turntableShipRoot;
        private GameObject _harborEnvironmentRoot;
        // 6 Tiers: Rowboat -> Cutter -> Sloop -> Brigantine -> Galleon -> Ghost Galleon
        private GameObject[] _turntableShipModels = new GameObject[6];
        private int _currentShowcaseTierIndex = 0; // Default to Tier 1 Rowboat to showcase starter craft
        private float _turntableCurrentYaw = 0f;
        private float _turntableYawVelocity = 0f;
        private bool _isDraggingTurntable = false;
        private Vector2 _lastPointerPos;

        // Adaptive Turntable Camera Framing based on ship size
        private static readonly float[] TurntableDistances = { 7.5f, 9.5f, 11.5f, 13.5f, 16.0f, 16.0f };
        private static readonly float[] TurntableHeights   = { 2.4f, 3.2f,  3.8f,  4.5f,  5.2f,  5.2f };

        // Camera Reference
        private UnityEngine.Camera _mainCamera;
        private float _orbitAngle = 45f;

        // Canvas & UI Screens
        private Canvas _canvas;
        private Image _screenFadeOverlay;
        private GameObject _titleScreenPanel;
        private GameObject _christeningModalPanel;
        private GameObject _showcasePanel;
        private GameObject _settingsModalPanel;

        // UI Controls - Title
        private Button _btnContinueVoyage;
        private Button _btnNewVoyage;
        private Button _btnShipyardShowcase;
        private Button _btnSettings;
        private Text _txtContinueStatus;

        // UI Controls - Christening
        private InputField _inputChristenedName;
        private Button _btnRandomizeName;
        private Button _btnConfirmSetSail;
        private Button _btnCancelChristen;

        // UI Controls - Showcase
        private Button[] _btnTierSelectors = new Button[6];
        private Text _txtShowcaseClassTitle;
        private Text _txtShowcaseRole;
        private Text _txtShowcaseHP;
        private Text _txtShowcaseSpeed;
        private Text _txtShowcaseGuns;
        private Text _txtShowcaseCargo;
        private Text _txtShowcaseCrew;
        private Text _txtShowcaseTraits;
        private Button _btnExitShowcase;

        // UI Controls - Settings
        private Slider _sliderMasterVol;
        private Slider _sliderMusicVol;
        private Slider _sliderSfxVol;
        private Text _txtMasterVol;
        private Text _txtMusicVol;
        private Text _txtSfxVol;
        private Button _btnScaleRetro;
        private Button _btnScaleCrisp;
        private Button _btnScaleNative;
        private Text _txtCurrentScale;
        private Button _btnCloseSettings;

        private static Font _cachedFont;
        public static Font GetDefaultFont()
        {
            if (_cachedFont == null)
            {
                _cachedFont = NauticalUITheme.GetDefaultFont();
                if (_cachedFont == null) _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_cachedFont == null) _cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (_cachedFont == null) _cachedFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
            }
            return _cachedFont;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // ponytail: lock target frame rate to 60fps and disable vSync fighting
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            // Debug: log screen dimensions to diagnose Android half-screen bug
            if (FindAnyObjectByType<Plunderlust.Diagnostics.ScreenDebugLogger>() == null)
            {
                gameObject.AddComponent<Plunderlust.Diagnostics.ScreenDebugLogger>();
            }

            EnsureEventSystem();
            EnsureCameraAndLighting();
            EnsureAudioManager();
            EnsureOceanWaveManager();
            EnsureWaterGrid();
            EnsureSaveManager();

            Build3DHarborDiorama();

            var existingCanvas = transform.Find("MainMenu_Canvas");
            if (existingCanvas != null)
            {
                DestroyImmediate(existingCanvas.gameObject);
            }

            BuildUIHierarchy();
            SetMenuState(MainMenuState.TitleScreen);
            ApplySavedRenderScale();
        }

        private void Start()
        {
            UpdateContinueButtonState();
            SetMenuState(MainMenuState.TitleScreen);

            if (NauticalAudioManager.Instance != null)
            {
                NauticalAudioManager.Instance.PlayMainMenuMusic();
            }

            StartCoroutine(FadeFromBlack(0.6f));
        }

        private void EnsureCameraAndLighting()
        {
            _mainCamera = UnityEngine.Camera.main;
            if (_mainCamera == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                _mainCamera = camGo.AddComponent<UnityEngine.Camera>();
                camGo.AddComponent<AudioListener>();
            }

            _mainCamera.clearFlags = CameraClearFlags.Skybox;
            _mainCamera.fieldOfView = 52f;

            Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            bool hasDir = false;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional) { hasDir = true; break; }
            }

            if (!hasDir)
            {
                GameObject sunGo = new GameObject("Sun_DirectionalLight");
                sunGo.transform.rotation = Quaternion.Euler(38f, 135f, 0f);
                Light sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = new Color(1.0f, 0.94f, 0.84f);
                sun.intensity = 1.25f;
                sun.shadows = LightShadows.Soft;
            }
        }

        private void EnsureOceanWaveManager()
        {
            if (FindAnyObjectByType<OceanWaveManager>() == null)
            {
                GameObject oceanGo = new GameObject("OceanWaveManager");
                oceanGo.AddComponent<OceanWaveManager>();
            }
        }

        private void EnsureWaterGrid()
        {
            if (FindAnyObjectByType<WaterGrid>() == null)
            {
                GameObject waterGo = new GameObject("OceanSurface");
                waterGo.transform.position = Vector3.zero;
                var grid = waterGo.AddComponent<WaterGrid>();
                var rend = waterGo.GetComponent<MeshRenderer>();
                var mat = Resources.Load<Material>("M_LowPolyOcean");
#if UNITY_EDITOR
                if (mat == null)
                {
                    mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_LowPolyOcean.mat");
                }
#endif
                if (mat != null && rend != null) rend.sharedMaterial = mat;
            }
        }

        private void EnsureAudioManager()
        {
            if (NauticalAudioManager.Instance == null)
            {
                GameObject audioGo = new GameObject("NauticalAudioManager");
                audioGo.AddComponent<NauticalAudioManager>();
            }
        }

        private void EnsureEventSystem()
        {
            var es = FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                GameObject esGo = new GameObject("EventSystem");
                es = esGo.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                esGo.AddComponent<InputSystemUIInputModule>();
#else
                esGo.AddComponent<StandaloneInputModule>();
#endif
            }
            else
            {
#if ENABLE_INPUT_SYSTEM
                var standalone = es.GetComponent<StandaloneInputModule>();
                if (standalone != null) Destroy(standalone);
                if (es.GetComponent<InputSystemUIInputModule>() == null)
                    es.gameObject.AddComponent<InputSystemUIInputModule>();
#else
                var inputModule = es.GetComponent<InputSystemUIInputModule>();
                if (inputModule != null) Destroy(inputModule);
                if (es.GetComponent<StandaloneInputModule>() == null)
                    es.gameObject.AddComponent<StandaloneInputModule>();
#endif
            }
        }

        private void EnsureSaveManager()
        {
            if (ShipSaveManager.Instance == null)
            {
                GameObject saveGo = new GameObject("ShipSaveManager");
                saveGo.AddComponent<ShipSaveManager>();
            }
        }

        private void ApplySavedRenderScale()
        {
            float scale = PlayerPrefs.GetFloat(PrefRenderScale, 0.50f);
            SetRenderScale(scale, false);
        }

        public void SetRenderScale(float scale, bool save = true)
        {
            scale = Mathf.Clamp(scale, 0.25f, 1.0f);
            if (save)
            {
                PlayerPrefs.SetFloat(PrefRenderScale, scale);
                PlayerPrefs.Save();
            }

            var urpAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null)
            {
                urpAsset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            }

            if (urpAsset != null)
            {
                urpAsset.renderScale = scale;
            }

            if (_txtCurrentScale != null)
            {
                if (Mathf.Approximately(scale, 0.25f)) _txtCurrentScale.text = "CURRENT: 0.25x (RETRO PIXEL)";
                else if (Mathf.Approximately(scale, 0.50f)) _txtCurrentScale.text = "CURRENT: 0.50x (CRISP LOW-POLY)";
                else _txtCurrentScale.text = "CURRENT: 1.00x (NATIVE WIDESCREEN)";
            }
        }

        // =====================================================================
        // 1. BUILD 3D HARBOR DIORAMA (KENNEY PIRATE KIT + BUOYANCY BOBBING)
        // =====================================================================

        private void Build3DHarborDiorama()
        {
            var existingDiorama = GameObject.Find("[Harbor_MainMenu_Diorama]");
            if (existingDiorama != null && existingDiorama.transform.childCount > 0)
            {
                _dioramaRoot = existingDiorama;
                var env = _dioramaRoot.transform.Find("Harbor_Environment_Props");
                if (env != null) _harborEnvironmentRoot = env.gameObject;

                var ship = _dioramaRoot.transform.Find("Moored_PirateBrigantine");
                if (ship != null) _mooredShipTransform = ship;

                var turntable = _dioramaRoot.transform.Find("Shipyard_Turntable_Root");
                if (turntable != null)
                {
                    _turntableShipRoot = turntable;
                    for (int i = 0; i < 6 && i < turntable.childCount; i++)
                    {
                        _turntableShipModels[i] = turntable.GetChild(i).gameObject;
                    }
                }
                return;
            }

            _dioramaRoot = new GameObject("[Harbor_MainMenu_Diorama]");
            _dioramaRoot.transform.position = _dioramaCenter;

            var registry = PrefabRegistrySO.Instance;
            if (registry == null) return;

            // Harbor Environment Container (dock, walkway, props, island beach, palm trees)
            // Can be cleanly hidden in Shipyard Showcase mode for 100% unobstructed vessel inspection!
            _harborEnvironmentRoot = new GameObject("Harbor_Environment_Props");
            _harborEnvironmentRoot.transform.SetParent(_dioramaRoot.transform, false);

            // 1. Wooden Pier Dock (runs along ship port side at z = -3.8m with 0.26m clean clearance from hull)
            registry.Spawn("platform-planks", _harborEnvironmentRoot.transform, new Vector3(-4.8f, 0.35f, -3.8f), Quaternion.Euler(0f, 90f, 0f), new Vector3(1.2f, 1f, 1.2f), null, false);
            registry.Spawn("platform-planks", _harborEnvironmentRoot.transform, new Vector3(-1.6f, 0.35f, -3.8f), Quaternion.Euler(0f, 90f, 0f), new Vector3(1.2f, 1f, 1.2f), null, false);
            registry.Spawn("platform-planks", _harborEnvironmentRoot.transform, new Vector3( 1.6f, 0.35f, -3.8f), Quaternion.Euler(0f, 90f, 0f), new Vector3(1.2f, 1f, 1.2f), null, false);
            registry.Spawn("platform-planks", _harborEnvironmentRoot.transform, new Vector3( 4.8f, 0.35f, -3.8f), Quaternion.Euler(0f, 90f, 0f), new Vector3(1.2f, 1f, 1.2f), null, false);

            // 2. Walkway Extending from Pier to Island Shoreline across 12m of open Caribbean water
            registry.Spawn("platform-planks", _harborEnvironmentRoot.transform, new Vector3(0f, 0.35f, -6.5f), Quaternion.identity, new Vector3(1.2f, 1f, 1.2f), null, false);
            registry.Spawn("platform-planks", _harborEnvironmentRoot.transform, new Vector3(0f, 0.35f, -9.5f), Quaternion.identity, new Vector3(1.2f, 1f, 1.2f), null, false);
            registry.Spawn("platform-planks", _harborEnvironmentRoot.transform, new Vector3(0f, 0.35f, -12.5f), Quaternion.identity, new Vector3(1.2f, 1f, 1.2f), null, false);
            registry.Spawn("platform-planks", _harborEnvironmentRoot.transform, new Vector3(0f, 0.35f, -15.5f), Quaternion.identity, new Vector3(1.2f, 1f, 1.2f), null, false);

            // 3. Pier Props: Mooring Barrels, Cargo Crates, Treasure Chest, Pirate Flag, Deck Cannon
            registry.Spawn("barrel", _harborEnvironmentRoot.transform, new Vector3(-3.5f, 0.75f, -3.6f), Quaternion.identity, Vector3.one * 1.1f, null, false);
            registry.Spawn("barrel", _harborEnvironmentRoot.transform, new Vector3(-3.0f, 0.75f, -3.7f), Quaternion.Euler(0f, 35f, 0f), Vector3.one * 1.1f, null, false);
            registry.Spawn("crate", _harborEnvironmentRoot.transform, new Vector3(3.0f, 0.75f, -3.6f), Quaternion.identity, Vector3.one, null, false);
            registry.Spawn("crate-bottles", _harborEnvironmentRoot.transform, new Vector3(3.0f, 1.25f, -3.6f), Quaternion.Euler(0f, 15f, 0f), Vector3.one, null, false);
            registry.Spawn("chest", _harborEnvironmentRoot.transform, new Vector3(-0.8f, 0.75f, -3.6f), Quaternion.Euler(0f, -25f, 0f), Vector3.one * 1.1f, null, false);
            registry.Spawn("flag-pirate", _harborEnvironmentRoot.transform, new Vector3(-4.8f, 0.75f, -3.6f), Quaternion.identity, Vector3.one * 1.3f, null, false);
            registry.Spawn("cannon", _harborEnvironmentRoot.transform, new Vector3(4.5f, 0.75f, -3.6f), Quaternion.Euler(0f, 180f, 0f), Vector3.one * 1.15f, null, false);

            // 4. Harbor Pier Lantern (warm amber light illuminating pier, water and ship hull)
            GameObject lanternGo = new GameObject("HarborPierLantern");
            lanternGo.transform.SetParent(_harborEnvironmentRoot.transform, false);
            lanternGo.transform.localPosition = new Vector3(0f, 1.6f, -3.6f);
            Light lanternLight = lanternGo.AddComponent<Light>();
            lanternLight.type = LightType.Point;
            lanternLight.color = new Color(1.0f, 0.68f, 0.32f);
            lanternLight.range = 14f;
            lanternLight.intensity = 2.2f;
            lanternLight.shadows = LightShadows.Soft;

            // 5. Tropical Island Shoreline & Grounded Submerged Foundation (at z = -22.0m)
            registry.Spawn("rocks-sand-b", _harborEnvironmentRoot.transform, new Vector3(0f, -1.8f, -22.0f), Quaternion.identity, new Vector3(9.0f, 3.5f, 7.0f), null, false);
            registry.Spawn("patch-sand", _harborEnvironmentRoot.transform, new Vector3(0f, -0.60f, -22.0f), Quaternion.identity, new Vector3(8.0f, 2.0f, 6.0f), null, false);
            registry.Spawn("patch-sand", _harborEnvironmentRoot.transform, new Vector3(0f, -0.10f, -22.0f), Quaternion.identity, new Vector3(6.5f, 1.4f, 5.0f), null, false);
            registry.Spawn("patch-sand-foliage", _harborEnvironmentRoot.transform, new Vector3(3f, 0.15f, -24.0f), Quaternion.Euler(0f, 45f, 0f), new Vector3(4.0f, 1.2f, 4.0f), null, false);
            registry.Spawn("patch-sand-foliage", _harborEnvironmentRoot.transform, new Vector3(-3f, 0.15f, -23.0f), Quaternion.Euler(0f, -30f, 0f), new Vector3(4.0f, 1.2f, 4.0f), null, false);

            // 6. Coastal Boulders & Palm Trees on Island Beach (rooted in foundation at Y = -0.35m)
            registry.Spawn("rocks-sand-b", _harborEnvironmentRoot.transform, new Vector3(-5.5f, -0.35f, -20.0f), Quaternion.Euler(0f, 25f, 0f), new Vector3(2.5f, 2.5f, 2.5f), null, false);
            registry.Spawn("rocks-sand-c", _harborEnvironmentRoot.transform, new Vector3(5.5f, -0.35f, -20.0f), Quaternion.Euler(0f, -40f, 0f), new Vector3(2.5f, 2.5f, 2.5f), null, false);
            registry.Spawn("palm-detailed-bend", _harborEnvironmentRoot.transform, new Vector3(3.5f, 0.25f, -21.0f), Quaternion.Euler(0f, 60f, 0f), Vector3.one * 1.3f, null, false);
            registry.Spawn("palm-detailed-straight", _harborEnvironmentRoot.transform, new Vector3(-3.0f, 0.25f, -22.0f), Quaternion.Euler(0f, 120f, 0f), Vector3.one * 1.2f, null, false);
            registry.Spawn("palm-bend", _harborEnvironmentRoot.transform, new Vector3(5.5f, 0.25f, -24.0f), Quaternion.Euler(0f, -80f, 0f), Vector3.one * 1.2f, null, false);

            // 7. Pirate Lookout Watchtower on Hillside
            registry.Spawn("tower-watch", _harborEnvironmentRoot.transform, new Vector3(0f, 0.2f, -26.0f), Quaternion.identity, Vector3.one * 1.3f, null, false);

            // 8. Moored Pirate Brigantine (floats in 100% open deep water alongside pier, perfectly centered)
            GameObject mooredShip = registry.Spawn("ship-pirate-large", _dioramaRoot.transform, Vector3.zero, Quaternion.Euler(0f, 90f, 0f), Vector3.one, null, false);
            if (mooredShip != null)
            {
                mooredShip.name = "Moored_PirateBrigantine";
                _mooredShipTransform = mooredShip.transform;
            }

            // 9. Turntable Hub (centered at diorama origin for seamless camera framing)
            GameObject turntableRoot = new GameObject("Shipyard_Turntable_Root");
            turntableRoot.transform.SetParent(_dioramaRoot.transform, false);
            turntableRoot.transform.localPosition = Vector3.zero;
            _turntableShipRoot = turntableRoot.transform;

            // 6-Tier Ship Models Catalog (Starter boat-row-small up to Cursed Ghost Galleon)
            string[] models = { "boat-row-small", "ship-small", "ship-medium", "ship-large", "ship-pirate-large", "ship-ghost" };
            for (int i = 0; i < 6; i++)
            {
                Material matOverride = (i == 5) ? registry.GhostShipMaterial : null;
                GameObject shipModel = registry.Spawn(models[i], _turntableShipRoot, Vector3.zero, Quaternion.identity, Vector3.one, matOverride, false);
                if (shipModel != null)
                {
                    shipModel.name = $"Turntable_Tier_{i + 1}_{models[i]}";
                    shipModel.SetActive(false);
                    _turntableShipModels[i] = shipModel;
                }
            }
        }

        // =====================================================================
        // 2. MAIN MENU UI (17TH-CENTURY NAUTICAL CARTOUCHE)
        // =====================================================================

        public void BuildUIHierarchy()
        {
            GameObject canvasGo = new GameObject("MainMenu_Canvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 500;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Safe Area Container
            GameObject safeAreaGo = new GameObject("SafeArea_Root");
            safeAreaGo.transform.SetParent(canvasGo.transform, false);
            RectTransform saRect = safeAreaGo.AddComponent<RectTransform>();
            saRect.anchorMin = Vector2.zero;
            saRect.anchorMax = Vector2.one;
            saRect.offsetMin = Vector2.zero;
            saRect.offsetMax = Vector2.zero;
            // Native full-screen canvas root without safe-area axis crushing

            // Screen Fade Overlay
            GameObject fadeGo = new GameObject("ScreenFadeOverlay");
            fadeGo.transform.SetParent(canvasGo.transform, false);
            RectTransform fadeRect = fadeGo.AddComponent<RectTransform>();
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = Vector2.zero;
            fadeRect.offsetMax = Vector2.zero;
            _screenFadeOverlay = fadeGo.AddComponent<Image>();
            _screenFadeOverlay.color = Color.black;
            _screenFadeOverlay.raycastTarget = false;

            // Panels
            BuildTitleScreenPanel(safeAreaGo.transform);
            BuildChristeningModalPanel(safeAreaGo.transform);
            BuildShipyardShowcasePanel(safeAreaGo.transform);
            BuildSettingsModalPanel(safeAreaGo.transform);
        }

        private void BuildTitleScreenPanel(Transform parent)
        {
            _titleScreenPanel = new GameObject("TitleScreen_Panel");
            _titleScreenPanel.transform.SetParent(parent, false);
            RectTransform rootRect = _titleScreenPanel.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // Left Side Cartouche Card (460px width, 760px height)
            GameObject cardGo = new GameObject("Title_Cartouche");
            cardGo.transform.SetParent(_titleScreenPanel.transform, false);
            RectTransform cardRect = cardGo.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.04f, 0.5f);
            cardRect.anchorMax = new Vector2(0.04f, 0.5f);
            cardRect.pivot = new Vector2(0f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(460f, 760f);

            Image cardBg = cardGo.AddComponent<Image>();
            cardBg.sprite = NauticalUITheme.MahoganyPanel;
            cardBg.type = Image.Type.Sliced;
            cardBg.color = new Color(0.12f, 0.08f, 0.06f, 0.95f);

            // Aged Parchment Inset
            GameObject insetGo = new GameObject("Cartouche_ParchmentInset");
            insetGo.transform.SetParent(cardGo.transform, false);
            RectTransform insetRect = insetGo.AddComponent<RectTransform>();
            insetRect.anchorMin = Vector2.zero;
            insetRect.anchorMax = Vector2.one;
            insetRect.offsetMin = new Vector2(16f, 16f);
            insetRect.offsetMax = new Vector2(-16f, -16f);
            Image insetBg = insetGo.AddComponent<Image>();
            insetBg.sprite = NauticalUITheme.ParchmentPanel;
            insetBg.type = Image.Type.Sliced;
            insetBg.color = new Color(0.90f, 0.83f, 0.69f, 0.96f);

            // Compass Rose Crest
            GameObject crestGo = new GameObject("CompassRose_Crest");
            crestGo.transform.SetParent(insetGo.transform, false);
            RectTransform crestRect = crestGo.AddComponent<RectTransform>();
            crestRect.anchorMin = new Vector2(0.5f, 0.88f);
            crestRect.anchorMax = new Vector2(0.5f, 0.88f);
            crestRect.pivot = new Vector2(0.5f, 0.5f);
            crestRect.anchoredPosition = Vector2.zero;
            crestRect.sizeDelta = new Vector2(88f, 88f);
            Image crestImg = crestGo.AddComponent<Image>();
            crestImg.sprite = NauticalUITheme.CompassRose;

            // Title "PLUNDERLUST"
            GameObject titleGo = new GameObject("Title_Text");
            titleGo.transform.SetParent(insetGo.transform, false);
            RectTransform titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.77f);
            titleRect.anchorMax = new Vector2(0.5f, 0.77f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = Vector2.zero;
            titleRect.sizeDelta = new Vector2(400f, 55f);
            Text titleTxt = titleGo.AddComponent<Text>();
            titleTxt.font = GetDefaultFont();
            titleTxt.fontSize = 38;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = new Color(0.22f, 0.14f, 0.08f);
            titleTxt.text = "PLUNDERLUST";

            // Subtitle
            GameObject subGo = new GameObject("Subtitle_Text");
            subGo.transform.SetParent(insetGo.transform, false);
            RectTransform subRect = subGo.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 0.71f);
            subRect.anchorMax = new Vector2(0.5f, 0.71f);
            subRect.pivot = new Vector2(0.5f, 0.5f);
            subRect.anchoredPosition = Vector2.zero;
            subRect.sizeDelta = new Vector2(400f, 30f);
            Text subTxt = subGo.AddComponent<Text>();
            subTxt.font = GetDefaultFont();
            subTxt.fontSize = 15;
            subTxt.fontStyle = FontStyle.Italic;
            subTxt.alignment = TextAnchor.MiddleCenter;
            subTxt.color = new Color(0.38f, 0.28f, 0.18f);
            subTxt.text = "A High Seas Tactical Pirate Adventure";

            // Button Stack
            float startY = 100f;
            float stepY = 82f;

            // 1. CONTINUE VOYAGE
            _btnContinueVoyage = CreateMenuButton(insetGo.transform, new Vector2(0f, startY), "CONTINUE VOYAGE", HandleContinueVoyageClicked);
            _txtContinueStatus = CreateSubtext(_btnContinueVoyage.transform, "Resume Saved Voyage");

            // 2. NEW VOYAGE
            _btnNewVoyage = CreateMenuButton(insetGo.transform, new Vector2(0f, startY - stepY), "NEW VOYAGE", HandleNewVoyageClicked);

            // 3. SHIPYARD SHOWCASE
            _btnShipyardShowcase = CreateMenuButton(insetGo.transform, new Vector2(0f, startY - stepY * 2), "SHIPYARD SHOWCASE", HandleShowcaseClicked);

            // 4. SETTINGS
            _btnSettings = CreateMenuButton(insetGo.transform, new Vector2(0f, startY - stepY * 3), "SETTINGS & FIDELITY", HandleSettingsClicked);

            // Footer Badge
            GameObject verGo = new GameObject("Version_Badge");
            verGo.transform.SetParent(insetGo.transform, false);
            RectTransform verRect = verGo.AddComponent<RectTransform>();
            verRect.anchorMin = new Vector2(0.5f, 0.05f);
            verRect.anchorMax = new Vector2(0.5f, 0.05f);
            verRect.pivot = new Vector2(0.5f, 0.5f);
            verRect.anchoredPosition = Vector2.zero;
            verRect.sizeDelta = new Vector2(380f, 24f);
            Text verTxt = verGo.AddComponent<Text>();
            verTxt.font = GetDefaultFont();
            verTxt.fontSize = 12;
            verTxt.alignment = TextAnchor.MiddleCenter;
            verTxt.color = new Color(0.48f, 0.38f, 0.28f);
            verTxt.text = "Unity 6 Naval Engine • URP Mobile Low-Poly";
        }

        private void BuildChristeningModalPanel(Transform parent)
        {
            _christeningModalPanel = new GameObject("ChristeningModal_Panel");
            _christeningModalPanel.transform.SetParent(parent, false);
            RectTransform rootRect = _christeningModalPanel.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image dimImg = _christeningModalPanel.AddComponent<Image>();
            dimImg.color = new Color(0.04f, 0.03f, 0.02f, 0.75f);

            GameObject boxGo = new GameObject("Christen_Box");
            boxGo.transform.SetParent(_christeningModalPanel.transform, false);
            RectTransform boxRect = boxGo.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.anchoredPosition = Vector2.zero;
            boxRect.sizeDelta = new Vector2(640f, 440f);

            Image boxBg = boxGo.AddComponent<Image>();
            boxBg.sprite = NauticalUITheme.MahoganyPanel;
            boxBg.type = Image.Type.Sliced;

            GameObject innerGo = new GameObject("Inner_Parchment");
            innerGo.transform.SetParent(boxGo.transform, false);
            RectTransform innerRect = innerGo.AddComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(16f, 16f);
            innerRect.offsetMax = new Vector2(-16f, -16f);
            Image innerBg = innerGo.AddComponent<Image>();
            innerBg.sprite = NauticalUITheme.ParchmentPanel;
            innerBg.type = Image.Type.Sliced;
            innerBg.color = new Color(0.92f, 0.85f, 0.72f);

            // Title
            GameObject titleGo = new GameObject("Christen_Title");
            titleGo.transform.SetParent(innerGo.transform, false);
            RectTransform titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0f, 145f);
            titleRect.sizeDelta = new Vector2(500f, 40f);
            Text titleTxt = titleGo.AddComponent<Text>();
            titleTxt.font = GetDefaultFont();
            titleTxt.fontSize = 24;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = new Color(0.18f, 0.12f, 0.08f);
            titleTxt.text = "CHRISTEN YOUR FIRST VESSEL";

            // Class Description
            GameObject classGo = new GameObject("Class_Desc");
            classGo.transform.SetParent(innerGo.transform, false);
            RectTransform classRect = classGo.AddComponent<RectTransform>();
            classRect.anchoredPosition = new Vector2(0f, 95f);
            classRect.sizeDelta = new Vector2(540f, 50f);
            Text classTxt = classGo.AddComponent<Text>();
            classTxt.font = GetDefaultFont();
            classTxt.fontSize = 15;
            classTxt.alignment = TextAnchor.MiddleCenter;
            classTxt.color = new Color(0.28f, 0.22f, 0.16f);
            classTxt.text = "Classification: Tier 1 Leaky Rowboat\nEvery legendary pirate begins with a humble vessel and wooden oars.";

            // Input Field
            GameObject inputGo = new GameObject("Name_InputField");
            inputGo.transform.SetParent(innerGo.transform, false);
            RectTransform inputRect = inputGo.AddComponent<RectTransform>();
            inputRect.anchoredPosition = new Vector2(0f, 25f);
            inputRect.sizeDelta = new Vector2(380f, 52f);

            Image inputBg = inputGo.AddComponent<Image>();
            inputBg.sprite = NauticalUITheme.MahoganyPanel;
            inputBg.type = Image.Type.Sliced;
            inputBg.color = new Color(0.15f, 0.11f, 0.08f);

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(inputGo.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 8f);
            textRect.offsetMax = new Vector2(-16f, -8f);
            Text txt = textGo.AddComponent<Text>();
            txt.font = GetDefaultFont();
            txt.fontSize = 22;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.96f, 0.85f, 0.45f);

            _inputChristenedName = inputGo.AddComponent<InputField>();
            _inputChristenedName.textComponent = txt;
            _inputChristenedName.text = "The Barnacle";
            _inputChristenedName.characterLimit = 24;

            // Randomize Dice Button
            _btnRandomizeName = CreateMenuButton(innerGo.transform, new Vector2(0f, -45f), "🎲 RANDOMIZE NAME", HandleRandomizeNameClicked, 280f, 44f);

            // Confirm & Cancel
            _btnConfirmSetSail = CreateMenuButton(innerGo.transform, new Vector2(-120f, -125f), "SET SAIL!", HandleConfirmSetSailClicked, 210f, 50f);
            _btnCancelChristen = CreateMenuButton(innerGo.transform, new Vector2(120f, -125f), "CANCEL", () => SetMenuState(MainMenuState.TitleScreen), 210f, 50f);
        }

        private void BuildShipyardShowcasePanel(Transform parent)
        {
            _showcasePanel = new GameObject("ShipyardShowcase_Panel");
            _showcasePanel.transform.SetParent(parent, false);
            RectTransform rootRect = _showcasePanel.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // Header Banner
            GameObject headerGo = new GameObject("Showcase_Header");
            headerGo.transform.SetParent(_showcasePanel.transform, false);
            RectTransform headRect = headerGo.AddComponent<RectTransform>();
            headRect.anchorMin = new Vector2(0.5f, 0.94f);
            headRect.anchorMax = new Vector2(0.5f, 0.94f);
            headRect.pivot = new Vector2(0.5f, 0.5f);
            headRect.anchoredPosition = Vector2.zero;
            headRect.sizeDelta = new Vector2(720f, 55f);
            Image headBg = headerGo.AddComponent<Image>();
            headBg.sprite = NauticalUITheme.MahoganyPanel;
            headBg.type = Image.Type.Sliced;
            headBg.color = new Color(0.12f, 0.08f, 0.05f, 0.95f);

            GameObject headTextGo = new GameObject("Header_Text");
            headTextGo.transform.SetParent(headerGo.transform, false);
            RectTransform htRect = headTextGo.AddComponent<RectTransform>();
            htRect.anchorMin = Vector2.zero;
            htRect.anchorMax = Vector2.one;
            Text headTxt = headTextGo.AddComponent<Text>();
            headTxt.font = GetDefaultFont();
            headTxt.fontSize = 24;
            headTxt.fontStyle = FontStyle.Bold;
            headTxt.alignment = TextAnchor.MiddleCenter;
            headTxt.color = new Color(0.96f, 0.82f, 0.35f);
            headTxt.text = "ROYAL FLEET ARCHIVES & SHIPYARD";

            // Tier Selector Row (6 Tiers)
            GameObject tierRowGo = new GameObject("TierSelector_Row");
            tierRowGo.transform.SetParent(_showcasePanel.transform, false);
            RectTransform tierRowRect = tierRowGo.AddComponent<RectTransform>();
            tierRowRect.anchorMin = new Vector2(0.5f, 0.86f);
            tierRowRect.anchorMax = new Vector2(0.5f, 0.86f);
            tierRowRect.pivot = new Vector2(0.5f, 0.5f);
            tierRowRect.anchoredPosition = Vector2.zero;
            tierRowRect.sizeDelta = new Vector2(980f, 44f);

            string[] tierLabels = { "TIER 1: ROWBOAT", "TIER 2: CUTTER", "TIER 3: SLOOP", "TIER 4: BRIGANTINE", "TIER 5: GALLEON", "TIER 6: GHOST" };
            for (int i = 0; i < 6; i++)
            {
                int tierIndex = i;
                float xPos = -400f + (i * 160f);
                _btnTierSelectors[i] = CreateMenuButton(tierRowGo.transform, new Vector2(xPos, 0f), tierLabels[i], () => SelectShowcaseTier(tierIndex), 150f, 38f);
            }

            // Specs Card on the Right
            GameObject cardGo = new GameObject("SpecsCard_Parchment");
            cardGo.transform.SetParent(_showcasePanel.transform, false);
            RectTransform cardRect = cardGo.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.96f, 0.5f);
            cardRect.anchorMax = new Vector2(0.96f, 0.5f);
            cardRect.pivot = new Vector2(1f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(440f, 540f);

            Image cardBg = cardGo.AddComponent<Image>();
            cardBg.sprite = NauticalUITheme.MahoganyPanel;
            cardBg.type = Image.Type.Sliced;
            cardBg.color = new Color(0.12f, 0.08f, 0.05f, 0.95f);

            GameObject cardInnerGo = new GameObject("Card_Inner");
            cardInnerGo.transform.SetParent(cardGo.transform, false);
            RectTransform innerCardRect = cardInnerGo.AddComponent<RectTransform>();
            innerCardRect.anchorMin = Vector2.zero;
            innerCardRect.anchorMax = Vector2.one;
            innerCardRect.offsetMin = new Vector2(14f, 14f);
            innerCardRect.offsetMax = new Vector2(-14f, -14f);
            Image innerCardBg = cardInnerGo.AddComponent<Image>();
            innerCardBg.sprite = NauticalUITheme.ParchmentPanel;
            innerCardBg.type = Image.Type.Sliced;
            innerCardBg.color = new Color(0.92f, 0.85f, 0.72f);

            _txtShowcaseClassTitle = CreateSpecsText(cardInnerGo.transform, new Vector2(0f, 215f), 22, FontStyle.Bold, new Color(0.18f, 0.12f, 0.08f), "Leaky Rowboat");
            _txtShowcaseRole = CreateSpecsText(cardInnerGo.transform, new Vector2(0f, 182f), 14, FontStyle.Italic, new Color(0.35f, 0.28f, 0.22f), "Light Coastal Dinghy & Recon Vessel");

            _txtShowcaseHP = CreateSpecsText(cardInnerGo.transform, new Vector2(0f, 130f), 15, FontStyle.Bold, new Color(0.2f, 0.15f, 0.1f), "Hull Armor: 100 HP");
            _txtShowcaseSpeed = CreateSpecsText(cardInnerGo.transform, new Vector2(0f, 90f), 15, FontStyle.Bold, new Color(0.2f, 0.15f, 0.1f), "Top Speed: 7.5 Knots (Oars)");
            _txtShowcaseGuns = CreateSpecsText(cardInnerGo.transform, new Vector2(0f, 50f), 15, FontStyle.Bold, new Color(0.2f, 0.15f, 0.1f), "Guns: 1 Forward Swivel Gun");
            _txtShowcaseCargo = CreateSpecsText(cardInnerGo.transform, new Vector2(0f, 10f), 15, FontStyle.Bold, new Color(0.2f, 0.15f, 0.1f), "Cargo Hold: 10 Tonnes");
            _txtShowcaseCrew = CreateSpecsText(cardInnerGo.transform, new Vector2(0f, -30f), 15, FontStyle.Bold, new Color(0.2f, 0.15f, 0.1f), "Crew Complement: 2 Hands");
            _txtShowcaseTraits = CreateSpecsText(cardInnerGo.transform, new Vector2(0f, -95f), 13, FontStyle.Normal, new Color(0.25f, 0.18f, 0.12f), "Special: Agile turning radius, low draft, silent oar stroke.");

            // Drag hint at bottom
            GameObject hintGo = new GameObject("Drag_Hint");
            hintGo.transform.SetParent(_showcasePanel.transform, false);
            RectTransform hintRect = hintGo.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 0.08f);
            hintRect.anchorMax = new Vector2(0.5f, 0.08f);
            hintRect.pivot = new Vector2(0.5f, 0.5f);
            hintRect.anchoredPosition = Vector2.zero;
            hintRect.sizeDelta = new Vector2(400f, 35f);
            Text hintTxt = hintGo.AddComponent<Text>();
            hintTxt.font = GetDefaultFont();
            hintTxt.fontSize = 15;
            hintTxt.alignment = TextAnchor.MiddleCenter;
            hintTxt.color = new Color(0.96f, 0.85f, 0.45f);
            hintTxt.text = "✦ DRAG TO ROTATE 360° ✦";

            // Back to Main Menu Button anchored cleanly to Top-Left corner
            _btnExitShowcase = CreateMenuButton(_showcasePanel.transform, Vector2.zero, "◀ MAIN MENU", () => SetMenuState(MainMenuState.TitleScreen), 220f, 48f);
            RectTransform exitRt = _btnExitShowcase.GetComponent<RectTransform>();
            exitRt.anchorMin = new Vector2(0f, 1f);
            exitRt.anchorMax = new Vector2(0f, 1f);
            exitRt.pivot = new Vector2(0f, 1f);
            exitRt.anchoredPosition = new Vector2(35f, -25f);
            _btnExitShowcase.transform.SetAsLastSibling();
        }

        private void BuildSettingsModalPanel(Transform parent)
        {
            _settingsModalPanel = new GameObject("SettingsModal_Panel");
            _settingsModalPanel.transform.SetParent(parent, false);
            RectTransform rootRect = _settingsModalPanel.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image dimImg = _settingsModalPanel.AddComponent<Image>();
            dimImg.color = new Color(0.04f, 0.03f, 0.02f, 0.78f);

            GameObject boxGo = new GameObject("Settings_Box");
            boxGo.transform.SetParent(_settingsModalPanel.transform, false);
            RectTransform boxRect = boxGo.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.anchoredPosition = Vector2.zero;
            boxRect.sizeDelta = new Vector2(680f, 530f);

            Image boxBg = boxGo.AddComponent<Image>();
            boxBg.sprite = NauticalUITheme.MahoganyPanel;
            boxBg.type = Image.Type.Sliced;

            GameObject innerGo = new GameObject("Inner_Parchment");
            innerGo.transform.SetParent(boxGo.transform, false);
            RectTransform innerRect = innerGo.AddComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(16f, 16f);
            innerRect.offsetMax = new Vector2(-16f, -16f);
            Image innerBg = innerGo.AddComponent<Image>();
            innerBg.sprite = NauticalUITheme.ParchmentPanel;
            innerBg.type = Image.Type.Sliced;
            innerBg.color = new Color(0.92f, 0.85f, 0.72f);

            // Title
            GameObject titleGo = new GameObject("Settings_Title");
            titleGo.transform.SetParent(innerGo.transform, false);
            RectTransform titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0f, 210f);
            titleRect.sizeDelta = new Vector2(400f, 40f);
            Text titleTxt = titleGo.AddComponent<Text>();
            titleTxt.font = GetDefaultFont();
            titleTxt.fontSize = 24;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = new Color(0.18f, 0.12f, 0.08f);
            titleTxt.text = "SETTINGS & FIDELITY";

            // Volume Sliders
            _sliderMasterVol = CreateVolumeRow(innerGo.transform, new Vector2(0f, 150f), "MASTER VOLUME", out _txtMasterVol, (val) =>
            {
                NauticalAudioManager.Instance?.SetMasterVolume(val);
                if (_txtMasterVol != null) _txtMasterVol.text = $"{Mathf.RoundToInt(val * 100)}%";
            });

            _sliderMusicVol = CreateVolumeRow(innerGo.transform, new Vector2(0f, 95f), "SEA CHANTEYS & MUSIC", out _txtMusicVol, (val) =>
            {
                NauticalAudioManager.Instance?.SetMusicVolume(val);
                if (_txtMusicVol != null) _txtMusicVol.text = $"{Mathf.RoundToInt(val * 100)}%";
            });

            _sliderSfxVol = CreateVolumeRow(innerGo.transform, new Vector2(0f, 40f), "CANNONS & SURF SFX", out _txtSfxVol, (val) =>
            {
                NauticalAudioManager.Instance?.SetSFXVolume(val);
                if (_txtSfxVol != null) _txtSfxVol.text = $"{Mathf.RoundToInt(val * 100)}%";
            });

            // Pixel Art Fidelity
            GameObject scaleLabelGo = new GameObject("Scale_Label");
            scaleLabelGo.transform.SetParent(innerGo.transform, false);
            RectTransform slRect = scaleLabelGo.AddComponent<RectTransform>();
            slRect.anchoredPosition = new Vector2(0f, -20f);
            slRect.sizeDelta = new Vector2(500f, 26f);
            Text slTxt = scaleLabelGo.AddComponent<Text>();
            slTxt.font = GetDefaultFont();
            slTxt.fontSize = 15;
            slTxt.fontStyle = FontStyle.Bold;
            slTxt.alignment = TextAnchor.MiddleCenter;
            slTxt.color = new Color(0.25f, 0.18f, 0.12f);
            slTxt.text = "VISUAL RENDERING RESOLUTION (PIXEL ART ENGINE)";

            _txtCurrentScale = CreateSpecsText(innerGo.transform, new Vector2(0f, -55f), 14, FontStyle.Italic, new Color(0.35f, 0.28f, 0.22f), "CURRENT: 0.50x (CRISP LOW-POLY)");

            _btnScaleRetro = CreateMenuButton(innerGo.transform, new Vector2(-180f, -105f), "0.25x RETRO", () => SetRenderScale(0.25f), 160f, 42f);
            _btnScaleCrisp = CreateMenuButton(innerGo.transform, new Vector2(0f, -105f), "0.50x CRISP", () => SetRenderScale(0.50f), 160f, 42f);
            _btnScaleNative = CreateMenuButton(innerGo.transform, new Vector2(180f, -105f), "1.00x NATIVE", () => SetRenderScale(1.00f), 160f, 42f);

            _btnCloseSettings = CreateMenuButton(innerGo.transform, new Vector2(0f, -180f), "SAVE & CLOSE", () => SetMenuState(MainMenuState.TitleScreen), 220f, 46f);
        }

        // =====================================================================
        // 3. MENU STATE TRANSITIONS & HANDLERS
        // =====================================================================

        public void SetMenuState(MainMenuState newState)
        {
            _currentState = newState;

            _titleScreenPanel.SetActive(_currentState == MainMenuState.TitleScreen);
            _christeningModalPanel.SetActive(_currentState == MainMenuState.NewVoyageChristening);
            _showcasePanel.SetActive(_currentState == MainMenuState.ShipyardShowcase);
            _settingsModalPanel.SetActive(_currentState == MainMenuState.Settings);

            if (_mooredShipTransform != null)
            {
                _mooredShipTransform.gameObject.SetActive(_currentState != MainMenuState.ShipyardShowcase);
            }

            if (_harborEnvironmentRoot != null)
            {
                _harborEnvironmentRoot.SetActive(_currentState != MainMenuState.ShipyardShowcase);
            }

            if (_turntableShipRoot != null)
            {
                _turntableShipRoot.gameObject.SetActive(_currentState == MainMenuState.ShipyardShowcase);
                if (_currentState == MainMenuState.ShipyardShowcase)
                {
                    SelectShowcaseTier(_currentShowcaseTierIndex);
                }
            }

            if (_currentState == MainMenuState.Settings)
            {
                SyncSettingsSliders();
            }

            NauticalAudioManager.Instance?.PlayUIClick();
        }

        private void SyncSettingsSliders()
        {
            if (NauticalAudioManager.Instance != null)
            {
                if (_sliderMasterVol != null) _sliderMasterVol.value = NauticalAudioManager.Instance.MasterVolume;
                if (_sliderMusicVol != null) _sliderMusicVol.value = NauticalAudioManager.Instance.MusicVolume;
                if (_sliderSfxVol != null) _sliderSfxVol.value = NauticalAudioManager.Instance.SFXVolume;

                if (_txtMasterVol != null) _txtMasterVol.text = $"{Mathf.RoundToInt(NauticalAudioManager.Instance.MasterVolume * 100)}%";
                if (_txtMusicVol != null) _txtMusicVol.text = $"{Mathf.RoundToInt(NauticalAudioManager.Instance.MusicVolume * 100)}%";
                if (_txtSfxVol != null) _txtSfxVol.text = $"{Mathf.RoundToInt(NauticalAudioManager.Instance.SFXVolume * 100)}%";
            }
        }

        private void UpdateContinueButtonState()
        {
            bool hasSave = ShipSaveManager.SaveExists();
            if (_btnContinueVoyage != null)
            {
                _btnContinueVoyage.interactable = hasSave;
                if (_txtContinueStatus != null)
                {
                    if (hasSave)
                    {
                        var save = ShipSaveManager.Instance != null ? ShipSaveManager.Instance.LoadSaveData() : null;
                        if (save != null)
                        {
                            string tName = save.tier switch
                            {
                                ShipTier.Tier2_CoastalCutter => "Cutter",
                                ShipTier.Tier3_PirateBrigantine => "Brigantine",
                                ShipTier.Tier4_RoyalGalleon => "Galleon",
                                _ => "Rowboat"
                            };
                            _txtContinueStatus.text = $"Resume: {save.shipCustomName} ({tName} • {save.currentGold}g)";
                        }
                        else
                        {
                            _txtContinueStatus.text = "Resume Saved Voyage";
                        }
                    }
                    else
                    {
                        _txtContinueStatus.text = "No voyage recorded in sea chest";
                    }
                }
            }
        }

        private void HandleContinueVoyageClicked()
        {
            NauticalAudioManager.Instance?.PlayUIClick();
            PlayerPrefs.SetInt("Plunderlust_ResumeSavedGame", 1);
            PlayerPrefs.Save();
            StartEmbarkTransition();
        }

        private void HandleNewVoyageClicked()
        {
            SetMenuState(MainMenuState.NewVoyageChristening);
            HandleRandomizeNameClicked();
        }

        private void HandleRandomizeNameClicked()
        {
            string randName = ShipIdentitySystem.GenerateRandomShipName(ShipTier.Tier1_LeakyRowboat);
            if (_inputChristenedName != null)
            {
                _inputChristenedName.text = randName;
            }
            NauticalAudioManager.Instance?.PlayUIClick();
        }

        private void HandleConfirmSetSailClicked()
        {
            string shipName = _inputChristenedName != null ? _inputChristenedName.text.Trim() : "The Barnacle";
            if (string.IsNullOrEmpty(shipName)) shipName = "The Barnacle";

            PlayerPrefs.SetInt("Plunderlust_ResumeSavedGame", 0);
            PlayerPrefs.SetInt("Plunderlust_TutorialCompleted", 0);
            PlayerPrefs.SetString("Plunderlust_NewVoyageName", shipName);
            PlayerPrefs.Save();

            // Clear old save so new voyage begins fresh
            if (ShipSaveManager.Instance != null)
            {
                ShipSaveManager.Instance.DeleteSave();
            }

            StartEmbarkTransition();
        }

        private void HandleShowcaseClicked()
        {
            SetMenuState(MainMenuState.ShipyardShowcase);
        }

        private void HandleSettingsClicked()
        {
            SetMenuState(MainMenuState.Settings);
        }

        // =====================================================================
        // 4. SHIPYARD TURNTABLE LOGIC (6 TIERS WITH BOAT-ROW-SMALL)
        // =====================================================================

        public void SelectShowcaseTier(int tierIndex)
        {
            _currentShowcaseTierIndex = Mathf.Clamp(tierIndex, 0, 5);

            for (int i = 0; i < 6; i++)
            {
                if (_turntableShipModels[i] != null)
                {
                    _turntableShipModels[i].SetActive(i == _currentShowcaseTierIndex);
                }
            }

            UpdateShowcaseSpecsCard(_currentShowcaseTierIndex);
            NauticalAudioManager.Instance?.PlayUIClick();
        }

        private void UpdateShowcaseSpecsCard(int tierIndex)
        {
            switch (tierIndex)
            {
                case 0:
                    _txtShowcaseClassTitle.text = "Leaky Rowboat";
                    _txtShowcaseRole.text = "Light Coastal Dinghy & Recon Vessel";
                    _txtShowcaseHP.text = "Hull Armor: 100 HP";
                    _txtShowcaseSpeed.text = "Top Speed: 7.5 Knots (Oars)";
                    _txtShowcaseGuns.text = "Guns: 1 Forward Swivel Gun";
                    _txtShowcaseCargo.text = "Cargo Hold: 10 Tonnes";
                    _txtShowcaseCrew.text = "Crew Complement: 2 Hands";
                    _txtShowcaseTraits.text = "Special: Agile turning radius, low draft, silent oar stroke.";
                    break;
                case 1:
                    _txtShowcaseClassTitle.text = "Harbor Skiff / Cutter";
                    _txtShowcaseRole.text = "Fast Courier & Harbor Scout";
                    _txtShowcaseHP.text = "Hull Armor: 160 HP";
                    _txtShowcaseSpeed.text = "Top Speed: 10.0 Knots";
                    _txtShowcaseGuns.text = "Guns: 1 Swivel Gun + 1 Port / 1 Starboard";
                    _txtShowcaseCargo.text = "Cargo Hold: 18 Tonnes";
                    _txtShowcaseCrew.text = "Crew Complement: 6 Hands";
                    _txtShowcaseTraits.text = "Special: Swift coastal cruiser; easy to handle in shallow bays.";
                    break;
                case 2:
                    _txtShowcaseClassTitle.text = "Jamaican Sloop";
                    _txtShowcaseRole.text = "Fast Commerce Raider & Smuggler";
                    _txtShowcaseHP.text = "Hull Armor: 240 HP";
                    _txtShowcaseSpeed.text = "Top Speed: 12.5 Knots";
                    _txtShowcaseGuns.text = "Guns: 2 Port / 2 Starboard";
                    _txtShowcaseCargo.text = "Cargo Hold: 28 Tonnes";
                    _txtShowcaseCrew.text = "Crew Complement: 10 Hands";
                    _txtShowcaseTraits.text = "Special: High top speed; capable of out-sailing heavy war frigates.";
                    break;
                case 3:
                    _txtShowcaseClassTitle.text = "Corsair Brigantine";
                    _txtShowcaseRole.text = "Tactical Raider & Broadside Specialist";
                    _txtShowcaseHP.text = "Hull Armor: 350 HP";
                    _txtShowcaseSpeed.text = "Top Speed: 10.5 Knots";
                    _txtShowcaseGuns.text = "Guns: 4 Port / 4 Starboard";
                    _txtShowcaseCargo.text = "Cargo Hold: 45 Tonnes";
                    _txtShowcaseCrew.text = "Crew Complement: 16 Hands";
                    _txtShowcaseTraits.text = "Special: Twin broadside batteries; the definitive pirate dread.";
                    break;
                case 4:
                    _txtShowcaseClassTitle.text = "Royal Frigate / Galleon";
                    _txtShowcaseRole.text = "Heavy Armored Capital Flagship";
                    _txtShowcaseHP.text = "Hull Armor: 550 HP";
                    _txtShowcaseSpeed.text = "Top Speed: 9.0 Knots";
                    _txtShowcaseGuns.text = "Guns: 6 Port / 6 Starboard";
                    _txtShowcaseCargo.text = "Cargo Hold: 70 Tonnes";
                    _txtShowcaseCrew.text = "Crew Complement: 28 Hands";
                    _txtShowcaseTraits.text = "Special: Devastating broadsides and thick oak timber armor.";
                    break;
                case 5:
                    _txtShowcaseClassTitle.text = "Cursed Ghost Galleon";
                    _txtShowcaseRole.text = "Legendary Spectral Phantom of the Deep";
                    _txtShowcaseHP.text = "Hull Armor: 750 HP (Regenerative)";
                    _txtShowcaseSpeed.text = "Top Speed: 13.5 Knots";
                    _txtShowcaseGuns.text = "Guns: 8 Spectral Ghost Cannons";
                    _txtShowcaseCargo.text = "Cargo Hold: 100 Tonnes (Soul Vault)";
                    _txtShowcaseCrew.text = "Crew Complement: Undead Phantoms";
                    _txtShowcaseTraits.text = "Special: Spectral hull phases through reefs and regenerates damage in fog.";
                    break;
            }
        }

        // =====================================================================
        // 5. UPDATE: CAMERA ORBIT, TURNTABLE SPIN & BUOYANCY BOBBING
        // =====================================================================

        private void Update()
        {
            // Back navigation (Escape key on PC or Android Back button)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_currentState == MainMenuState.ShipyardShowcase ||
                    _currentState == MainMenuState.Settings ||
                    _currentState == MainMenuState.NewVoyageChristening)
                {
                    SetMenuState(MainMenuState.TitleScreen);
                    return;
                }
            }

            float dt = Time.deltaTime;

            if (_currentState == MainMenuState.TitleScreen || _currentState == MainMenuState.Settings || _currentState == MainMenuState.NewVoyageChristening)
            {
                // Smooth Camera Orbit with wide radius and elevated view to frame full masts
                _orbitAngle += dt * _dioramaOrbitSpeed;
                float rad = _orbitAngle * Mathf.Deg2Rad;
                Vector3 camPos = _dioramaCenter + new Vector3(Mathf.Sin(rad) * _dioramaOrbitRadius, _dioramaOrbitHeight, Mathf.Cos(rad) * _dioramaOrbitRadius);
                Vector3 lookTarget = _dioramaCenter + Vector3.up * _dioramaVerticalLookTarget;
                Quaternion camRot = Quaternion.LookRotation(lookTarget - camPos);

                if (_mainCamera != null)
                {
                    _mainCamera.transform.position = Vector3.Lerp(_mainCamera.transform.position, camPos, dt * 5.0f);
                    _mainCamera.transform.rotation = Quaternion.Slerp(_mainCamera.transform.rotation, camRot, dt * 5.0f);
                }

                // Moored Brigantine Gentle Wave Bobbing
                if (_mooredShipTransform != null)
                {
                    Vector3 shipPos = _dioramaCenter;
                    OceanWaveManager.GetWaveHeightAndNormal(shipPos.x, shipPos.z, Time.time, out float waveY, out _);
                    shipPos.y = Mathf.Lerp(_mooredShipTransform.position.y, waveY - 0.15f, dt * 5.0f);
                    _mooredShipTransform.position = shipPos;

                    float roll = Mathf.Sin(Time.time * 1.2f) * 3.5f;
                    float pitch = Mathf.Cos(Time.time * 0.9f) * 2.5f;
                    _mooredShipTransform.rotation = Quaternion.Euler(pitch, 90f + Mathf.Sin(Time.time * 0.5f) * 2.0f, roll);
                }
            }
            else if (_currentState == MainMenuState.ShipyardShowcase)
            {
                // Adaptive Turntable Camera Framing based on selected vessel tier
                float targetDist = TurntableDistances[_currentShowcaseTierIndex];
                float targetH = TurntableHeights[_currentShowcaseTierIndex];

                Vector3 camPos = _turntableCenter + new Vector3(0f, targetH, -targetDist);
                Quaternion camRot = Quaternion.Euler(15f, 0f, 0f);
                if (_mainCamera != null)
                {
                    _mainCamera.transform.position = Vector3.Lerp(_mainCamera.transform.position, camPos, dt * 5.0f);
                    _mainCamera.transform.rotation = Quaternion.Slerp(_mainCamera.transform.rotation, camRot, dt * 5.0f);
                }

                HandleTurntableDragInput(dt);
            }
        }

        private void HandleTurntableDragInput(float dt)
        {
            bool pointerDown = false;
            Vector2 pointerPos = Vector2.zero;

            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                pointerDown = (t.phase == TouchPhase.Began || t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary);
                pointerPos = t.position;
            }
            else if (Input.GetMouseButton(0))
            {
                pointerDown = true;
                pointerPos = Input.mousePosition;
            }

            if (pointerDown)
            {
                if (!_isDraggingTurntable)
                {
                    _isDraggingTurntable = true;
                    _lastPointerPos = pointerPos;
                }
                else
                {
                    float deltaX = pointerPos.x - _lastPointerPos.x;
                    _turntableYawVelocity = -deltaX * _turntableDragSensitivity * 25f;
                    _lastPointerPos = pointerPos;
                }
            }
            else
            {
                _isDraggingTurntable = false;
                _turntableYawVelocity = Mathf.Lerp(_turntableYawVelocity, _turntableSpinSpeed, dt * 3.0f);
            }

            _turntableCurrentYaw += _turntableYawVelocity * dt;

            if (_turntableShipRoot != null)
            {
                Vector3 pos = _turntableCenter;
                OceanWaveManager.GetWaveHeightAndNormal(pos.x, pos.z, Time.time, out float waveY, out _);
                pos.y = waveY - 0.12f;
                _turntableShipRoot.position = pos;
                _turntableShipRoot.rotation = Quaternion.Euler(0f, _turntableCurrentYaw, 0f);
            }
        }

        // =====================================================================
        // 6. EMBARK TRANSITION & SCENE LOAD
        // =====================================================================

        private void StartEmbarkTransition()
        {
            if (_currentState == MainMenuState.TransitioningToGame) return;
            StartCoroutine(EmbarkTransitionSequence());
        }

        private IEnumerator EmbarkTransitionSequence()
        {
            _currentState = MainMenuState.TransitioningToGame;

            if (NauticalAudioManager.Instance != null)
            {
                NauticalAudioManager.Instance.PlaySFX(SFXType.ShipBell);
            }

            float fadeDuration = 0.8f;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / fadeDuration);
                if (_screenFadeOverlay != null)
                {
                    _screenFadeOverlay.color = new Color(0f, 0f, 0f, alpha);
                }
                yield return null;
            }

            SceneManager.LoadScene(GameplaySceneName);
        }

        private IEnumerator FadeFromBlack(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / duration);
                if (_screenFadeOverlay != null)
                {
                    _screenFadeOverlay.color = new Color(0f, 0f, 0f, alpha);
                }
                yield return null;
            }

            if (_screenFadeOverlay != null)
            {
                _screenFadeOverlay.color = new Color(0f, 0f, 0f, 0f);
            }
        }

        // =====================================================================
        // UI HELPERS (BUTTONS, TEXT, SLIDERS WITH NAUTICAL PALETTE)
        // =====================================================================

        private Button CreateMenuButton(Transform parent, Vector2 pos, string label, Action onClick, float width = 360f, float height = 48f)
        {
            GameObject btnGo = new GameObject($"Btn_{label.Replace(" ", "_")}");
            btnGo.transform.SetParent(parent, false);
            RectTransform rt = btnGo.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, height);

            Image img = btnGo.AddComponent<Image>();
            img.sprite = NauticalUITheme.BrassPanel;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.85f, 0.70f, 0.28f);

            Button btn = btnGo.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.15f, 1.1f, 0.9f);
            cb.pressedColor = new Color(0.7f, 0.55f, 0.2f);
            cb.disabledColor = new Color(0.5f, 0.45f, 0.4f, 0.55f);
            btn.colors = cb;

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(btnGo.transform, false);
            RectTransform trt = textGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            Text txt = textGo.AddComponent<Text>();
            txt.font = GetDefaultFont();
            txt.fontSize = 17;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.12f, 0.08f, 0.05f);
            txt.text = label;
            txt.raycastTarget = false;

            if (onClick != null)
            {
                btn.onClick.AddListener(() => onClick());
            }

            return btn;
        }

        private Text CreateSubtext(Transform parent, string message)
        {
            GameObject subGo = new GameObject("Subtext");
            subGo.transform.SetParent(parent, false);
            RectTransform rt = subGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -4f);
            rt.sizeDelta = new Vector2(340f, 18f);

            Text txt = subGo.AddComponent<Text>();
            txt.font = GetDefaultFont();
            txt.fontSize = 12;
            txt.fontStyle = FontStyle.Italic;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.42f, 0.32f, 0.22f);
            txt.text = message;
            txt.raycastTarget = false;
            return txt;
        }

        private Text CreateSpecsText(Transform parent, Vector2 pos, int size, FontStyle style, Color color, string content)
        {
            GameObject go = new GameObject("SpecsText");
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(390f, 32f);

            Text txt = go.AddComponent<Text>();
            txt.font = GetDefaultFont();
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.color = color;
            txt.text = content;
            return txt;
        }

        private Slider CreateVolumeRow(Transform parent, Vector2 pos, string label, out Text valText, Action<float> onValueChanged)
        {
            GameObject rowGo = new GameObject($"Row_{label.Replace(" ", "_")}");
            rowGo.transform.SetParent(parent, false);
            RectTransform rt = rowGo.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(560f, 40f);

            // Label
            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(rowGo.transform, false);
            RectTransform lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0.5f);
            lrt.anchorMax = new Vector2(0f, 0.5f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.anchoredPosition = Vector2.zero;
            lrt.sizeDelta = new Vector2(240f, 30f);
            Text ltxt = labelGo.AddComponent<Text>();
            ltxt.font = GetDefaultFont();
            ltxt.fontSize = 15;
            ltxt.fontStyle = FontStyle.Bold;
            ltxt.color = new Color(0.25f, 0.18f, 0.12f);
            ltxt.alignment = TextAnchor.MiddleLeft;
            ltxt.text = label;

            // Slider
            GameObject sliderGo = new GameObject("Slider");
            sliderGo.transform.SetParent(rowGo.transform, false);
            RectTransform srt = sliderGo.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.46f, 0.5f);
            srt.anchorMax = new Vector2(0.46f, 0.5f);
            srt.pivot = new Vector2(0f, 0.5f);
            srt.anchoredPosition = Vector2.zero;
            srt.sizeDelta = new Vector2(220f, 20f);

            // Background
            GameObject bgGo = new GameObject("Background");
            bgGo.transform.SetParent(sliderGo.transform, false);
            RectTransform bgrt = bgGo.AddComponent<RectTransform>();
            bgrt.anchorMin = Vector2.zero;
            bgrt.anchorMax = Vector2.one;
            bgrt.offsetMin = Vector2.zero;
            bgrt.offsetMax = Vector2.zero;
            Image bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.15f, 0.1f, 0.85f);

            // Fill Area
            GameObject fillAreaGo = new GameObject("Fill Area");
            fillAreaGo.transform.SetParent(sliderGo.transform, false);
            RectTransform fart = fillAreaGo.AddComponent<RectTransform>();
            fart.anchorMin = Vector2.zero;
            fart.anchorMax = Vector2.one;
            fart.offsetMin = Vector2.zero;
            fart.offsetMax = Vector2.zero;

            GameObject fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            RectTransform frt = fillGo.AddComponent<RectTransform>();
            frt.sizeDelta = Vector2.zero;
            Image fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.85f, 0.68f, 0.22f);

            // Handle
            GameObject handleAreaGo = new GameObject("Handle Slide Area");
            handleAreaGo.transform.SetParent(sliderGo.transform, false);
            RectTransform hart = handleAreaGo.AddComponent<RectTransform>();
            hart.anchorMin = Vector2.zero;
            hart.anchorMax = Vector2.one;
            hart.offsetMin = Vector2.zero;
            hart.offsetMax = Vector2.zero;

            GameObject handleGo = new GameObject("Handle");
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            RectTransform hrt = handleGo.AddComponent<RectTransform>();
            hrt.sizeDelta = new Vector2(22f, 22f);
            Image handleImg = handleGo.AddComponent<Image>();
            handleImg.sprite = NauticalUITheme.BrassPanel;
            handleImg.color = Color.white;

            Slider slider = sliderGo.AddComponent<Slider>();
            slider.fillRect = frt;
            slider.handleRect = hrt;
            slider.targetGraphic = handleImg;
            slider.minValue = 0f;
            slider.maxValue = 1f;

            // Value Text
            GameObject valGo = new GameObject("ValueText");
            valGo.transform.SetParent(rowGo.transform, false);
            RectTransform vrt = valGo.AddComponent<RectTransform>();
            vrt.anchorMin = new Vector2(1f, 0.5f);
            vrt.anchorMax = new Vector2(1f, 0.5f);
            vrt.pivot = new Vector2(1f, 0.5f);
            vrt.anchoredPosition = Vector2.zero;
            vrt.sizeDelta = new Vector2(60f, 30f);
            valText = valGo.AddComponent<Text>();
            valText.font = GetDefaultFont();
            valText.fontSize = 14;
            valText.fontStyle = FontStyle.Bold;
            valText.color = new Color(0.2f, 0.15f, 0.1f);
            valText.alignment = TextAnchor.MiddleRight;
            valText.text = "100%";

            if (onValueChanged != null)
            {
                slider.onValueChanged.AddListener((v) => onValueChanged(v));
            }

            return slider;
        }
    }
}
