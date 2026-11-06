using System;
using System.Collections;
using Plunderlust.Audio;
using Plunderlust.Combat;
using Plunderlust.Core.Persistence;
using Plunderlust.Ship;
using UnityEngine;
using UnityEngine.UI;

namespace Plunderlust.UI
{
    /// <summary>
    /// Master 17th-Century Nautical Tutorial & Captain's Primer System.
    /// Automatically presents when starting a new game / voyage.
    /// Features:
    /// 1. The Captain's Primer: A 4-chapter authentic parchment codex explaining:
    ///    - Chapter 1: Helm & Canvas (touch wheel & throttle, keyboard fallback, turning radius)
    ///    - Chapter 2: Wind & Tacking (wind bearing indicator, running vs in irons, tacking maneuvers)
    ///    - Chapter 3: Cannon Batteries & Hull Integrity (broadside volleys, localized subsystem damage, bilge flooding & repairs)
    ///    - Chapter 4: Harbors, Shipyard Tiers & Treasure (port mooring, 6 ship tiers, sea chart navigation, flotsam & atoll excavation)
    /// 2. Interactive In-Game HUD Orders Banner: Step-by-step onboarding tracker (Hoist Canvas -> Steer Helm -> Test Cannons -> Open Sea Chart).
    /// 3. Re-accessible at any time from the in-game Pause Menu.
    /// </summary>
    [DefaultExecutionOrder(18)]
    public class TutorialManagerUI : MonoBehaviour
    {
        public static TutorialManagerUI Instance { get; private set; }

        public const string PrefTutorialCompleted = "Plunderlust_TutorialCompleted";
        public const string PrefResumeSaved = "Plunderlust_ResumeSavedGame";

        private Canvas _canvas;
        private CanvasScaler _scaler;

        // Modal Elements
        private GameObject _modalBackdropGo;
        private RectTransform _cardRect;
        private Image _dimOverlayImg;

        // Page Components
        private Text _txtPageIndicator;
        private Text _txtChapterTitle;
        private Text _txtChapterSubtitle;
        private Text _txtContentBody;
        private Text _txtProTip;
        private Image _imgChapterIcon;

        // Navigation Buttons
        private Button _btnPrev;
        private Button _btnNext;
        private Button _btnSkip;
        private Text _txtNextBtnLabel;
        private Button[] _tabButtons = new Button[4];
        private Image[] _tabImages = new Image[4];
        private Text[] _tabTexts = new Text[4];

        // Active In-Game Orders Banner
        private GameObject _ordersBannerGo;
        private RectTransform _ordersBannerRt;
        private GameObject _ordersBodyGo;
        private Text _txtOrderStep1;
        private Text _txtOrderStep2;
        private Text _txtOrderStep3;
        private Text _txtOrderStep4;
        private Button _btnToggleOrders;
        private Text _txtToggleOrdersLabel;
        private bool _isOrdersCollapsed = false;

        // State Tracking
        private int _currentPage = 0; // 0 to 3
        private bool _isModalOpen = false;
        private bool _hasStartedOrders = false;
        private bool _step1Done = false; // Hoist Canvas
        private bool _step2Done = false; // Steer Helm
        private bool _step3Done = false; // Fire Cannon
        private bool _step4Done = false; // Open Sea Chart

        private ShipLocomotionController _playerLocomotion;
        private NavalCombatController _playerCombat;
        private float _initialHeading = 0f;
        private static Font _cachedFont;

        public static Font GetDefaultFont()
        {
            if (_cachedFont == null)
            {
                _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

            BuildTutorialCanvas();
        }

        private void Start()
        {
            HookPlayerReferences();

            // Evaluate whether to trigger automatically on new game
            if (ShouldShowTutorialOnStart())
            {
                StartCoroutine(DelayedTutorialPromptRoutine());
            }
        }

        public static bool ShouldShowTutorialOnStart()
        {
            // If player chose "New Voyage" from main menu
            int resumeGame = PlayerPrefs.GetInt(PrefResumeSaved, 0);
            if (resumeGame == 0) return true;

            // If tutorial was never completed
            int completed = PlayerPrefs.GetInt(PrefTutorialCompleted, 0);
            if (completed == 0) return true;

            // If no save file exists
            if (!ShipSaveManager.SaveExists()) return true;

            return false;
        }

        private void HookPlayerReferences()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                var loco = FindAnyObjectByType<ShipLocomotionController>();
                if (loco != null) player = loco.gameObject;
            }

            if (player != null)
            {
                _playerLocomotion = player.GetComponent<ShipLocomotionController>();
                _playerCombat = player.GetComponent<NavalCombatController>();
                _initialHeading = player.transform.eulerAngles.y;

                if (_playerCombat != null)
                {
                    _playerCombat.OnWeaponFired -= HandleWeaponFired;
                    _playerCombat.OnWeaponFired += HandleWeaponFired;
                }
            }
        }

        private void OnDestroy()
        {
            if (_playerCombat != null)
            {
                _playerCombat.OnWeaponFired -= HandleWeaponFired;
            }
        }

        private void HandleWeaponFired()
        {
            CompleteStep3();
        }

        public void CompleteStep3()
        {
            if (!_step3Done)
            {
                _step3Done = true;
                NauticalAudioManager.Instance?.PlaySFX(SFXType.ShipBell);
                UpdateOrdersDisplay();
            }
        }

        private IEnumerator DelayedTutorialPromptRoutine()
        {
            // Wait 1.0 second for water, ship, and camera to settle
            yield return new WaitForSeconds(1.0f);

            // Don't show if game over is already active
            if (GameOverUI.IsGameOver) yield break;

            OpenPrimerModal();
        }

        private void Update()
        {
            // Re-hook player if needed
            if (_playerLocomotion == null)
            {
                HookPlayerReferences();
            }

            // Keyboard navigation while primer modal is open
            if (_isModalOpen)
            {
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.Space))
                {
                    HandleNextClicked();
                }
                else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                {
                    HandlePrevClicked();
                }
                else if (Input.GetKeyDown(KeyCode.Escape))
                {
                    ClosePrimerModal();
                }
                return;
            }

            // Hide the orders banner while full-screen modals are open (Sea Chart, Pause Menu, Game Over, Primer)
            bool modalBlocking = (SeaChartMapUI.Instance != null && SeaChartMapUI.Instance.IsOpen)
                || (PauseMenuUI.Instance != null && PauseMenuUI.IsPaused)
                || GameOverUI.IsGameOver
                || _isModalOpen;

            if (_ordersBannerGo != null)
            {
                bool shouldBeActive = !modalBlocking && _hasStartedOrders && !_step4Done;
                if (_ordersBannerGo.activeSelf != shouldBeActive)
                {
                    _ordersBannerGo.SetActive(shouldBeActive);
                }
            }

            // Track live in-game orders progress
            if (_hasStartedOrders && !_step4Done)
            {
                TrackInGameOrdersProgress();
            }
        }

        // =====================================================================
        // MODAL OPEN / CLOSE & STEP NAVIGATION
        // =====================================================================

        public void OpenPrimerModal()
        {
            _isModalOpen = true;
            _modalBackdropGo.SetActive(true);

            // Pause game engine during tutorial reading
            Time.timeScale = 0.0f;

            NauticalAudioManager.Instance?.PlaySFX(SFXType.ShipBell);
            ShowPage(0);
        }

        public void ClosePrimerModal()
        {
            _isModalOpen = false;
            _modalBackdropGo.SetActive(false);

            // Resume game engine
            Time.timeScale = 1.0f;

            // Mark tutorial completed in preferences
            PlayerPrefs.SetInt(PrefTutorialCompleted, 1);
            PlayerPrefs.SetInt(PrefResumeSaved, 1);
            PlayerPrefs.Save();

            NauticalAudioManager.Instance?.PlayUIClick();

            // Start In-Game Orders Banner guidance
            if (!_hasStartedOrders)
            {
                StartInGameOrders();
            }
        }

        public void ShowPage(int pageIndex)
        {
            _currentPage = Mathf.Clamp(pageIndex, 0, 3);

            // Update Tabs Styling
            for (int i = 0; i < 4; i++)
            {
                bool isActive = (i == _currentPage);
                if (_tabImages[i] != null)
                {
                    _tabImages[i].color = isActive ? new Color(0.20f, 0.13f, 0.08f, 1.0f) : new Color(0.85f, 0.76f, 0.58f, 0.90f);
                }
                if (_tabTexts[i] != null)
                {
                    _tabTexts[i].color = isActive ? new Color(1.0f, 0.84f, 0.32f) : new Color(0.26f, 0.18f, 0.10f);
                }
            }

            // Update Page Indicator
            if (_txtPageIndicator != null)
            {
                _txtPageIndicator.text = $"CHAPTER {_currentPage + 1} OF 4";
            }

            // Populate Chapter Contents
            switch (_currentPage)
            {
                case 0:
                    PopulateChapter1();
                    break;
                case 1:
                    PopulateChapter2();
                    break;
                case 2:
                    PopulateChapter3();
                    break;
                case 3:
                    PopulateChapter4();
                    break;
            }

            // Update Navigation Buttons
            if (_btnPrev != null) _btnPrev.interactable = (_currentPage > 0);
            if (_txtNextBtnLabel != null)
            {
                _txtNextBtnLabel.text = (_currentPage == 3) ? "SET SAIL, CAPTAIN!" : "NEXT CHAPTER ▶";
            }
        }

        private void HandleNextClicked()
        {
            NauticalAudioManager.Instance?.PlayUIClick();

            if (_currentPage < 3)
            {
                ShowPage(_currentPage + 1);
            }
            else
            {
                ClosePrimerModal();
            }
        }

        private void HandlePrevClicked()
        {
            NauticalAudioManager.Instance?.PlayUIClick();

            if (_currentPage > 0)
            {
                ShowPage(_currentPage - 1);
            }
        }

        // =====================================================================
        // CHAPTER DATA POPULATORS
        // =====================================================================

        private void PopulateChapter1()
        {
            if (_imgChapterIcon != null) _imgChapterIcon.sprite = NauticalUITheme.RudderStern;
            if (_txtChapterTitle != null) _txtChapterTitle.text = "HELM, RUDDER & PROPULSION";
            if (_txtChapterSubtitle != null) _txtChapterSubtitle.text = "Steering the rudder and commanding your canvas sails.";

            if (_txtContentBody != null)
            {
                _txtContentBody.text =
                    "<b>• STEERING THE RUDDER:</b>\n" +
                    "  - <i>Mobile Touch:</i> Drag the Virtual Steering Wheel (lower-left) Port or Starboard.\n" +
                    "  - <i>Keyboard:</i> Press <b>A / D</b> or <b>Left / Right Arrows</b> to turn the helm.\n\n" +
                    "<b>• THROTTLE & CANVAS LEVELS:</b>\n" +
                    "  - <i>Mobile Touch:</i> Shift the vertical notched Throttle Lever on the lower-left.\n" +
                    "  - <i>Keyboard:</i> Press <b>W</b> to raise canvas (Full speed) or <b>S</b> to strike canvas (Stop).\n" +
                    "  - <b>Anchored:</b> Drops iron anchor to hold position.\n" +
                    "  - <b>Battle Sails:</b> Half canvas for combat maneuvering.\n" +
                    "  - <b>Full Canvas:</b> Maximum forward knots for open ocean cruising.";
            }

            if (_txtProTip != null)
            {
                _txtProTip.text = "<color=#8B2500><b>NAVAL DOCTRINE:</b></color> Striking canvas to <i>Battle Sails</i> allows your vessel to pivot with a dramatically tighter turning radius during broadside dogfights!";
            }
        }

        private void PopulateChapter2()
        {
            if (_imgChapterIcon != null) _imgChapterIcon.sprite = NauticalUITheme.CompassRose;
            if (_txtChapterTitle != null) _txtChapterTitle.text = "WIND BEARING & TACKING";
            if (_txtChapterSubtitle != null) _txtChapterSubtitle.text = "Harnessing ocean breezes and navigating against adverse gales.";

            if (_txtContentBody != null)
            {
                _txtContentBody.text =
                    "<b>• THE WIND INDICATOR:</b>\n" +
                    "  - Watch the golden arrow on your compass rose at the top-right of your screen.\n" +
                    "  - It continuously reveals the live atmospheric wind heading across the archipelago.\n\n" +
                    "<b>• SAILING WITH THE WIND:</b>\n" +
                    "  - Sailing <i>Downwind</i> (running) or <i>Across the Wind</i> (reaching) bills your sails\n" +
                    "    and propels your flagship to top velocity.\n\n" +
                    "<b>• SAILING INTO THE WIND (IN IRONS):</b>\n" +
                    "  - Pointing your bow directly upwind causes your canvas to luff and speed drops to near zero.\n" +
                    "  - <b>Tacking:</b> Zig-zag back and forth at a 45-degree angle across the wind to make headway against oncoming head-gales!";
            }

            if (_txtProTip != null)
            {
                _txtProTip.text = "<color=#8B2500><b>NAVAL DOCTRINE:</b></color> Keep your sails filled! An enemy catching the weather gauge (upwind of you) commands superior speed and firing angles.";
            }
        }

        private void PopulateChapter3()
        {
            if (_imgChapterIcon != null) _imgChapterIcon.sprite = NauticalUITheme.CrossedYardarms;
            if (_txtChapterTitle != null) _txtChapterTitle.text = "CANNON BATTERIES & DAMAGE CONTROL";
            if (_txtChapterSubtitle != null) _txtChapterSubtitle.text = "Delivering broadsides, localized damage, and pumping bilges.";

            if (_txtContentBody != null)
            {
                _txtContentBody.text =
                    "<b>• FIRING BROADSIDES:</b>\n" +
                    "  - <i>Mobile Touch:</i> Tap the dedicated Port or Starboard Cannon triggers on the lower-right.\n" +
                    "  - <i>Keyboard:</i> Press <b>Q</b> (Port fire), <b>E</b> (Starboard fire), or <b>Spacebar</b>.\n\n" +
                    "<b>• LOCALIZED SUBSYSTEM DAMAGE:</b>\n" +
                    "  - <b>Hull Planking:</b> Waterline strikes shatter oak timbers and flood your bilge!\n" +
                    "  - <b>Sails & Rigging:</b> High hits shred canvas, severely diminishing propulsion.\n" +
                    "  - <b>Rudder & Helm:</b> Stern hits wreck steering, locking your ship on course.\n\n" +
                    "<b>• BILGE FLOODING & REPAIRS:</b>\n" +
                    "  - If bilge flooding reaches 100%, your ship founders and plunges into Davy Jones' locker!\n" +
                    "  - Tap the <b>REPAIR</b> button to pump bilges and deploy emergency hull patches.";
            }

            if (_txtProTip != null)
            {
                _txtProTip.text = "<color=#8B2500><b>NAVAL DOCTRINE:</b></color> Monitor your Paper-Doll status widget on the left! Never let flooding exceed 75% without deploying your repair crew.";
            }
        }

        private void PopulateChapter4()
        {
            if (_imgChapterIcon != null) _imgChapterIcon.sprite = NauticalUITheme.CircleWaxSeal;
            if (_txtChapterTitle != null) _txtChapterTitle.text = "HARBORS, SHIPYARD TIERS & TREASURE";
            if (_txtChapterSubtitle != null) _txtChapterSubtitle.text = "Port Royal Haven, Smuggler's Cove, upgrading hulls & plundering.";

            if (_txtContentBody != null)
            {
                _txtContentBody.text =
                    "<b>• MOORING AT HARBORS:</b>\n" +
                    "  - Sail within 35 meters of <b>Port Royal Haven</b> (North) or <b>Smuggler's Cove</b> (South).\n" +
                    "  - An anchor prompt will appear. Tap to moor your vessel in safe waters.\n\n" +
                    "<b>• SHIPYARD UPGRADES (6 TIERS):</b>\n" +
                    "  - Accumulate doubloons to commission larger hulls at the Shipyard: from the starter Rowboat\n" +
                    "    up to Coastal Cutters, War Sloops, Pirate Brigantines, and towering Royal Galleons!\n\n" +
                    "<b>• NAVIGATION & SALVAGE:</b>\n" +
                    "  - Tap the <b>SEA CHART</b> button (or press <b>M</b>) to survey uncharted islands and ports.\n" +
                    "  - Sail near floating cargo crates to scoop up sugar, spices, and rum.\n" +
                    "  - Unearth buried treasure chests marked on atolls across the archipelago!";
            }

            if (_txtProTip != null)
            {
                _txtProTip.text = "<color=#8B2500><b>NAVAL DOCTRINE:</b></color> Mooring at a harbor automatically saves your voyage progress to disk so your fleet and doubloons are always preserved!";
            }
        }

        // =====================================================================
        // ACTIVE IN-GAME ORDERS HUD TRACKER
        // =====================================================================

        private void StartInGameOrders()
        {
            _hasStartedOrders = true;
            _ordersBannerGo.SetActive(true);
            UpdateOrdersDisplay();
        }

        private void TrackInGameOrdersProgress()
        {
            // Step 1: Hoist Canvas (Speed > 2.5 knots)
            if (!_step1Done && _playerLocomotion != null)
            {
                if (_playerLocomotion.CurrentSpeedKnots > 2.5f)
                {
                    _step1Done = true;
                    NauticalAudioManager.Instance?.PlaySFX(SFXType.ShipBell);
                    UpdateOrdersDisplay();
                }
            }

            // Step 2: Steer with Rudder (Turned at least 18 degrees)
            if (_step1Done && !_step2Done && _playerLocomotion != null)
            {
                float currentHeading = _playerLocomotion.transform.eulerAngles.y;
                float angleDiff = Mathf.Abs(Mathf.DeltaAngle(_initialHeading, currentHeading));
                if (angleDiff > 18.0f)
                {
                    _step2Done = true;
                    NauticalAudioManager.Instance?.PlaySFX(SFXType.ShipBell);
                    UpdateOrdersDisplay();
                }
            }

            // Step 3: Test Cannons (Fired projectile)
            if (_step2Done && !_step3Done)
            {
                // Detects keyboard triggers, reload timers being active after firing, or direct weapon fire
                bool firedKeyboard = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.E);
                bool reloadActive = _playerCombat != null && (_playerCombat.ForwardReloadNormalized > 0.001f || _playerCombat.PortReloadNormalized > 0.001f || _playerCombat.StarboardReloadNormalized > 0.001f);
                if (firedKeyboard || reloadActive)
                {
                    CompleteStep3();
                }
            }

            // Step 4: Open Sea Chart
            if (_step3Done && !_step4Done)
            {
                if (Input.GetKeyDown(KeyCode.M) || (SeaChartMapUI.Instance != null && SeaChartMapUI.Instance.IsOpen))
                {
                    _step4Done = true;
                    NauticalAudioManager.Instance?.PlaySFX(SFXType.ShipBell);
                    UpdateOrdersDisplay();

                    // All done! Auto-hide banner after 5 seconds
                    StartCoroutine(DismissOrdersBannerRoutine());
                }
            }
        }

        private void UpdateOrdersDisplay()
        {
            if (_txtOrderStep1 != null)
            {
                _txtOrderStep1.text = _step1Done
                    ? "<color=#27AE60>✔</color> <color=#8C9B90><i>Hoist Canvas: Set Full Canvas</i></color>"
                    : "<color=#FFD700>1.</color> <b>Hoist Canvas:</b> Set Full Canvas (W key or Lever)";
            }

            if (_txtOrderStep2 != null)
            {
                _txtOrderStep2.text = _step2Done
                    ? "<color=#27AE60>✔</color> <color=#8C9B90><i>Turn the Helm: Steer Port or Starboard</i></color>"
                    : _step1Done
                        ? "<color=#FFD700>2.</color> <b>Turn the Helm:</b> Steer Port or Starboard (A/D or Wheel)"
                        : "<color=#888888>2. Turn the Helm: Steer Port or Starboard</color>";
            }

            if (_txtOrderStep3 != null)
            {
                _txtOrderStep3.text = _step3Done
                    ? "<color=#27AE60>✔</color> <color=#8C9B90><i>Test Cannons: Broadside fired</i></color>"
                    : _step2Done
                        ? "<color=#FFD700>3.</color> <b>Test Cannons:</b> Fire a broadside (Space / Q / E or Fire button)"
                        : "<color=#888888>3. Test Cannons: Fire broadside battery</color>";
            }

            if (_txtOrderStep4 != null)
            {
                _txtOrderStep4.text = _step4Done
                    ? "<color=#27AE60>✔</color> <color=#8C9B90><i>Navigate: Sea Chart charted</i></color>"
                    : _step3Done
                        ? "<color=#FFD700>4.</color> <b>Navigate:</b> Open the Sea Chart (M key or Map icon)"
                        : "<color=#888888>4. Navigate: Open Sea Chart</color>";
            }
        }

        private IEnumerator DismissOrdersBannerRoutine()
        {
            yield return new WaitForSeconds(5.0f);
            if (_ordersBannerGo != null)
            {
                _ordersBannerGo.SetActive(false);
            }
        }

        private void ToggleOrdersCollapse()
        {
            _isOrdersCollapsed = !_isOrdersCollapsed;
            if (_ordersBodyGo != null)
            {
                _ordersBodyGo.SetActive(!_isOrdersCollapsed);
            }
            if (_ordersBannerRt != null)
            {
                _ordersBannerRt.sizeDelta = new Vector2(640f, _isOrdersCollapsed ? 52f : 225f);
                LayoutRebuilder.ForceRebuildLayoutImmediate(_ordersBannerRt);
            }
            if (_txtToggleOrdersLabel != null)
            {
                _txtToggleOrdersLabel.text = _isOrdersCollapsed ? "[+] SHOW" : "[-] HIDE";
            }
            NauticalAudioManager.Instance?.PlayUIClick();
        }

        // =====================================================================
        // PROCEDURAL CANVAS & CARTOGRAPHIC PRIMER BUILDER
        // =====================================================================

        private void BuildTutorialCanvas()
        {
            // 1. Root Canvas
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 650; // Above PauseMenu (600), below GameOverUI (700)

            _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1600, 900);
            _scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            // 2. Fullscreen Modal Backdrop
            _modalBackdropGo = new GameObject("PrimerModalBackdrop");
            _modalBackdropGo.transform.SetParent(transform, false);
            RectTransform bdRt = _modalBackdropGo.AddComponent<RectTransform>();
            bdRt.anchorMin = Vector2.zero;
            bdRt.anchorMax = Vector2.one;
            bdRt.sizeDelta = Vector2.zero;

            _dimOverlayImg = _modalBackdropGo.AddComponent<Image>();
            _dimOverlayImg.sprite = NauticalUITheme.SolidWhite;
            _dimOverlayImg.color = new Color(0.04f, 0.06f, 0.09f, 0.85f);
            _dimOverlayImg.raycastTarget = true;

            // 3. Central Cartouche Box (940 x 650 - Expanded for mobile legibility)
            GameObject cardGo = new GameObject("PrimerCartouche");
            cardGo.transform.SetParent(_modalBackdropGo.transform, false);
            _cardRect = cardGo.AddComponent<RectTransform>();
            _cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            _cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            _cardRect.sizeDelta = new Vector2(940f, 650f);
            _cardRect.anchoredPosition = Vector2.zero;

            Image cardBg = cardGo.AddComponent<Image>();
            cardBg.sprite = NauticalUITheme.MahoganyPanel;
            cardBg.type = Image.Type.Sliced;

            // Inner Parchment Inset
            GameObject parchmentGo = new GameObject("ParchmentInset");
            parchmentGo.transform.SetParent(cardGo.transform, false);
            RectTransform pRt = parchmentGo.AddComponent<RectTransform>();
            pRt.anchorMin = Vector2.zero;
            pRt.anchorMax = Vector2.one;
            pRt.offsetMin = new Vector2(18f, 18f);
            pRt.offsetMax = new Vector2(-18f, -18f);

            Image pBg = parchmentGo.AddComponent<Image>();
            pBg.sprite = NauticalUITheme.ParchmentPanel;
            pBg.type = Image.Type.Sliced;
            pBg.color = new Color(0.93f, 0.87f, 0.74f, 1.0f);

            // 4. Header Section
            GameObject roseGo = new GameObject("HeaderIcon");
            roseGo.transform.SetParent(parchmentGo.transform, false);
            RectTransform roseRt = roseGo.AddComponent<RectTransform>();
            roseRt.anchoredPosition = new Vector2(0f, 258f);
            roseRt.sizeDelta = new Vector2(50f, 50f);
            _imgChapterIcon = roseGo.AddComponent<Image>();
            _imgChapterIcon.sprite = NauticalUITheme.CompassRose;
            _imgChapterIcon.color = new Color(0.77f, 0.61f, 0.15f);
            _imgChapterIcon.raycastTarget = false;

            GameObject titleGo = new GameObject("HeaderTitle");
            titleGo.transform.SetParent(parchmentGo.transform, false);
            RectTransform tRt = titleGo.AddComponent<RectTransform>();
            tRt.anchoredPosition = new Vector2(0f, 218f);
            tRt.sizeDelta = new Vector2(760f, 34f);
            Text tTxt = titleGo.AddComponent<Text>();
            tTxt.font = GetDefaultFont();
            tTxt.fontSize = 25;
            tTxt.fontStyle = FontStyle.Bold;
            tTxt.alignment = TextAnchor.MiddleCenter;
            tTxt.color = new Color(0.18f, 0.12f, 0.08f);
            tTxt.text = "THE CAPTAIN'S LOG & NAVAL PRIMER";

            // Skip Tutorial Button (top right corner of parchment)
            _btnSkip = CreateSubtleButton(parchmentGo.transform, new Vector2(380f, 260f), "SKIP TUTORIAL [X]", ClosePrimerModal, 175f, 36f);

            // 5. Chapter Pill Tabs
            string[] tabNames = { "1. HELM", "2. WIND", "3. CANNONS", "4. PORTS" };
            float tabWidth = 195f;
            float tabStartX = -292f;
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                GameObject tabGo = new GameObject($"Tab_{i}");
                tabGo.transform.SetParent(parchmentGo.transform, false);
                RectTransform tabRt = tabGo.AddComponent<RectTransform>();
                tabRt.anchoredPosition = new Vector2(tabStartX + (i * tabWidth), 170f);
                tabRt.sizeDelta = new Vector2(190f, 38f);

                Image tabImg = tabGo.AddComponent<Image>();
                tabImg.sprite = NauticalUITheme.BrassPanel;
                tabImg.type = Image.Type.Sliced;
                _tabImages[i] = tabImg;

                Button tabBtn = tabGo.AddComponent<Button>();
                tabBtn.targetGraphic = tabImg;
                tabBtn.onClick.AddListener(() => { NauticalAudioManager.Instance?.PlayUIClick(); ShowPage(idx); });
                _tabButtons[i] = tabBtn;

                GameObject lblGo = new GameObject("Label");
                lblGo.transform.SetParent(tabGo.transform, false);
                RectTransform lblRt = lblGo.AddComponent<RectTransform>();
                lblRt.anchorMin = Vector2.zero;
                lblRt.anchorMax = Vector2.one;
                lblRt.sizeDelta = Vector2.zero;

                Text lblTxt = lblGo.AddComponent<Text>();
                lblTxt.font = GetDefaultFont();
                lblTxt.fontSize = 14;
                lblTxt.fontStyle = FontStyle.Bold;
                lblTxt.alignment = TextAnchor.MiddleCenter;
                lblTxt.text = tabNames[i];
                _tabTexts[i] = lblTxt;
            }

            // 6. Chapter Content Box
            GameObject contentBoxGo = new GameObject("ChapterContentBox");
            contentBoxGo.transform.SetParent(parchmentGo.transform, false);
            RectTransform cbRt = contentBoxGo.AddComponent<RectTransform>();
            cbRt.anchoredPosition = new Vector2(0f, -4f);
            cbRt.sizeDelta = new Vector2(820f, 290f);

            // Title of current Chapter
            GameObject chTitleGo = new GameObject("ChapterTitle");
            chTitleGo.transform.SetParent(contentBoxGo.transform, false);
            RectTransform chttRt = chTitleGo.AddComponent<RectTransform>();
            chttRt.anchoredPosition = new Vector2(0f, 130f);
            chttRt.sizeDelta = new Vector2(800f, 28f);
            _txtChapterTitle = chTitleGo.AddComponent<Text>();
            _txtChapterTitle.font = GetDefaultFont();
            _txtChapterTitle.fontSize = 22;
            _txtChapterTitle.fontStyle = FontStyle.Bold;
            _txtChapterTitle.alignment = TextAnchor.MiddleCenter;
            _txtChapterTitle.color = new Color(0.24f, 0.16f, 0.10f);

            // Subtitle
            GameObject chSubGo = new GameObject("ChapterSubtitle");
            chSubGo.transform.SetParent(contentBoxGo.transform, false);
            RectTransform chsubRt = chSubGo.AddComponent<RectTransform>();
            chsubRt.anchoredPosition = new Vector2(0f, 104f);
            chsubRt.sizeDelta = new Vector2(800f, 22f);
            _txtChapterSubtitle = chSubGo.AddComponent<Text>();
            _txtChapterSubtitle.font = GetDefaultFont();
            _txtChapterSubtitle.fontSize = 15;
            _txtChapterSubtitle.fontStyle = FontStyle.Italic;
            _txtChapterSubtitle.alignment = TextAnchor.MiddleCenter;
            _txtChapterSubtitle.color = new Color(0.28f, 0.20f, 0.12f);

            // Body Text
            GameObject bodyGo = new GameObject("BodyText");
            bodyGo.transform.SetParent(contentBoxGo.transform, false);
            RectTransform bRt = bodyGo.AddComponent<RectTransform>();
            bRt.anchoredPosition = new Vector2(0f, -3f);
            bRt.sizeDelta = new Vector2(800f, 185f);
            _txtContentBody = bodyGo.AddComponent<Text>();
            _txtContentBody.font = GetDefaultFont();
            _txtContentBody.fontSize = 16;
            _txtContentBody.lineSpacing = 1.20f;
            _txtContentBody.alignment = TextAnchor.UpperLeft;
            _txtContentBody.color = new Color(0.16f, 0.12f, 0.08f);

            // Pro Tip Box at bottom of content
            GameObject tipBoxGo = new GameObject("DoctrineTipBox");
            tipBoxGo.transform.SetParent(contentBoxGo.transform, false);
            RectTransform tbRt = tipBoxGo.AddComponent<RectTransform>();
            tbRt.anchoredPosition = new Vector2(0f, -118f);
            tbRt.sizeDelta = new Vector2(800f, 46f);

            Image tipBg = tipBoxGo.AddComponent<Image>();
            tipBg.sprite = NauticalUITheme.SolidWhite;
            tipBg.color = new Color(0.18f, 0.12f, 0.08f, 0.08f);

            GameObject tipTxtGo = new GameObject("TipText");
            tipTxtGo.transform.SetParent(tipBoxGo.transform, false);
            RectTransform ttRt = tipTxtGo.AddComponent<RectTransform>();
            ttRt.anchorMin = Vector2.zero;
            ttRt.anchorMax = Vector2.one;
            ttRt.offsetMin = new Vector2(12f, 4f);
            ttRt.offsetMax = new Vector2(-12f, -4f);

            _txtProTip = tipTxtGo.AddComponent<Text>();
            _txtProTip.font = GetDefaultFont();
            _txtProTip.fontSize = 14;
            _txtProTip.fontStyle = FontStyle.Italic;
            _txtProTip.alignment = TextAnchor.MiddleLeft;
            _txtProTip.color = new Color(0.20f, 0.15f, 0.10f);

            // 7. Bottom Navigation Bar
            _btnPrev = CreateNavButton(parchmentGo.transform, new Vector2(-280f, -245f), "◀ PREVIOUS", HandlePrevClicked, false);

            GameObject pageIndGo = new GameObject("PageIndicator");
            pageIndGo.transform.SetParent(parchmentGo.transform, false);
            RectTransform piRt = pageIndGo.AddComponent<RectTransform>();
            piRt.anchoredPosition = new Vector2(0f, -245f);
            piRt.sizeDelta = new Vector2(220f, 40f);
            _txtPageIndicator = pageIndGo.AddComponent<Text>();
            _txtPageIndicator.font = GetDefaultFont();
            _txtPageIndicator.fontSize = 16;
            _txtPageIndicator.fontStyle = FontStyle.Bold;
            _txtPageIndicator.alignment = TextAnchor.MiddleCenter;
            _txtPageIndicator.color = new Color(0.38f, 0.30f, 0.20f);
            _txtPageIndicator.text = "CHAPTER 1 OF 4";

            _btnNext = CreateNavButton(parchmentGo.transform, new Vector2(280f, -245f), "NEXT CHAPTER ▶", HandleNextClicked, true, out _txtNextBtnLabel);

            // Initially hidden
            _modalBackdropGo.SetActive(false);

            // 8. Build Active In-Game Orders Banner (Top of HUD)
            BuildInGameOrdersBanner();
        }

        private void BuildInGameOrdersBanner()
        {
            _ordersBannerGo = new GameObject("CaptainOrdersHUD_Banner", typeof(RectTransform));
            _ordersBannerGo.transform.SetParent(transform, false);
            _ordersBannerRt = _ordersBannerGo.GetComponent<RectTransform>();
            _ordersBannerRt.anchorMin = new Vector2(0.5f, 1f);
            _ordersBannerRt.anchorMax = new Vector2(0.5f, 1f);
            _ordersBannerRt.pivot = new Vector2(0.5f, 1f);
            _ordersBannerRt.anchoredPosition = new Vector2(0f, -14f);
            _ordersBannerRt.sizeDelta = new Vector2(640f, 220f);

            // Give banner an independent HUD sorting layer so Sea Chart, Pause Menu & Modals sit above it
            Canvas bannerCanvas = _ordersBannerGo.AddComponent<Canvas>();
            bannerCanvas.overrideSorting = true;
            bannerCanvas.sortingOrder = 55;
            _ordersBannerGo.AddComponent<GraphicRaycaster>();

            Image bannerBg = _ordersBannerGo.AddComponent<Image>();
            bannerBg.sprite = NauticalUITheme.MahoganyPanel;
            bannerBg.type = Image.Type.Sliced;
            bannerBg.color = Color.white;

            VerticalLayoutGroup vlg = _ordersBannerGo.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 12, 14);
            vlg.spacing = 8f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter bannerCsf = _ordersBannerGo.AddComponent<ContentSizeFitter>();
            bannerCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            bannerCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 1. Header Row
            GameObject headerRowGo = new GameObject("HeaderRow", typeof(RectTransform));
            headerRowGo.transform.SetParent(_ordersBannerGo.transform, false);
            LayoutElement hLe = headerRowGo.AddComponent<LayoutElement>();
            hLe.minHeight = 36f;
            hLe.preferredHeight = 36f;

            // Header Title (Left-aligned)
            GameObject titleGo = new GameObject("HeaderTitle", typeof(RectTransform));
            titleGo.transform.SetParent(headerRowGo.transform, false);
            RectTransform tRt = titleGo.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0f, 0f);
            tRt.anchorMax = new Vector2(1f, 1f);
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = new Vector2(-125f, 0f);

            Text hTxt = titleGo.AddComponent<Text>();
            hTxt.font = GetDefaultFont();
            hTxt.fontSize = 17;
            hTxt.fontStyle = FontStyle.Bold;
            hTxt.alignment = TextAnchor.MiddleLeft;
            hTxt.color = NauticalUITheme.GoldAccent;
            hTxt.text = "⚓ CAPTAIN'S ORDERS (TUTORIAL)";

            // Toggle / Hide Button (Right-aligned)
            _btnToggleOrders = CreateSubtleButton(headerRowGo.transform, Vector2.zero, "[-] HIDE", ToggleOrdersCollapse, 115f, 32f, out _txtToggleOrdersLabel);
            RectTransform btnRt = _btnToggleOrders.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1f, 0.5f);
            btnRt.anchorMax = new Vector2(1f, 0.5f);
            btnRt.pivot = new Vector2(1f, 0.5f);
            btnRt.anchoredPosition = Vector2.zero;

            // 2. Orders Content Body Container
            _ordersBodyGo = new GameObject("OrdersBody", typeof(RectTransform));
            _ordersBodyGo.transform.SetParent(_ordersBannerGo.transform, false);
            LayoutElement bodyLe = _ordersBodyGo.AddComponent<LayoutElement>();
            bodyLe.minHeight = 145f;
            bodyLe.preferredHeight = 150f;

            VerticalLayoutGroup bodyVlg = _ordersBodyGo.AddComponent<VerticalLayoutGroup>();
            bodyVlg.spacing = 6f;
            bodyVlg.childControlWidth = true;
            bodyVlg.childControlHeight = true;
            bodyVlg.childForceExpandWidth = true;
            bodyVlg.childForceExpandHeight = false;

            ContentSizeFitter bodyCsf = _ordersBodyGo.AddComponent<ContentSizeFitter>();
            bodyCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            bodyCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Gold Divider Line (Using Kenney Divider)
            GameObject divGo = new GameObject("Divider", typeof(RectTransform));
            divGo.transform.SetParent(_ordersBodyGo.transform, false);
            LayoutElement divLe = divGo.AddComponent<LayoutElement>();
            divLe.minHeight = 4f;
            divLe.preferredHeight = 4f;
            Image divImg = divGo.AddComponent<Image>();
            divImg.sprite = NauticalUITheme.Divider;
            divImg.type = Image.Type.Sliced;
            divImg.color = NauticalUITheme.BrassBorder;

            // Step Rows
            _txtOrderStep1 = CreateOrderStepRow(_ordersBodyGo.transform, "1. Hoist Canvas: Set Full Canvas (W key or Lever)");
            _txtOrderStep2 = CreateOrderStepRow(_ordersBodyGo.transform, "2. Turn the Helm: Steer Port or Starboard (A/D or Wheel)");
            _txtOrderStep3 = CreateOrderStepRow(_ordersBodyGo.transform, "3. Test Cannons: Fire broadside battery (Space / Q / E or Fire button)");
            _txtOrderStep4 = CreateOrderStepRow(_ordersBodyGo.transform, "4. Navigate: Open the Sea Chart (M key or Map icon)");

            _ordersBannerGo.SetActive(false);
        }

        private Text CreateOrderStepRow(Transform parent, string text)
        {
            GameObject rowGo = new GameObject("StepRow", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            LayoutElement le = rowGo.AddComponent<LayoutElement>();
            le.minHeight = 30f;
            le.preferredHeight = 32f;

            Text t = rowGo.AddComponent<Text>();
            t.font = GetDefaultFont();
            t.fontSize = 15;
            t.alignment = TextAnchor.MiddleLeft;
            t.color = new Color(0.92f, 0.88f, 0.80f);
            t.text = text;
            return t;
        }

        private Button CreateNavButton(Transform parent, Vector2 pos, string label, Action onClick, bool isPrimary)
        {
            return CreateNavButton(parent, pos, label, onClick, isPrimary, out _);
        }

        private Button CreateNavButton(Transform parent, Vector2 pos, string label, Action onClick, bool isPrimary, out Text labelText)
        {
            GameObject btnGo = new GameObject($"Btn_{label.Replace(" ", "_")}");
            btnGo.transform.SetParent(parent, false);
            RectTransform rt = btnGo.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(210f, 44f);

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
            btn.colors = cb;

            btn.onClick.AddListener(() => onClick?.Invoke());

            GameObject txtGo = new GameObject("Label");
            txtGo.transform.SetParent(btnGo.transform, false);
            RectTransform txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;

            Text txt = txtGo.AddComponent<Text>();
            txt.font = GetDefaultFont();
            txt.fontSize = 14;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = isPrimary ? Color.white : new Color(0.95f, 0.82f, 0.35f);
            txt.text = label;

            labelText = txt;
            return btn;
        }

        private Button CreateSubtleButton(Transform parent, Vector2 pos, string label, Action onClick, float width, float height)
        {
            return CreateSubtleButton(parent, pos, label, onClick, width, height, out _);
        }

        private Button CreateSubtleButton(Transform parent, Vector2 pos, string label, Action onClick, float width, float height, out Text labelText)
        {
            GameObject btnGo = new GameObject($"Btn_{label.Replace(" ", "_")}");
            btnGo.transform.SetParent(parent, false);
            RectTransform rt = btnGo.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, height);

            Image img = btnGo.AddComponent<Image>();
            img.sprite = NauticalUITheme.BrassPanel;
            img.type = Image.Type.Sliced;
            img.color = Color.white;

            Button btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());

            GameObject txtGo = new GameObject("Label");
            txtGo.transform.SetParent(btnGo.transform, false);
            RectTransform txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;

            Text txt = txtGo.AddComponent<Text>();
            txt.font = GetDefaultFont();
            txt.fontSize = 12;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.18f, 0.12f, 0.08f); // High-contrast Mahogany on Brass button
            txt.text = label;

            labelText = txt;
            return btn;
        }
    }
}
