using System;
using System.Collections;
using Plunderlust.Audio;
using Plunderlust.Core.Persistence;
using Plunderlust.Ship;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Plunderlust.UI
{
    /// <summary>
    /// Tactical In-Game Pause Menu & Settings Modal.
    /// Provides:
    /// 1. Dedicated HUD Pause touch button and keyboard shortcuts (ESC / P).
    /// 2. Clean game engine freeze via Time.timeScale = 0.0f and mobile input blocking.
    /// 3. Authentic 17th-Century Nautical Cartouche UI matching Main Menu styling:
    ///    - Resume Voyage
    ///    - Audio Settings (Master, Sea Chanteys/Music, Cannon & Surf SFX)
    ///    - Visual Rendering Fidelity (0.25x Retro, 0.50x Crisp Low-Poly, 1.00x Native HD)
    ///    - Restart Voyage (reloads combat arena fresh)
    ///    - Save & Return to Main Menu (persists ship state via ShipSaveManager, chimes bell, and loads MainMenu scene)
    /// </summary>
    [DefaultExecutionOrder(15)]
    public class PauseMenuUI : MonoBehaviour
    {
        public static PauseMenuUI Instance { get; private set; }

        public static bool IsPaused { get; private set; }
        public static event Action<bool> OnPauseStateChanged;

        private const string PrefRenderScale = "Plunderlust_RenderScale";
        private const string MainMenuSceneName = "MainMenu";
        private const string GameplaySceneName = "SampleScene";

        private Canvas _canvas;
        private GameObject _hudButtonGo;
        private GameObject _modalRootGo;
        private GameObject _mainPausePanel;
        private GameObject _settingsSubPanel;
        private Image _screenFadeOverlay;

        // Header / Identity Text
        private Text _hudButtonText;
        private Text _txtShipIdentity;
        private Text _txtCurrentScale;

        // Audio Sliders
        private Slider _sliderMasterVol;
        private Slider _sliderMusicVol;
        private Slider _sliderSfxVol;
        private Text _txtMasterVol;
        private Text _txtMusicVol;
        private Text _txtSfxVol;

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

            EnsureEventSystem();
            BuildPauseCanvas();
            SetPaused(false);
            ApplySavedRenderScale();
        }

        private void OnDestroy()
        {
            if (IsPaused)
            {
                Time.timeScale = 1.0f;
                IsPaused = false;
            }
        }

        private void Update()
        {
            if (GameOverUI.IsGameOver) return;

            // Toggle pause via Escape or P
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
            {
                TogglePause();
            }
        }

        public void TogglePause()
        {
            if (GameOverUI.IsGameOver) return;
            SetPaused(!IsPaused);
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            Time.timeScale = paused ? 0.0f : 1.0f;

            if (_modalRootGo != null)
            {
                _modalRootGo.SetActive(paused);
            }

            if (paused)
            {
                ShowMainPausePanel();
                RefreshShipIdentityHeader();
                SyncSettingsSliders();
                NauticalAudioManager.Instance?.PlayUIClick();
            }

            OnPauseStateChanged?.Invoke(paused);
        }

        private void RefreshShipIdentityHeader()
        {
            if (_txtShipIdentity == null) return;

            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                ShipIdentitySystem identity = playerGo.GetComponent<ShipIdentitySystem>();
                if (identity != null)
                {
                    _txtShipIdentity.text = $"FLAGSHIP: {identity.FullShipTitle.ToUpper()}";
                    return;
                }
            }

            _txtShipIdentity.text = "FLAGSHIP: THE BARNACLE • CORSAIR VESSEL";
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

        private void ShowMainPausePanel()
        {
            if (_mainPausePanel != null) _mainPausePanel.SetActive(true);
            if (_settingsSubPanel != null) _settingsSubPanel.SetActive(false);
        }

        private void ShowSettingsPanel()
        {
            if (_mainPausePanel != null) _mainPausePanel.SetActive(false);
            if (_settingsSubPanel != null) _settingsSubPanel.SetActive(true);
            SyncSettingsSliders();
            NauticalAudioManager.Instance?.PlayUIClick();
        }

        // =====================================================================
        // UI BUILDER (UGUI CANVAS + 17TH-CENTURY NAUTICAL CARTOUCHE)
        // =====================================================================

        private void BuildPauseCanvas()
        {
            GameObject canvasGo = new GameObject("PauseMenu_Canvas");
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 600; // Above general HUD, below system alerts

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Safe Area Wrapper
            GameObject safeAreaGo = new GameObject("SafeAreaContainer");
            safeAreaGo.transform.SetParent(canvasGo.transform, false);
            RectTransform saRect = safeAreaGo.AddComponent<RectTransform>();
            saRect.anchorMin = Vector2.zero;
            saRect.anchorMax = Vector2.one;
            saRect.sizeDelta = Vector2.zero;
            safeAreaGo.AddComponent<SafeAreaFitter>();

            BuildHUDPauseButton(saRect);
            BuildModalOverlay(saRect);
            BuildScreenFadeOverlay(canvasGo.transform);
        }

        private void BuildHUDPauseButton(RectTransform parent)
        {
            _hudButtonGo = new GameObject("HUD_PauseButton");
            _hudButtonGo.transform.SetParent(parent, false);

            RectTransform btnRt = _hudButtonGo.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0f, 1f);
            btnRt.anchorMax = new Vector2(0f, 1f);
            btnRt.pivot = new Vector2(0f, 1f);
            // Positioned right beside the Cargo Hold button on the top-left (Cargo button is at X=24, Width=214)
            btnRt.anchoredPosition = new Vector2(246f, -218f);
            btnRt.sizeDelta = new Vector2(118f, 44f);

            Image btnImg = _hudButtonGo.AddComponent<Image>();
            btnImg.sprite = NauticalUITheme.MahoganyPanel;
            btnImg.type = Image.Type.Sliced;
            btnImg.color = Color.white;

            Button btn = _hudButtonGo.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = NauticalUITheme.BrassHighlight;
            cb.pressedColor = NauticalUITheme.BrassDark;
            btn.colors = cb;
            btn.onClick.AddListener(TogglePause);

            GameObject textGo = new GameObject("ButtonText");
            textGo.transform.SetParent(_hudButtonGo.transform, false);
            RectTransform textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;

            _hudButtonText = textGo.AddComponent<Text>();
            _hudButtonText.text = "⏸ <b>PAUSE</b>";
            _hudButtonText.font = GetDefaultFont();
            _hudButtonText.fontSize = 15;
            _hudButtonText.alignment = TextAnchor.MiddleCenter;
            _hudButtonText.color = NauticalUITheme.ParchmentLight;
            _hudButtonText.raycastTarget = false;
        }

        private void BuildModalOverlay(RectTransform parent)
        {
            _modalRootGo = new GameObject("Modal_Backdrop");
            _modalRootGo.transform.SetParent(parent, false);

            RectTransform rootRt = _modalRootGo.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.sizeDelta = Vector2.zero;

            // Semi-transparent dark vignette backdrop that blocks raycasts
            Image dimImg = _modalRootGo.AddComponent<Image>();
            dimImg.color = new Color(0.04f, 0.03f, 0.02f, 0.82f);
            dimImg.raycastTarget = true;

            // Central Cartouche Card (Expanded to 720 x 620 for mobile phones)
            GameObject cardGo = new GameObject("PauseCartouche_Box");
            cardGo.transform.SetParent(_modalRootGo.transform, false);
            RectTransform cardRt = cardGo.AddComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta = new Vector2(720f, 620f);

            Image cardBg = cardGo.AddComponent<Image>();
            cardBg.sprite = NauticalUITheme.MahoganyPanel;
            cardBg.type = Image.Type.Sliced;

            GameObject innerParchment = new GameObject("Inner_Parchment");
            innerParchment.transform.SetParent(cardGo.transform, false);
            RectTransform innerRt = innerParchment.AddComponent<RectTransform>();
            innerRt.anchorMin = Vector2.zero;
            innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(16f, 16f);
            innerRt.offsetMax = new Vector2(-16f, -16f);

            Image innerBg = innerParchment.AddComponent<Image>();
            innerBg.sprite = NauticalUITheme.ParchmentPanel;
            innerBg.type = Image.Type.Sliced;
            innerBg.color = new Color(0.92f, 0.85f, 0.72f);

            // Filigree Compass Rose Header
            GameObject roseGo = new GameObject("Header_CompassRose");
            roseGo.transform.SetParent(innerParchment.transform, false);
            RectTransform roseRt = roseGo.AddComponent<RectTransform>();
            roseRt.anchoredPosition = new Vector2(0f, 245f);
            roseRt.sizeDelta = new Vector2(62f, 62f);
            Image roseImg = roseGo.AddComponent<Image>();
            roseImg.sprite = NauticalUITheme.CompassRose;
            roseImg.color = new Color(0.77f, 0.61f, 0.15f);

            // Title
            GameObject titleGo = new GameObject("Title_Text");
            titleGo.transform.SetParent(innerParchment.transform, false);
            RectTransform titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchoredPosition = new Vector2(0f, 198f);
            titleRt.sizeDelta = new Vector2(560f, 38f);
            Text titleTxt = titleGo.AddComponent<Text>();
            titleTxt.font = GetDefaultFont();
            titleTxt.fontSize = 28;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = new Color(0.18f, 0.12f, 0.08f);
            titleTxt.text = "VOYAGE PAUSED";

            // Ship Identity Subtitle
            GameObject shipGo = new GameObject("Ship_Subtitle");
            shipGo.transform.SetParent(innerParchment.transform, false);
            RectTransform shipRt = shipGo.AddComponent<RectTransform>();
            shipRt.anchoredPosition = new Vector2(0f, 164f);
            shipRt.sizeDelta = new Vector2(560f, 24f);
            _txtShipIdentity = shipGo.AddComponent<Text>();
            _txtShipIdentity.font = GetDefaultFont();
            _txtShipIdentity.fontSize = 15;
            _txtShipIdentity.fontStyle = FontStyle.Italic;
            _txtShipIdentity.alignment = TextAnchor.MiddleCenter;
            _txtShipIdentity.color = new Color(0.38f, 0.30f, 0.22f);
            _txtShipIdentity.text = "FLAGSHIP: THE BARNACLE • CORSAIR BRIGANTINE";

            // Build Sub-Views
            BuildMainPauseSubView(innerParchment.transform);
            BuildSettingsSubView(innerParchment.transform);
        }

        private void BuildMainPauseSubView(Transform parent)
        {
            _mainPausePanel = new GameObject("MainPause_Actions");
            _mainPausePanel.transform.SetParent(parent, false);

            RectTransform rt = _mainPausePanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            float startY = 108f;
            float stepY = 62f;

            // 1. Resume Voyage (Primary Green/Brass Button)
            CreateMenuButton(_mainPausePanel.transform, new Vector2(0f, startY), "RESUME VOYAGE", () => SetPaused(false), 440f, 54f, true);

            // 2. Captain's Primer / Tutorial
            CreateMenuButton(_mainPausePanel.transform, new Vector2(0f, startY - stepY), "CAPTAIN'S PRIMER (TUTORIAL)", () =>
            {
                SetPaused(false);
                TutorialManagerUI.Instance?.OpenPrimerModal();
            }, 440f, 52f);

            // 3. Settings & Fidelity
            CreateMenuButton(_mainPausePanel.transform, new Vector2(0f, startY - stepY * 2), "SETTINGS & FIDELITY", ShowSettingsPanel, 440f, 52f);

            // 4. Restart Voyage
            CreateMenuButton(_mainPausePanel.transform, new Vector2(0f, startY - stepY * 3), "RESTART VOYAGE", HandleRestartVoyageClicked, 440f, 52f);

            // 5. Save & Return to Menu
            CreateMenuButton(_mainPausePanel.transform, new Vector2(0f, startY - stepY * 4), "SAVE & RETURN TO MENU", HandleReturnToMenuClicked, 440f, 54f);
        }

        private void BuildSettingsSubView(Transform parent)
        {
            _settingsSubPanel = new GameObject("Settings_SubView");
            _settingsSubPanel.transform.SetParent(parent, false);

            RectTransform rt = _settingsSubPanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            // Audio Sliders
            _sliderMasterVol = CreateVolumeRow(_settingsSubPanel.transform, new Vector2(0f, 75f), "MASTER VOLUME", out _txtMasterVol, (val) =>
            {
                NauticalAudioManager.Instance?.SetMasterVolume(val);
                if (_txtMasterVol != null) _txtMasterVol.text = $"{Mathf.RoundToInt(val * 100)}%";
            });

            _sliderMusicVol = CreateVolumeRow(_settingsSubPanel.transform, new Vector2(0f, 25f), "SEA CHANTEYS & MUSIC", out _txtMusicVol, (val) =>
            {
                NauticalAudioManager.Instance?.SetMusicVolume(val);
                if (_txtMusicVol != null) _txtMusicVol.text = $"{Mathf.RoundToInt(val * 100)}%";
            });

            _sliderSfxVol = CreateVolumeRow(_settingsSubPanel.transform, new Vector2(0f, -25f), "CANNONS & SURF SFX", out _txtSfxVol, (val) =>
            {
                NauticalAudioManager.Instance?.SetSFXVolume(val);
                if (_txtSfxVol != null) _txtSfxVol.text = $"{Mathf.RoundToInt(val * 100)}%";
            });

            // Graphics Fidelity Label
            GameObject scaleLabelGo = new GameObject("Scale_Label");
            scaleLabelGo.transform.SetParent(_settingsSubPanel.transform, false);
            RectTransform slRect = scaleLabelGo.AddComponent<RectTransform>();
            slRect.anchoredPosition = new Vector2(0f, -75f);
            slRect.sizeDelta = new Vector2(500f, 22f);
            Text slTxt = scaleLabelGo.AddComponent<Text>();
            slTxt.font = GetDefaultFont();
            slTxt.fontSize = 13;
            slTxt.fontStyle = FontStyle.Bold;
            slTxt.alignment = TextAnchor.MiddleCenter;
            slTxt.color = new Color(0.25f, 0.18f, 0.12f);
            slTxt.text = "VISUAL RENDERING RESOLUTION";

            _txtCurrentScale = CreateSpecsText(_settingsSubPanel.transform, new Vector2(0f, -100f), 12, FontStyle.Italic, new Color(0.35f, 0.28f, 0.22f), "CURRENT: 0.50x (CRISP LOW-POLY)");

            // 3 Scale Buttons
            CreateMenuButton(_settingsSubPanel.transform, new Vector2(-160f, -140f), "0.25x RETRO", () => SetRenderScale(0.25f), 140f, 38f);
            CreateMenuButton(_settingsSubPanel.transform, new Vector2(0f, -140f), "0.50x CRISP", () => SetRenderScale(0.50f), 140f, 38f);
            CreateMenuButton(_settingsSubPanel.transform, new Vector2(160f, -140f), "1.00x NATIVE", () => SetRenderScale(1.00f), 140f, 38f);

            // Back to Pause Menu
            CreateMenuButton(_settingsSubPanel.transform, new Vector2(0f, -195f), "BACK TO PAUSE MENU", ShowMainPausePanel, 280f, 42f);
        }

        private void BuildScreenFadeOverlay(Transform parent)
        {
            GameObject fadeGo = new GameObject("PauseScreenFadeOverlay");
            fadeGo.transform.SetParent(parent, false);
            RectTransform fadeRect = fadeGo.AddComponent<RectTransform>();
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = Vector2.zero;
            fadeRect.offsetMax = Vector2.zero;

            _screenFadeOverlay = fadeGo.AddComponent<Image>();
            _screenFadeOverlay.color = new Color(0f, 0f, 0f, 0f);
            _screenFadeOverlay.raycastTarget = false;
        }

        // =====================================================================
        // ACTIONS: RESTART, SAVE & QUIT
        // =====================================================================

        private void HandleRestartVoyageClicked()
        {
            NauticalAudioManager.Instance?.PlayUIClick();
            StartCoroutine(RestartVoyageSequence());
        }

        private IEnumerator RestartVoyageSequence()
        {
            float fadeDuration = 0.5f;
            float elapsed = 0f;

            if (_screenFadeOverlay != null) _screenFadeOverlay.raycastTarget = true;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(elapsed / fadeDuration);
                if (_screenFadeOverlay != null) _screenFadeOverlay.color = new Color(0f, 0f, 0f, a);
                yield return null;
            }

            Time.timeScale = 1.0f;
            IsPaused = false;
            SceneManager.LoadScene(GameplaySceneName);
        }

        private void HandleReturnToMenuClicked()
        {
            NauticalAudioManager.Instance?.PlayUIClick();

            // 1. Auto-save current ship progress
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo == null)
            {
                ShipLocomotionController loco = FindAnyObjectByType<ShipLocomotionController>();
                if (loco != null) playerGo = loco.gameObject;
            }

            if (ShipSaveManager.Instance == null)
            {
                GameObject saveGo = new GameObject("ShipSaveManager");
                saveGo.AddComponent<ShipSaveManager>();
            }

            if (playerGo != null && ShipSaveManager.Instance != null)
            {
                bool saved = ShipSaveManager.Instance.SaveGame(playerGo);
                Debug.Log($"<color=cyan>[PauseMenuUI]</color> Saved game progress before returning to title: {saved} (Path: {ShipSaveManager.SaveFilePath})");
            }
            else
            {
                Debug.LogWarning($"[PauseMenuUI] Could not find player ship to save! (playerGo: {playerGo})");
            }

            StartCoroutine(ReturnToMenuSequence());
        }

        private IEnumerator ReturnToMenuSequence()
        {
            if (NauticalAudioManager.Instance != null)
            {
                NauticalAudioManager.Instance.PlaySFX(SFXType.ShipBell);
            }

            float fadeDuration = 0.7f;
            float elapsed = 0f;

            if (_screenFadeOverlay != null) _screenFadeOverlay.raycastTarget = true;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(elapsed / fadeDuration);
                if (_screenFadeOverlay != null) _screenFadeOverlay.color = new Color(0f, 0f, 0f, a);
                yield return null;
            }

            // CRITICAL: Always unpause Time.timeScale before loading MainMenu
            Time.timeScale = 1.0f;
            IsPaused = false;
            SceneManager.LoadScene(MainMenuSceneName);
        }

        // =====================================================================
        // SETTINGS & VOLUME CONTROLS
        // =====================================================================

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

            float currentScale = PlayerPrefs.GetFloat(PrefRenderScale, 0.5f);
            UpdateRenderScaleLabel(currentScale);
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

            UpdateRenderScaleLabel(scale);
            NauticalAudioManager.Instance?.PlayUIClick();
        }

        private void ApplySavedRenderScale()
        {
            float scale = PlayerPrefs.GetFloat(PrefRenderScale, 0.50f);
            SetRenderScale(scale, false);
        }

        private void UpdateRenderScaleLabel(float scale)
        {
            if (_txtCurrentScale != null)
            {
                if (Mathf.Approximately(scale, 0.25f)) _txtCurrentScale.text = "CURRENT: 0.25x (RETRO PIXEL)";
                else if (Mathf.Approximately(scale, 0.50f)) _txtCurrentScale.text = "CURRENT: 0.50x (CRISP LOW-POLY)";
                else _txtCurrentScale.text = "CURRENT: 1.00x (NATIVE HD)";
            }
        }

        // =====================================================================
        // HELPER BUILDERS: BUTTONS, SLIDERS & LABELS
        // =====================================================================

        private Button CreateMenuButton(Transform parent, Vector2 pos, string label, Action onClick, float width = 360f, float height = 48f, bool isPrimary = false)
        {
            GameObject btnGo = new GameObject($"Btn_{label.Replace(" ", "_")}");
            btnGo.transform.SetParent(parent, false);
            RectTransform rt = btnGo.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, height);

            Image img = btnGo.AddComponent<Image>();
            img.sprite = isPrimary ? NauticalUITheme.ParchmentPanel : NauticalUITheme.BrassPanel;
            img.type = Image.Type.Sliced;
            img.color = isPrimary ? new Color(0.18f, 0.65f, 0.42f) : new Color(0.85f, 0.70f, 0.28f);

            Button btn = btnGo.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.0f, 0.94f, 0.65f);
            cb.pressedColor = new Color(0.65f, 0.52f, 0.18f);
            btn.colors = cb;

            btn.onClick.AddListener(() =>
            {
                NauticalAudioManager.Instance?.PlayUIClick();
                onClick?.Invoke();
            });

            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(btnGo.transform, false);
            RectTransform textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;

            Text txt = textGo.AddComponent<Text>();
            txt.font = GetDefaultFont();
            txt.fontSize = isPrimary ? 20 : 17;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = isPrimary ? Color.white : new Color(0.18f, 0.12f, 0.08f);
            txt.text = label;
            txt.raycastTarget = false;

            return btn;
        }

        private Slider CreateVolumeRow(Transform parent, Vector2 pos, string label, out Text valText, Action<float> onValueChanged)
        {
            GameObject rowGo = new GameObject($"Row_{label.Replace(" ", "_")}");
            rowGo.transform.SetParent(parent, false);
            RectTransform rt = rowGo.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(500f, 36f);

            // Label
            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(rowGo.transform, false);
            RectTransform lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0.5f);
            lrt.anchorMax = new Vector2(0f, 0.5f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.anchoredPosition = Vector2.zero;
            lrt.sizeDelta = new Vector2(170f, 30f);
            Text lt = labelGo.AddComponent<Text>();
            lt.font = GetDefaultFont();
            lt.fontSize = 13;
            lt.fontStyle = FontStyle.Bold;
            lt.alignment = TextAnchor.MiddleLeft;
            lt.color = new Color(0.2f, 0.15f, 0.1f);
            lt.text = label;

            // Slider Background Track
            GameObject sliderGo = new GameObject("Slider");
            sliderGo.transform.SetParent(rowGo.transform, false);
            RectTransform srt = sliderGo.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.36f, 0.5f);
            srt.anchorMax = new Vector2(0.82f, 0.5f);
            srt.pivot = new Vector2(0f, 0.5f);
            srt.anchoredPosition = Vector2.zero;
            srt.sizeDelta = new Vector2(0f, 18f);

            Slider slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;

            GameObject bgGo = new GameObject("Background");
            bgGo.transform.SetParent(sliderGo.transform, false);
            RectTransform bgrt = bgGo.AddComponent<RectTransform>();
            bgrt.anchorMin = Vector2.zero;
            bgrt.anchorMax = Vector2.one;
            bgrt.sizeDelta = Vector2.zero;
            Image bgImg = bgGo.AddComponent<Image>();
            bgImg.sprite = NauticalUITheme.MahoganyPanel;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(0.25f, 0.18f, 0.12f);

            // Fill Area
            GameObject fillAreaGo = new GameObject("Fill Area");
            fillAreaGo.transform.SetParent(sliderGo.transform, false);
            RectTransform fart = fillAreaGo.AddComponent<RectTransform>();
            fart.anchorMin = Vector2.zero;
            fart.anchorMax = Vector2.one;
            fart.sizeDelta = Vector2.zero;

            GameObject fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            RectTransform fillrt = fillGo.AddComponent<RectTransform>();
            fillrt.anchorMin = Vector2.zero;
            fillrt.anchorMax = Vector2.one;
            fillrt.sizeDelta = Vector2.zero;
            Image fillImg = fillGo.AddComponent<Image>();
            fillImg.sprite = NauticalUITheme.BrassPanel;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = new Color(0.85f, 0.68f, 0.22f);
            slider.fillRect = fillrt;

            // Handle
            GameObject handleAreaGo = new GameObject("Handle Slide Area");
            handleAreaGo.transform.SetParent(sliderGo.transform, false);
            RectTransform hart = handleAreaGo.AddComponent<RectTransform>();
            hart.anchorMin = Vector2.zero;
            hart.anchorMax = Vector2.one;
            hart.sizeDelta = Vector2.zero;

            GameObject handleGo = new GameObject("Handle");
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            RectTransform hrt = handleGo.AddComponent<RectTransform>();
            hrt.sizeDelta = new Vector2(20f, 26f);
            Image hImg = handleGo.AddComponent<Image>();
            hImg.sprite = NauticalUITheme.BrassPanel;
            hImg.type = Image.Type.Sliced;
            hImg.color = new Color(0.98f, 0.88f, 0.45f);
            slider.handleRect = hrt;
            slider.targetGraphic = hImg;

            // Value text
            GameObject valGo = new GameObject("ValueText");
            valGo.transform.SetParent(rowGo.transform, false);
            RectTransform vrt = valGo.AddComponent<RectTransform>();
            vrt.anchorMin = new Vector2(0.85f, 0.5f);
            vrt.anchorMax = new Vector2(1f, 0.5f);
            vrt.pivot = new Vector2(0f, 0.5f);
            vrt.anchoredPosition = Vector2.zero;
            vrt.sizeDelta = new Vector2(60f, 30f);
            valText = valGo.AddComponent<Text>();
            valText.font = GetDefaultFont();
            valText.fontSize = 13;
            valText.fontStyle = FontStyle.Bold;
            valText.alignment = TextAnchor.MiddleRight;
            valText.color = new Color(0.2f, 0.15f, 0.1f);
            valText.text = "100%";

            slider.onValueChanged.AddListener((v) => onValueChanged?.Invoke(v));

            return slider;
        }

        private Text CreateSpecsText(Transform parent, Vector2 pos, int fontSize, FontStyle style, Color color, string initialText)
        {
            GameObject go = new GameObject("Specs_Text");
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(480f, 26f);

            Text txt = go.AddComponent<Text>();
            txt.font = GetDefaultFont();
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
            txt.text = initialText;
            return txt;
        }
    }
}
