using System;
using System.Collections;
using Plunderlust.Audio;
using Plunderlust.Cargo;
using Plunderlust.Core.Persistence;
using Plunderlust.Ship;
using Plunderlust.Subsystems;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Plunderlust.UI
{
    /// <summary>
    /// Dedicated 17th-Century Nautical Game Over Modal & Voyage Sinking Screen.
    /// Triggers when the player flagship founders or is destroyed at sea.
    /// Features:
    /// 1. Cinematic delayed entry: Player witnesses their vessel roll over and plunge into the ocean depths.
    /// 2. Screen-space parchment & brass cartouche matching Plunderlust aesthetic:
    ///    - Ship epitaph & custom christened vessel name
    ///    - Demise cause and plunder lost telemetry
    ///    - Primary button: RETRY VOYAGE (restores to last harbor save or restarts voyage fresh)
    ///    - Secondary button: RETURN TO MENU (loads MainMenu scene with clean unpause)
    /// 3. Zero heap allocations during update loops, fully mobile touch friendly.
    /// </summary>
    [DefaultExecutionOrder(20)]
    public class GameOverUI : MonoBehaviour
    {
        public static GameOverUI Instance { get; private set; }
        public static bool IsGameOver { get; private set; }

        private const string MainMenuSceneName = "MainMenu";
        private const string GameplaySceneName = "SampleScene";

        private Canvas _canvas;
        private CanvasScaler _scaler;
        private Image _screenFadeOverlay;
        private GameObject _modalRootGo;
        private RectTransform _modalRect;

        // UI Labels
        private Text _txtTitle;
        private Text _txtEpitaph;
        private Text _txtDemiseDetails;
        private Text _txtLostPlunder;

        // Interactive Buttons
        private Button _btnRetry;
        private Button _btnReturnToMenu;

        // State Tracking
        private ShipSubsystemController _playerSubsystem;
        private bool _hasTriggered = false;
        private bool _isTransitioning = false;
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
            IsGameOver = false;

            BuildGameOverCanvas();
        }

        private void Start()
        {
            HookPlayerSubsystems();
        }

        private void OnDestroy()
        {
            if (IsGameOver)
            {
                Time.timeScale = 1.0f;
                IsGameOver = false;
            }

            UnhookPlayerSubsystems();
        }

        private void Update()
        {
            // If player subsystem wasn't available at Start, continuously look for it until hooked
            if (_playerSubsystem == null && !_hasTriggered)
            {
                HookPlayerSubsystems();
            }

            // Fallback safety check: If player ship is sinking or dead and sequence hasn't started
            if (_playerSubsystem != null && !_hasTriggered)
            {
                if (_playerSubsystem.IsSinking || _playerSubsystem.IsDead)
                {
                    TriggerGameOverSequence();
                }
            }

            // Keyboard navigation if modal is active
            if (IsGameOver && !_isTransitioning)
            {
                if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                {
                    HandleRetryClicked();
                }
                else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.M))
                {
                    HandleReturnToMenuClicked();
                }
            }
        }

        private void HookPlayerSubsystems()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                var loco = FindAnyObjectByType<ShipLocomotionController>();
                if (loco != null) player = loco.gameObject;
            }

            if (player != null)
            {
                _playerSubsystem = player.GetComponent<ShipSubsystemController>();
                if (_playerSubsystem != null)
                {
                    _playerSubsystem.OnShipStartedSinking -= HandleShipSinking;
                    _playerSubsystem.OnShipStartedSinking += HandleShipSinking;
                    _playerSubsystem.OnShipDestroyed -= HandleShipDestroyed;
                    _playerSubsystem.OnShipDestroyed += HandleShipDestroyed;
                }
            }
        }

        private void UnhookPlayerSubsystems()
        {
            if (_playerSubsystem != null)
            {
                _playerSubsystem.OnShipStartedSinking -= HandleShipSinking;
                _playerSubsystem.OnShipDestroyed -= HandleShipDestroyed;
            }
        }

        private void HandleShipSinking()
        {
            TriggerGameOverSequence();
        }

        private void HandleShipDestroyed()
        {
            TriggerGameOverSequence();
        }

        public void TriggerGameOverSequence()
        {
            if (_hasTriggered) return;
            _hasTriggered = true;

            StartCoroutine(GameOverCinematicRoutine());
        }

        private IEnumerator GameOverCinematicRoutine()
        {
            // 1. Dramatic Pause: Allow player to watch their flagship heel over and take on water (~2.2s)
            float delay = 2.2f;
            float elapsed = 0f;
            while (elapsed < delay)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            IsGameOver = true;

            // 2. Play haunting ship bell toll & distress wood groan
            NauticalAudioManager.Instance?.PlaySFX(SFXType.ShipBell);
            NauticalAudioManager.Instance?.PlaySFX(SFXType.WoodCreak);

            // 3. Populate Epitaph & Plunder Info
            PopulateGameOverTelemetry();

            // 4. Fade in deep oceanic vignette and scale up modal
            _modalRootGo.SetActive(true);
            if (_screenFadeOverlay != null) _screenFadeOverlay.raycastTarget = true;

            float fadeDuration = 1.0f;
            elapsed = 0f;

            Vector3 startScale = new Vector3(0.85f, 0.85f, 1.0f);
            Vector3 endScale = Vector3.one;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                if (_screenFadeOverlay != null)
                {
                    _screenFadeOverlay.color = new Color(0.04f, 0.07f, 0.11f, smoothT * 0.94f);
                }

                if (_modalRect != null)
                {
                    _modalRect.localScale = Vector3.Lerp(startScale, endScale, smoothT);
                }

                yield return null;
            }

            // 5. Freeze game world once modal is in full view
            Time.timeScale = 0.0f;
        }

        private void PopulateGameOverTelemetry()
        {
            string shipName = "The Flagship";
            int goldCount = 0;
            int cargoCount = 0;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var identity = player.GetComponent<ShipIdentitySystem>();
                if (identity != null && !string.IsNullOrEmpty(identity.ChristenedName))
                {
                    shipName = identity.ChristenedName;
                }

                var cargoHold = player.GetComponent<ShipCargoHold>();
                if (cargoHold != null)
                {
                    goldCount = cargoHold.CurrentGold;
                    cargoCount = cargoHold.TotalCargoUnits;
                }
            }

            if (_txtEpitaph != null)
            {
                _txtEpitaph.text = $"The gallant crew of <color=#FFD700><b>{shipName}</b></color> has succumbed to the merciless deep.\nHer timbers rest forever in Davy Jones' locker.";
            }

            if (_txtDemiseDetails != null)
            {
                _txtDemiseDetails.text = "<color=#C59B27>FATAL CASUALTY:</color> Catastrophic Hull Breach & Bilge Flooding";
            }

            if (_txtLostPlunder != null)
            {
                _txtLostPlunder.text = $"<color=#C59B27>PLUNDER LOST TO THE OCEAN:</color> <b>{goldCount}</b> Gold Doubloons | <b>{cargoCount}</b> Cargo Crates";
            }
        }

        private void HandleRetryClicked()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;

            NauticalAudioManager.Instance?.PlayUIClick();
            NauticalAudioManager.Instance?.PlaySFX(SFXType.ShipBell);

            StartCoroutine(TransitionToSceneRoutine(GameplaySceneName));
        }

        private void HandleReturnToMenuClicked()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;

            NauticalAudioManager.Instance?.PlayUIClick();

            StartCoroutine(TransitionToSceneRoutine(MainMenuSceneName));
        }

        private IEnumerator TransitionToSceneRoutine(string sceneName)
        {
            float fadeDuration = 0.6f;
            float elapsed = 0f;

            Color startCol = (_screenFadeOverlay != null) ? _screenFadeOverlay.color : Color.clear;
            Color endCol = Color.black;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                if (_screenFadeOverlay != null)
                {
                    _screenFadeOverlay.color = Color.Lerp(startCol, endCol, t);
                }
                yield return null;
            }

            // CRITICAL: Always restore normal timeScale before scene transition
            Time.timeScale = 1.0f;
            IsGameOver = false;
            SceneManager.LoadScene(sceneName);
        }

        // =====================================================================
        // PROCEDURAL CANVAS & MODAL BUILDER
        // =====================================================================

        private void BuildGameOverCanvas()
        {
            // 1. Root Canvas
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 700; // Above PauseMenu (600) and general HUD (50-90)

            _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1600, 900);
            _scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            // 2. Fullscreen Vignette / Dark Sea Dimmer
            GameObject fadeGo = new GameObject("ScreenDimmerOverlay");
            fadeGo.transform.SetParent(transform, false);
            RectTransform fadeRt = fadeGo.AddComponent<RectTransform>();
            fadeRt.anchorMin = Vector2.zero;
            fadeRt.anchorMax = Vector2.one;
            fadeRt.sizeDelta = Vector2.zero;

            _screenFadeOverlay = fadeGo.AddComponent<Image>();
            _screenFadeOverlay.sprite = NauticalUITheme.SolidWhite;
            _screenFadeOverlay.color = new Color(0.04f, 0.07f, 0.11f, 0f);
            _screenFadeOverlay.raycastTarget = false;

            // 3. Central Modal Root (Expanded to 720 x 590 for mobile phones)
            _modalRootGo = new GameObject("GameOverModalCartouche");
            _modalRootGo.transform.SetParent(transform, false);
            _modalRect = _modalRootGo.AddComponent<RectTransform>();
            _modalRect.anchorMin = new Vector2(0.5f, 0.5f);
            _modalRect.anchorMax = new Vector2(0.5f, 0.5f);
            _modalRect.sizeDelta = new Vector2(720f, 590f);
            _modalRect.anchoredPosition = Vector2.zero;

            // 4. Background Panel: Dark Mahogany Frame + Brass Border Inset
            Image panelImg = _modalRootGo.AddComponent<Image>();
            panelImg.sprite = NauticalUITheme.MahoganyPanel;
            panelImg.type = Image.Type.Sliced;
            panelImg.color = Color.white;

            // Brass Inset Border
            GameObject borderGo = new GameObject("BrassInsetBorder");
            borderGo.transform.SetParent(_modalRootGo.transform, false);
            RectTransform borderRt = borderGo.AddComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.sizeDelta = new Vector2(-16f, -16f);
            Image borderImg = borderGo.AddComponent<Image>();
            borderImg.sprite = NauticalUITheme.BrassBorderBox;
            borderImg.type = Image.Type.Sliced;
            borderImg.color = new Color(0.95f, 0.82f, 0.35f, 0.95f);
            borderImg.raycastTarget = false;

            // 5. Header Bar (Blood-Crimson / Gold)
            GameObject headerGo = new GameObject("HeaderBanner");
            headerGo.transform.SetParent(_modalRootGo.transform, false);
            RectTransform headerRt = headerGo.AddComponent<RectTransform>();
            headerRt.anchoredPosition = new Vector2(0f, 242f);
            headerRt.sizeDelta = new Vector2(670f, 66f);
            Image headerImg = headerGo.AddComponent<Image>();
            headerImg.sprite = NauticalUITheme.BrassPanel;
            headerImg.type = Image.Type.Sliced;
            headerImg.color = new Color(0.68f, 0.12f, 0.12f, 0.95f); // Crimson blood
            headerImg.raycastTarget = false;

            // Header Title Text
            GameObject titleGo = new GameObject("TitleText");
            titleGo.transform.SetParent(headerGo.transform, false);
            RectTransform titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = Vector2.zero;
            titleRt.anchorMax = Vector2.one;
            titleRt.sizeDelta = Vector2.zero;
            _txtTitle = titleGo.AddComponent<Text>();
            _txtTitle.font = GetDefaultFont();
            _txtTitle.fontSize = 28;
            _txtTitle.fontStyle = FontStyle.Bold;
            _txtTitle.alignment = TextAnchor.MiddleCenter;
            _txtTitle.color = NauticalUITheme.GoldAccent;
            _txtTitle.text = "VOYAGE LOST TO THE DEPTHS";

            // Title Shadow
            var titleShadow = titleGo.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            titleShadow.effectDistance = new Vector2(2f, -2f);

            // 6. Skull / Crossed Arms Nautical Seal Icon
            GameObject sealGo = new GameObject("CrestSeal");
            sealGo.transform.SetParent(_modalRootGo.transform, false);
            RectTransform sealRt = sealGo.AddComponent<RectTransform>();
            sealRt.anchoredPosition = new Vector2(0f, 165f);
            sealRt.sizeDelta = new Vector2(72f, 72f);
            Image sealImg = sealGo.AddComponent<Image>();
            sealImg.sprite = NauticalUITheme.CrossedYardarms;
            sealImg.color = new Color(0.95f, 0.82f, 0.35f, 0.95f);
            sealImg.raycastTarget = false;

            // 7. Ship Epitaph Text
            GameObject epitaphGo = new GameObject("EpitaphText");
            epitaphGo.transform.SetParent(_modalRootGo.transform, false);
            RectTransform epitaphRt = epitaphGo.AddComponent<RectTransform>();
            epitaphRt.anchoredPosition = new Vector2(0f, 92f);
            epitaphRt.sizeDelta = new Vector2(620f, 66f);
            _txtEpitaph = epitaphGo.AddComponent<Text>();
            _txtEpitaph.font = GetDefaultFont();
            _txtEpitaph.fontSize = 19;
            _txtEpitaph.fontStyle = FontStyle.Italic;
            _txtEpitaph.alignment = TextAnchor.MiddleCenter;
            _txtEpitaph.lineSpacing = 1.15f;
            _txtEpitaph.color = new Color(0.92f, 0.87f, 0.78f, 1.0f);
            _txtEpitaph.text = "The gallant crew has succumbed to the perilous deep.\nHer timbers rest forever in Davy Jones' locker.";

            // 8. Demise Details Text
            GameObject demiseGo = new GameObject("DemiseDetailsText");
            demiseGo.transform.SetParent(_modalRootGo.transform, false);
            RectTransform demiseRt = demiseGo.AddComponent<RectTransform>();
            demiseRt.anchoredPosition = new Vector2(0f, 30f);
            demiseRt.sizeDelta = new Vector2(670f, 32f);
            _txtDemiseDetails = demiseGo.AddComponent<Text>();
            _txtDemiseDetails.font = GetDefaultFont();
            _txtDemiseDetails.fontSize = 15;
            _txtDemiseDetails.alignment = TextAnchor.MiddleCenter;
            _txtDemiseDetails.color = new Color(0.92f, 0.78f, 0.35f, 1.0f);
            _txtDemiseDetails.text = "FATAL BREACH: Ship Planking Crushed";

            // 9. Lost Plunder Text
            GameObject plunderGo = new GameObject("LostPlunderText");
            plunderGo.transform.SetParent(_modalRootGo.transform, false);
            RectTransform plunderRt = plunderGo.AddComponent<RectTransform>();
            plunderRt.anchoredPosition = new Vector2(0f, -8f);
            plunderRt.sizeDelta = new Vector2(670f, 32f);
            _txtLostPlunder = plunderGo.AddComponent<Text>();
            _txtLostPlunder.font = GetDefaultFont();
            _txtLostPlunder.fontSize = 15;
            _txtLostPlunder.alignment = TextAnchor.MiddleCenter;
            _txtLostPlunder.color = new Color(0.88f, 0.84f, 0.76f, 1.0f);
            _txtLostPlunder.text = "PLUNDER LOST: 0 Gold Doubloons | 0 Cargo";

            // 10. Horizontal Brass Divider Line
            GameObject divGo = new GameObject("BrassDivider");
            divGo.transform.SetParent(_modalRootGo.transform, false);
            RectTransform divRt = divGo.AddComponent<RectTransform>();
            divRt.anchoredPosition = new Vector2(0f, -44f);
            divRt.sizeDelta = new Vector2(600f, 2f);
            Image divImg = divGo.AddComponent<Image>();
            divImg.sprite = NauticalUITheme.SolidWhite;
            divImg.color = new Color(0.77f, 0.61f, 0.15f, 0.60f);
            divImg.raycastTarget = false;

            // 11. Buttons Container (Enlarged to 460x56 for mobile fingers)
            _btnRetry = CreateButton(_modalRootGo.transform, new Vector2(0f, -105f), "RETRY VOYAGE", HandleRetryClicked, isPrimary: true);
            _btnReturnToMenu = CreateButton(_modalRootGo.transform, new Vector2(0f, -175f), "RETURN TO MENU", HandleReturnToMenuClicked, isPrimary: false);

            // Initially hidden
            _modalRootGo.SetActive(false);
        }

        private Button CreateButton(Transform parent, Vector2 pos, string label, Action onClick, bool isPrimary)
        {
            GameObject btnGo = new GameObject($"Btn_{label.Replace(" ", "_")}");
            btnGo.transform.SetParent(parent, false);
            RectTransform rt = btnGo.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(460f, 56f);

            Image img = btnGo.AddComponent<Image>();
            img.sprite = isPrimary ? NauticalUITheme.BrassPanel : NauticalUITheme.MahoganyPanel;
            img.type = Image.Type.Sliced;
            img.color = isPrimary ? new Color(0.18f, 0.65f, 0.42f) : new Color(0.24f, 0.17f, 0.11f);

            Button btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;

            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            cb.selectedColor = Color.white;
            btn.colors = cb;

            btn.onClick.AddListener(() => onClick?.Invoke());

            // Button Label Text
            GameObject txtGo = new GameObject("Label");
            txtGo.transform.SetParent(btnGo.transform, false);
            RectTransform txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;

            Text txt = txtGo.AddComponent<Text>();
            txt.font = GetDefaultFont();
            txt.fontSize = 22;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = isPrimary ? Color.white : new Color(0.95f, 0.82f, 0.35f);
            txt.text = label;

            var shadow = txtGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            return btn;
        }
    }
}
