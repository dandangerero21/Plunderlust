using Plunderlust.Combat;
using Plunderlust.Core.Data;
using Plunderlust.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Plunderlust.Ship
{
    /// <summary>
    /// Decoupled mobile input bridge providing 17th-century nautical touch ergonomics:
    /// 1. Virtual Nautical Steering Wheel with realistic helm inertia and auto-centering.
    /// 2. Vertical Notch Throttle Lever (Full, Battle/Row, Anchored, Reverse).
    /// 3. Dynamic Weapon Buttons: Single Forward Swivel Gun (Tier 1) vs Dual Port/Starboard Broadsides (Tier 2-5).
    /// 4. Full Editor keyboard fallback (WASD/Arrows/Space/QE).
    /// Pre-allocates all event data and runs with zero per-frame garbage collection.
    /// </summary>
    public class MobileInputBridge : MonoBehaviour
    {
        public static MobileInputBridge Instance { get; private set; }

        [Header("Target Ship Locomotion & Combat")]
        [SerializeField] private ShipLocomotionController _locomotion;
        [SerializeField] private NavalCombatController _combat;
        [SerializeField] private ShipIdentitySystem _identity;

        [Header("Steering Wheel Settings")]
        [SerializeField] private float _maxWheelAngle = 90.0f;
        [SerializeField] private float _wheelReturnSpeed = 160.0f; // degrees/sec return to center
        [SerializeField] private float _wheelInertia = 0.08f;

        [Header("Optional External Control Prefabs (TBD)")]
        [SerializeField] private GameObject _steeringWheelPrefab;
        [SerializeField] private GameObject _throttleLeverPrefab;
        [SerializeField] private GameObject _swivelButtonPrefab;
        [SerializeField] private GameObject _broadsideButtonsPrefab;

        // Runtime UI References
        private Canvas _canvas;
        private RectTransform _safeAreaRect;
        private RectTransform _wheelRect;
        private RectTransform _throttleKnobRect;
        private RectTransform _throttleTrackRect;
        private GameObject _swivelGunTriggerGo;
        private GameObject _portBroadsideTriggerGo;
        private GameObject _starboardBroadsideTriggerGo;
        private Image _portCooldownFill;
        private Image _starboardCooldownFill;
        private Image _swivelCooldownFill;
        private Image _repairCooldownFill;
        private GameObject _boardButtonGo;

        // Steering Wheel Runtime State
        private bool _isDraggingWheel = false;
        private float _currentWheelAngle = 0f;
        private float _wheelVelocity = 0f;
        private Vector2 _wheelCenterScreen = Vector2.zero;

        // Throttle Runtime State
        private bool _isDraggingThrottle = false;
        private float _targetThrottle = 0f;
        private float _currentThrottle = 0f;

        // Weapon Aim Drag State
        private bool _isAimingPort = false;
        private bool _isAimingStarboard = false;
        private bool _isAimingSwivel = false;
        private Vector2 _aimDragStart = Vector2.zero;

        // Public Control Properties
        public float SteerOutput { get; private set; }
        public float ThrottleOutput { get; private set; }
        public bool IsDraggingThrottle => _isDraggingThrottle;
        public bool IsDraggingWheel => _isDraggingWheel;
        public bool IsAimingWeapon => _isAimingPort || _isAimingStarboard || _isAimingSwivel;

        private int _cachedPortCannons = -1;
        private int _cachedStarboardCannons = -1;
        private ShipTier _cachedTier = (ShipTier)(-1);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureEventSystem();
            FindPlayerComponents();

            var existing = GameObject.Find("Plunderlust_MobileControlsCanvas");
            if (existing != null)
            {
                DestroyImmediate(existing);
            }

            BuildMobileHUDCanvas();
        }

        private void Start()
        {
            if (_locomotion == null) FindPlayerComponents();
            if (Shipyard.ShipyardManager.Instance != null)
            {
                Shipyard.ShipyardManager.Instance.OnShipUpgraded += HandleShipUpgraded;
            }
            UpdateWeaponTriggerLayout();
        }

        private void EnsureEventSystem()
        {
            var es = FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                GameObject esGo = new GameObject("EventSystem");
                es = esGo.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                esGo.AddComponent<StandaloneInputModule>();
#endif
            }
            else
            {
#if ENABLE_INPUT_SYSTEM
                var standalone = es.GetComponent<StandaloneInputModule>();
                if (standalone != null) Destroy(standalone);
                if (es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
                    es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                var inputModule = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                if (inputModule != null) Destroy(inputModule);
                if (es.GetComponent<StandaloneInputModule>() == null)
                    es.gameObject.AddComponent<StandaloneInputModule>();
#endif
            }
        }

        public void FindPlayerComponents()
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                _locomotion = playerGo.GetComponent<ShipLocomotionController>();
                _combat = playerGo.GetComponent<NavalCombatController>();
                _identity = playerGo.GetComponent<ShipIdentitySystem>();
                if (_identity == null)
                {
                    _identity = playerGo.AddComponent<ShipIdentitySystem>();
                }
                _identity.OnShipRenamed += HandleShipRenamed;
            }
        }

        private void OnDestroy()
        {
            if (_identity != null)
            {
                _identity.OnShipRenamed -= HandleShipRenamed;
            }
            if (Shipyard.ShipyardManager.Instance != null)
            {
                Shipyard.ShipyardManager.Instance.OnShipUpgraded -= HandleShipUpgraded;
            }
            if (Instance == this) Instance = null;
        }

        private void HandleShipRenamed(string shipName, string className)
        {
            UpdateWeaponTriggerLayout();
        }

        private void HandleShipUpgraded(ShipTier newTier)
        {
            UpdateWeaponTriggerLayout();
        }

        private void Update()
        {
            if (_locomotion == null) FindPlayerComponents();
            if (_locomotion == null) return;

            // Monitor hull/cannon changes every frame with zero heap allocation
            int currentPort = _combat != null ? _combat.PortCannonsCount : 0;
            int currentStar = _combat != null ? _combat.StarboardCannonsCount : 0;
            ShipTier currentTier = _identity != null ? _identity.CurrentTier : ShipTier.Tier1_LeakyRowboat;

            if (currentPort != _cachedPortCannons || currentStar != _cachedStarboardCannons || currentTier != _cachedTier)
            {
                _cachedPortCannons = currentPort;
                _cachedStarboardCannons = currentStar;
                _cachedTier = currentTier;
                UpdateWeaponTriggerLayout();
            }

            if (PauseMenuUI.IsPaused || GameOverUI.IsGameOver)
            {
                SteerOutput = 0f;
                ThrottleOutput = 0f;
                if (_locomotion != null)
                {
                    _locomotion.SteerInput = 0f;
                    _locomotion.ThrottleInput = 0f;
                }
                return;
            }

            float dt = Time.deltaTime;

            // 1. Process Steering Wheel Physics & Damping
            if (_isDraggingWheel)
            {
                SteerOutput = Mathf.Clamp(_currentWheelAngle / _maxWheelAngle, -1.0f, 1.0f);
            }
            else
            {
                // Auto-center with nautical helm damping
                _currentWheelAngle = Mathf.SmoothDamp(_currentWheelAngle, 0f, ref _wheelVelocity, _wheelInertia, _wheelReturnSpeed, dt);
                if (Mathf.Abs(_currentWheelAngle) < 0.2f)
                {
                    _currentWheelAngle = 0f;
                    _wheelVelocity = 0f;
                }
                SteerOutput = Mathf.Clamp(_currentWheelAngle / _maxWheelAngle, -1.0f, 1.0f);
            }

            // Keyboard Override / Editor Support for Steer
            float keySteer = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) keySteer -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) keySteer += 1f;

            if (Mathf.Abs(keySteer) > 0.05f)
            {
                SteerOutput = keySteer;
                _currentWheelAngle = -keySteer * _maxWheelAngle;
            }

            if (_wheelRect != null)
            {
                _wheelRect.localRotation = Quaternion.Euler(0f, 0f, -_currentWheelAngle);
            }

            // 2. Process Throttle & Propulsion
            // Keyboard Override / Editor Support for Throttle
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) _targetThrottle = 1.0f;
            else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) _targetThrottle = -0.85f;
            else if (Input.GetKey(KeyCode.X)) _targetThrottle = 0.0f; // Emergency stop

            _currentThrottle = Mathf.MoveTowards(_currentThrottle, _targetThrottle, 6.0f * dt);
            ThrottleOutput = _currentThrottle;

            // Feed directly into Locomotion
            _locomotion.SteerInput = SteerOutput;
            _locomotion.ThrottleInput = ThrottleOutput;

            // Synchronize Sail / Oar propulsion state
            SyncSailStateWithThrottle(_currentThrottle);

            UpdateThrottleKnobVisual();
            UpdateCooldownFills();
            HandleKeyboardCombatShortcuts();
        }

        public void SyncSailStateWithThrottle(float throttle)
        {
            if (_locomotion == null) return;

            bool isRowboat = _locomotion.HullData != null && _locomotion.HullData.SupportsRowing && !_locomotion.HullData.SupportsSails;
            if (isRowboat)
            {
                if (Mathf.Abs(throttle) > 0.05f)
                {
                    if (_locomotion.CurrentSailState != SailState.Rowing)
                    {
                        _locomotion.SetSailState(SailState.Rowing);
                    }
                }
                else
                {
                    if (_locomotion.CurrentSailState != SailState.Anchored)
                    {
                        _locomotion.SetSailState(SailState.Anchored);
                    }
                }
            }
            else
            {
                if (throttle > 0.75f && _locomotion.CurrentSailState != SailState.FullCanvas)
                {
                    _locomotion.SetSailState(SailState.FullCanvas);
                }
                else if (throttle > 0.15f && throttle <= 0.75f && _locomotion.CurrentSailState != SailState.BattleSails)
                {
                    _locomotion.SetSailState(SailState.BattleSails);
                }
                else if (Mathf.Abs(throttle) <= 0.05f && _locomotion.CurrentSailState != SailState.Anchored)
                {
                    _locomotion.SetSailState(SailState.Anchored);
                }
            }
        }

        private void HandleKeyboardCombatShortcuts()
        {
            if (_combat == null) return;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                _combat.FireForwardSwivel();
            }
            if (Input.GetKeyDown(KeyCode.Q))
            {
                _combat.FirePortBroadside();
            }
            if (Input.GetKeyDown(KeyCode.E))
            {
                _combat.FireStarboardBroadside();
            }

            // Check for nearby surrendered vessels to show mobile BOARD & PLUNDER button
            if (_boardButtonGo != null && Time.frameCount % 10 == 0)
            {
                bool hasSurrenderedNearby = false;
                var aiShips = FindObjectsByType<Plunderlust.AI.AIShipBrain>(FindObjectsInactive.Exclude);
                for (int i = 0; i < aiShips.Length; i++)
                {
                    var ai = aiShips[i];
                    if (ai != null && ai.IsSurrendered && !ai.IsPlundered)
                    {
                        if (Vector3.Distance(transform.position, ai.transform.position) <= 22.0f)
                        {
                            hasSurrenderedNearby = true;
                            break;
                        }
                    }
                }
                if (_boardButtonGo.activeSelf != hasSurrenderedNearby)
                {
                    _boardButtonGo.SetActive(hasSurrenderedNearby);
                }
            }
        }

        private void UpdateThrottleKnobVisual()
        {
            if (_throttleKnobRect == null || _throttleTrackRect == null) return;

            // Map throttle [-0.85 .. 1.0] to track height
            float trackH = _throttleTrackRect.rect.height - 32f;
            float norm = Mathf.InverseLerp(-0.85f, 1.0f, _currentThrottle);
            float yPos = (norm - 0.5f) * trackH;
            _throttleKnobRect.anchoredPosition = new Vector2(0f, yPos);
        }

        private void UpdateCooldownFills()
        {
            if (_combat == null) return;

            // 1. Forward Swivel Gun Reload Sweep
            if (_swivelCooldownFill != null)
            {
                if (_combat.CanFireForward)
                {
                    _swivelCooldownFill.fillAmount = 0f;
                }
                else
                {
                    // Aligns dynamically with crew efficiency and Gunner specialist perks
                    _swivelCooldownFill.fillAmount = _combat.ForwardReloadNormalized;
                }
            }

            // 2. Port Broadside Reload Sweep
            if (_portCooldownFill != null)
            {
                if (_combat.GetActivePortCannons() <= 0)
                {
                    _portCooldownFill.fillAmount = 1.0f; // Damaged / disabled
                }
                else if (_combat.CanFirePort)
                {
                    _portCooldownFill.fillAmount = 0f;
                }
                else
                {
                    // Aligns dynamically with crew efficiency and Gunner specialist perks
                    _portCooldownFill.fillAmount = _combat.PortReloadNormalized;
                }
            }

            // 3. Starboard Broadside Reload Sweep
            if (_starboardCooldownFill != null)
            {
                if (_combat.GetActiveStarboardCannons() <= 0)
                {
                    _starboardCooldownFill.fillAmount = 1.0f; // Damaged / disabled
                }
                else if (_combat.CanFireStarboard)
                {
                    _starboardCooldownFill.fillAmount = 0f;
                }
                else
                {
                    // Aligns dynamically with crew efficiency and Gunner specialist perks
                    _starboardCooldownFill.fillAmount = _combat.StarboardReloadNormalized;
                }
            }

            // 4. Emergency Repairs Cooldown Sweep
            if (_repairCooldownFill != null)
            {
                EmergencyRepairsController repairs = EmergencyRepairsController.Instance;
                if (repairs != null && (repairs.HullCooldownNormalized > 0f || repairs.SailsCooldownNormalized > 0f))
                {
                    _repairCooldownFill.fillAmount = Mathf.Max(repairs.HullCooldownNormalized, repairs.SailsCooldownNormalized);
                }
                else
                {
                    _repairCooldownFill.fillAmount = 0f;
                }
            }
        }

        public void UpdateWeaponTriggerLayout()
        {
            bool hasBroadsides = false;
            if (_combat != null)
            {
                hasBroadsides = (_combat.PortCannonsCount > 0 || _combat.StarboardCannonsCount > 0);
            }
            else if (_identity != null)
            {
                hasBroadsides = (_identity.CurrentTier != ShipTier.Tier1_LeakyRowboat);
            }
            else if (_locomotion != null && _locomotion.HullData != null)
            {
                hasBroadsides = (_locomotion.HullData.Tier != ShipTier.Tier1_LeakyRowboat);
            }

            if (_swivelGunTriggerGo != null) _swivelGunTriggerGo.SetActive(!hasBroadsides);
            if (_portBroadsideTriggerGo != null) _portBroadsideTriggerGo.SetActive(hasBroadsides);
            if (_starboardBroadsideTriggerGo != null) _starboardBroadsideTriggerGo.SetActive(hasBroadsides);
        }

        // --- Procedural Canvas Construction ---

        public void BuildMobileHUDCanvas()
        {
            // Canvas Root
            GameObject canvasGo = new GameObject("Plunderlust_MobileControlsCanvas");
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Safe Area Container
            GameObject safeAreaGo = new GameObject("SafeAreaContainer");
            safeAreaGo.transform.SetParent(canvasGo.transform, false);
            _safeAreaRect = safeAreaGo.AddComponent<RectTransform>();
            _safeAreaRect.anchorMin = Vector2.zero;
            _safeAreaRect.anchorMax = Vector2.one;
            _safeAreaRect.offsetMin = Vector2.zero;
            _safeAreaRect.offsetMax = Vector2.zero;
            safeAreaGo.AddComponent<SafeAreaFitter>();

            BuildSteeringWheelUI(_safeAreaRect);
            BuildThrottleLeverUI(_safeAreaRect);
            BuildCombatTriggersUI(_safeAreaRect);
            BuildAuxiliaryButtonsUI(_safeAreaRect);
        }

        private void BuildSteeringWheelUI(RectTransform parent)
        {
            GameObject wheelRoot = new GameObject("VirtualSteeringWheel");
            wheelRoot.transform.SetParent(parent, false);
            RectTransform rootRect = wheelRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(0f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = new Vector2(185f, 185f);
            rootRect.sizeDelta = new Vector2(260f, 260f);

            // Wheel Outer Mount Ring
            GameObject mountGo = new GameObject("WheelMount");
            mountGo.transform.SetParent(wheelRoot.transform, false);
            RectTransform mountRect = mountGo.AddComponent<RectTransform>();
            mountRect.anchorMin = Vector2.zero;
            mountRect.anchorMax = Vector2.one;
            mountRect.sizeDelta = Vector2.zero;
            Image mountImg = mountGo.AddComponent<Image>();
            mountImg.sprite = NauticalUITheme.CircleWaxSeal;
            mountImg.color = new Color(0.25f, 0.18f, 0.10f, 0.55f);
            mountImg.raycastTarget = false;

            // Rotatable Helm Wheel
            GameObject helmGo = new GameObject("HelmWheelImage");
            helmGo.transform.SetParent(wheelRoot.transform, false);
            _wheelRect = helmGo.AddComponent<RectTransform>();
            _wheelRect.anchorMin = Vector2.zero;
            _wheelRect.anchorMax = Vector2.one;
            _wheelRect.sizeDelta = new Vector2(-22f, -22f);
            Image helmImg = helmGo.AddComponent<Image>();
            helmImg.sprite = NauticalUITheme.CircleWaxSeal;
            helmImg.color = NauticalUITheme.BrassBorder;
            helmImg.raycastTarget = false;

            // Center Brass Boss/Hub
            GameObject hubGo = new GameObject("CenterHub");
            hubGo.transform.SetParent(helmGo.transform, false);
            RectTransform hubRect = hubGo.AddComponent<RectTransform>();
            hubRect.anchorMin = new Vector2(0.5f, 0.5f);
            hubRect.anchorMax = new Vector2(0.5f, 0.5f);
            hubRect.sizeDelta = new Vector2(58f, 58f);
            Image hubImg = hubGo.AddComponent<Image>();
            hubImg.sprite = NauticalUITheme.CircleWaxSeal;
            hubImg.color = NauticalUITheme.MahoganyDark;
            hubImg.raycastTarget = false;

            // Wheel Spokes Indicator
            GameObject spokeGo = new GameObject("TopSpokePip");
            spokeGo.transform.SetParent(helmGo.transform, false);
            RectTransform spokeRect = spokeGo.AddComponent<RectTransform>();
            spokeRect.anchorMin = new Vector2(0.5f, 0.5f);
            spokeRect.anchorMax = new Vector2(0.5f, 0.5f);
            spokeRect.anchoredPosition = new Vector2(0f, 96f);
            spokeRect.sizeDelta = new Vector2(20f, 30f);
            Image spokeImg = spokeGo.AddComponent<Image>();
            spokeImg.sprite = NauticalUITheme.SolidWhite;
            spokeImg.color = NauticalUITheme.GoldAccent;
            spokeImg.raycastTarget = false;

            // Wheel Root Raycast Image to catch all mouse clicks and drags
            Image wheelHit = wheelRoot.AddComponent<Image>();
            wheelHit.color = Color.clear;
            wheelHit.raycastTarget = true;

            // Event Trigger Handler
            EventTrigger trigger = wheelRoot.AddComponent<EventTrigger>();

            // PointerDown
            EventTrigger.Entry down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener((data) =>
            {
                var pe = (PointerEventData)data;
                _isDraggingWheel = true;
                _wheelCenterScreen = RectTransformUtility.WorldToScreenPoint(null, rootRect.position);
                UpdateWheelAngleFromPointer(pe.position);
            });
            trigger.triggers.Add(down);

            // Drag
            EventTrigger.Entry drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener((data) =>
            {
                var pe = (PointerEventData)data;
                UpdateWheelAngleFromPointer(pe.position);
            });
            trigger.triggers.Add(drag);

            // PointerUp
            EventTrigger.Entry up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener((data) =>
            {
                _isDraggingWheel = false;
            });
            trigger.triggers.Add(up);
        }

        private void UpdateWheelAngleFromPointer(Vector2 screenPos)
        {
            Vector2 dir = screenPos - _wheelCenterScreen;
            if (dir.sqrMagnitude < 40f) return;

            // Calculate angle where Up = 0 deg
            float angle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            _currentWheelAngle = Mathf.Clamp(angle, -_maxWheelAngle, _maxWheelAngle);
        }

        private void BuildThrottleLeverUI(RectTransform parent)
        {
            GameObject leverRoot = new GameObject("VerticalThrottleLever");
            leverRoot.transform.SetParent(parent, false);
            RectTransform rootRect = leverRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(0f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = new Vector2(360f, 185f);
            rootRect.sizeDelta = new Vector2(85f, 260f);

            // Give leverRoot a clear raycast image so mouse clicks and drags on the track work reliably
            Image leverHit = leverRoot.AddComponent<Image>();
            leverHit.color = Color.clear;
            leverHit.raycastTarget = true;

            // Track Background
            GameObject trackGo = new GameObject("ThrottleTrack");
            trackGo.transform.SetParent(leverRoot.transform, false);
            _throttleTrackRect = trackGo.AddComponent<RectTransform>();
            _throttleTrackRect.anchorMin = Vector2.zero;
            _throttleTrackRect.anchorMax = Vector2.one;
            _throttleTrackRect.sizeDelta = Vector2.zero;
            Image trackImg = trackGo.AddComponent<Image>();
            trackImg.sprite = NauticalUITheme.SlideTrackVertical;
            trackImg.type = Image.Type.Sliced;
            trackImg.color = Color.white;
            trackImg.raycastTarget = false;

            // 4 Tactile Notch Indicator Labels
            CreateNotchPlaque(leverRoot.transform, "FULL", new Vector2(0f, 92f), 1.0f);
            CreateNotchPlaque(leverRoot.transform, "HALF", new Vector2(0f, 32f), 0.5f);
            CreateNotchPlaque(leverRoot.transform, "ANCH", new Vector2(0f, -28f), 0.0f);
            CreateNotchPlaque(leverRoot.transform, "REV", new Vector2(0f, -88f), -0.85f);

            // Sliding Brass Knob (Using Kenney Slide Handle)
            GameObject knobGo = new GameObject("ThrottleKnob");
            knobGo.transform.SetParent(trackGo.transform, false);
            _throttleKnobRect = knobGo.AddComponent<RectTransform>();
            _throttleKnobRect.anchorMin = new Vector2(0.5f, 0.5f);
            _throttleKnobRect.anchorMax = new Vector2(0.5f, 0.5f);
            _throttleKnobRect.sizeDelta = new Vector2(64f, 54f);
            Image knobImg = knobGo.AddComponent<Image>();
            knobImg.sprite = NauticalUITheme.SlideHandle;
            knobImg.type = Image.Type.Simple;
            knobImg.color = Color.white;
            knobImg.raycastTarget = false;

            // Knob Center Pip
            GameObject pipGo = new GameObject("KnobPip");
            pipGo.transform.SetParent(knobGo.transform, false);
            RectTransform pipRect = pipGo.AddComponent<RectTransform>();
            pipRect.anchorMin = new Vector2(0.5f, 0.5f);
            pipRect.anchorMax = new Vector2(0.5f, 0.5f);
            pipRect.sizeDelta = new Vector2(28f, 7f);
            Image pipImg = pipGo.AddComponent<Image>();
            pipImg.sprite = NauticalUITheme.SolidWhite;
            pipImg.color = NauticalUITheme.MahoganyDark;
            pipImg.raycastTarget = false;

            // Drag Handler on Lever
            EventTrigger trigger = leverRoot.AddComponent<EventTrigger>();

            EventTrigger.Entry down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener((data) =>
            {
                var pe = (PointerEventData)data;
                _isDraggingThrottle = true;
                UpdateThrottleFromPointer(pe.position, rootRect);
            });
            trigger.triggers.Add(down);

            EventTrigger.Entry drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener((data) =>
            {
                var pe = (PointerEventData)data;
                UpdateThrottleFromPointer(pe.position, rootRect);
            });
            trigger.triggers.Add(drag);

            EventTrigger.Entry up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener((data) =>
            {
                _isDraggingThrottle = false;
                SnapThrottleToNearestNotch();
            });
            trigger.triggers.Add(up);
        }

        private void CreateNotchPlaque(Transform parent, string label, Vector2 pos, float targetValue)
        {
            GameObject notchGo = new GameObject($"Notch_{label}");
            notchGo.transform.SetParent(parent, false);
            RectTransform rt = notchGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(68f, 26f);

            Text t = notchGo.AddComponent<Text>();
            t.text = label;
            t.font = NauticalUITheme.GetDefaultFont();
            t.fontSize = 13;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = NauticalUITheme.ParchmentDark;
            t.raycastTarget = false;

            // Tap notch to snap
            Button btn = notchGo.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                _targetThrottle = targetValue;
                _currentThrottle = targetValue;
                ThrottleOutput = targetValue;
                if (_locomotion != null)
                {
                    _locomotion.ThrottleInput = targetValue;
                    SyncSailStateWithThrottle(targetValue);
                }
            });
        }

        private void UpdateThrottleFromPointer(Vector2 screenPos, RectTransform rootRect)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, screenPos, null, out Vector2 localPt))
            {
                float trackH = rootRect.rect.height - 32f;
                float norm = Mathf.Clamp01((localPt.y / trackH) + 0.5f);
                _targetThrottle = Mathf.Lerp(-0.85f, 1.0f, norm);
                _currentThrottle = _targetThrottle;
                ThrottleOutput = _targetThrottle;
                if (_locomotion != null)
                {
                    _locomotion.ThrottleInput = _targetThrottle;
                    SyncSailStateWithThrottle(_targetThrottle);
                }
            }
        }

        private void SnapThrottleToNearestNotch()
        {
            float[] notches = { -0.85f, 0.0f, 0.5f, 1.0f };
            float closest = notches[0];
            float minDiff = Mathf.Abs(_targetThrottle - notches[0]);

            for (int i = 1; i < notches.Length; i++)
            {
                float diff = Mathf.Abs(_targetThrottle - notches[i]);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closest = notches[i];
                }
            }
            _targetThrottle = closest;
            _currentThrottle = closest;
            ThrottleOutput = closest;
            if (_locomotion != null)
            {
                _locomotion.ThrottleInput = closest;
                SyncSailStateWithThrottle(closest);
            }
        }

        private void BuildCombatTriggersUI(RectTransform parent)
        {
            // 1. Tier 1 Harbor Skiff: Single Large Forward Swivel Gun Button (Enlarged to 150x150 for mobile)
            _swivelGunTriggerGo = CreateTouchButton(parent, "SwivelGunTrigger", new Vector2(-155f, 155f), new Vector2(150f, 150f),
                NauticalUITheme.CircleWaxSeal, "SWIVEL", out _swivelCooldownFill,
                () => { _isAimingSwivel = true; },
                () =>
                {
                    _isAimingSwivel = false;
                    if (_combat != null)
                    {
                        if (_combat.FireForwardSwivel())
                        {
                            Plunderlust.UI.TutorialManagerUI.Instance?.CompleteStep3();
                        }
                    }
                });

            // 2. Tier 2-5 Dual Broadsides: Port & Starboard Triggers (Enlarged to 132x132 for mobile)
            _portBroadsideTriggerGo = CreateTouchButton(parent, "PortBroadsideTrigger", new Vector2(-245f, 155f), new Vector2(132f, 132f),
                NauticalUITheme.CircleWaxSeal, "PORT\nFIRE", out _portCooldownFill,
                () => { _isAimingPort = true; },
                () =>
                {
                    _isAimingPort = false;
                    if (_combat != null)
                    {
                        if (_combat.FirePortBroadside())
                        {
                            Plunderlust.UI.TutorialManagerUI.Instance?.CompleteStep3();
                        }
                    }
                });

            _starboardBroadsideTriggerGo = CreateTouchButton(parent, "StarboardBroadsideTrigger", new Vector2(-100f, 155f), new Vector2(132f, 132f),
                NauticalUITheme.CircleWaxSeal, "STARBD\nFIRE", out _starboardCooldownFill,
                () => { _isAimingStarboard = true; },
                () =>
                {
                    _isAimingStarboard = false;
                    if (_combat != null)
                    {
                        if (_combat.FireStarboardBroadside())
                        {
                            Plunderlust.UI.TutorialManagerUI.Instance?.CompleteStep3();
                        }
                    }
                });
        }

        private GameObject CreateTouchButton(RectTransform parent, string name, Vector2 pos, Vector2 size, Sprite bg, string text, out Image cooldownFill, UnityEngine.Events.UnityAction onPress, UnityEngine.Events.UnityAction onRelease)
        {
            GameObject btnGo = new GameObject(name);
            btnGo.transform.SetParent(parent, false);
            RectTransform rt = btnGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = btnGo.AddComponent<Image>();
            img.sprite = bg;
            img.color = Color.white;

            // Cooldown Mask Overlay
            GameObject cdGo = new GameObject("CooldownOverlay");
            cdGo.transform.SetParent(btnGo.transform, false);
            RectTransform cdRt = cdGo.AddComponent<RectTransform>();
            cdRt.anchorMin = Vector2.zero;
            cdRt.anchorMax = Vector2.one;
            cdRt.sizeDelta = Vector2.zero;
            cooldownFill = cdGo.AddComponent<Image>();
            cooldownFill.sprite = NauticalUITheme.CircleWaxSeal;
            cooldownFill.type = Image.Type.Filled;
            cooldownFill.fillMethod = Image.FillMethod.Radial360;
            cooldownFill.fillOrigin = (int)Image.Origin360.Top;
            cooldownFill.fillClockwise = true;
            cooldownFill.color = new Color(0.08f, 0.08f, 0.08f, 0.72f);
            cooldownFill.raycastTarget = false;

            // Text Label (Enlarged to 18pt font for mobile readability)
            GameObject txtGo = new GameObject("Label");
            txtGo.transform.SetParent(btnGo.transform, false);
            RectTransform txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;
            Text t = txtGo.AddComponent<Text>();
            t.text = text;
            t.font = NauticalUITheme.GetDefaultFont();
            t.fontSize = 18;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = NauticalUITheme.ParchmentLight;
            t.raycastTarget = false;

            // Touch Press / Drag / Release Handling
            EventTrigger trigger = btnGo.AddComponent<EventTrigger>();

            EventTrigger.Entry down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener((data) =>
            {
                var pe = (PointerEventData)data;
                _aimDragStart = pe.position;
                img.color = new Color(0.8f, 0.8f, 0.8f, 1f);
                onPress?.Invoke();
            });
            trigger.triggers.Add(down);

            EventTrigger.Entry drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener((data) =>
            {
                var pe = (PointerEventData)data;
                float deltaY = (pe.position.y - _aimDragStart.y) * 0.08f;
                if (_combat != null)
                {
                    _combat.AimPitchAngle = Mathf.Clamp(6.0f + deltaY, -5f, 25f);
                }
            });
            trigger.triggers.Add(drag);

            EventTrigger.Entry up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener((data) =>
            {
                img.color = Color.white;
                onRelease?.Invoke();
                if (_combat != null)
                {
                    _combat.AimPitchAngle = 6.0f; // reset to nominal pitch
                }
            });
            trigger.triggers.Add(up);

            return btnGo;
        }

        private void BuildAuxiliaryButtonsUI(RectTransform parent)
        {
            // Emergency Sea Repair HUD Button (Lower Right, above broadsides - enlarged to 78x78)
            GameObject repairGo = new GameObject("EmergencyRepairBtn");
            repairGo.transform.SetParent(parent, false);
            RectTransform rt = repairGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-75f, 275f);
            rt.sizeDelta = new Vector2(78f, 78f);

            Image img = repairGo.AddComponent<Image>();
            img.sprite = NauticalUITheme.EmeraldSeal;
            img.color = Color.white;

            // Cooldown Mask Overlay for Emergency Repairs
            GameObject cdGo = new GameObject("RepairCooldownOverlay");
            cdGo.transform.SetParent(repairGo.transform, false);
            RectTransform cdRt = cdGo.AddComponent<RectTransform>();
            cdRt.anchorMin = Vector2.zero;
            cdRt.anchorMax = Vector2.one;
            cdRt.sizeDelta = Vector2.zero;
            _repairCooldownFill = cdGo.AddComponent<Image>();
            _repairCooldownFill.sprite = NauticalUITheme.EmeraldSeal;
            _repairCooldownFill.type = Image.Type.Filled;
            _repairCooldownFill.fillMethod = Image.FillMethod.Radial360;
            _repairCooldownFill.fillOrigin = (int)Image.Origin360.Top;
            _repairCooldownFill.fillClockwise = true;
            _repairCooldownFill.color = new Color(0.08f, 0.08f, 0.08f, 0.72f);
            _repairCooldownFill.raycastTarget = false;

            GameObject txtGo = new GameObject("RepairLabel");
            txtGo.transform.SetParent(repairGo.transform, false);
            RectTransform txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;
            Text t = txtGo.AddComponent<Text>();
            t.text = "REPAIR";
            t.font = NauticalUITheme.GetDefaultFont();
            t.fontSize = 13;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;

            Button btn = repairGo.AddComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                EmergencyRepairsController repairs = FindAnyObjectByType<EmergencyRepairsController>();
                if (repairs != null) repairs.TriggerEmergencyRepairs();
            });

            // Note: Sea Chart button is cleanly integrated directly under the compass minimap in NavalMinimapAndChart.cs
            GameObject chartBtnGo = new GameObject("OpenSeaChartBtn");
            chartBtnGo.transform.SetParent(parent, false);
            chartBtnGo.SetActive(false); // Hide duplicate to avoid cluttering minimap area
            RectTransform chartRt = chartBtnGo.AddComponent<RectTransform>();
            chartRt.anchorMin = new Vector2(1f, 1f);
            chartRt.anchorMax = new Vector2(1f, 1f);
            chartRt.pivot = new Vector2(1f, 1f);
            chartRt.anchoredPosition = new Vector2(-20f, -20f);
            chartRt.sizeDelta = new Vector2(68f, 68f);

            Image chartImg = chartBtnGo.AddComponent<Image>();
            chartImg.sprite = NauticalUITheme.CircleWaxSeal;
            chartImg.color = NauticalUITheme.GoldAccent;

            GameObject cTxtGo = new GameObject("ChartLabel");
            cTxtGo.transform.SetParent(chartBtnGo.transform, false);
            RectTransform cTxtRt = cTxtGo.AddComponent<RectTransform>();
            cTxtRt.anchorMin = Vector2.zero;
            cTxtRt.anchorMax = Vector2.one;
            cTxtRt.sizeDelta = Vector2.zero;
            Text ct = cTxtGo.AddComponent<Text>();
            ct.text = "MAP";
            ct.font = NauticalUITheme.GetDefaultFont();
            ct.fontSize = 13;
            ct.fontStyle = FontStyle.Bold;
            ct.alignment = TextAnchor.MiddleCenter;
            ct.color = NauticalUITheme.MahoganyDark;
            ct.raycastTarget = false;

            Button cBtn = chartBtnGo.AddComponent<Button>();
            cBtn.onClick.AddListener(() =>
            {
                SeaChartMapUI chartUI = FindAnyObjectByType<SeaChartMapUI>();
                if (chartUI != null) chartUI.ToggleChart();
            });

            // Mobile Board & Plunder Button (appears dynamically when near a struck vessel)
            GameObject boardGo = new GameObject("BoardSurrenderedBtn");
            boardGo.transform.SetParent(parent, false);
            RectTransform boardRt = boardGo.AddComponent<RectTransform>();
            boardRt.anchorMin = new Vector2(0.5f, 0.2f);
            boardRt.anchorMax = new Vector2(0.5f, 0.2f);
            boardRt.pivot = new Vector2(0.5f, 0.5f);
            boardRt.anchoredPosition = new Vector2(0f, 0f);
            boardRt.sizeDelta = new Vector2(230f, 62f);

            Image boardImg = boardGo.AddComponent<Image>();
            boardImg.sprite = NauticalUITheme.ParchmentPanel;
            boardImg.color = new Color(1f, 0.94f, 0.65f);

            GameObject bTxtGo = new GameObject("BoardLabel");
            bTxtGo.transform.SetParent(boardGo.transform, false);
            RectTransform bTxtRt = bTxtGo.AddComponent<RectTransform>();
            bTxtRt.anchorMin = Vector2.zero;
            bTxtRt.anchorMax = Vector2.one;
            bTxtRt.sizeDelta = Vector2.zero;
            Text bt = bTxtGo.AddComponent<Text>();
            bt.text = "🏳️ BOARD & PLUNDER!";
            bt.font = NauticalUITheme.GetDefaultFont();
            bt.fontSize = 15;
            bt.fontStyle = FontStyle.Bold;
            bt.alignment = TextAnchor.MiddleCenter;
            bt.color = new Color(0.35f, 0.15f, 0.05f);
            bt.raycastTarget = false;

            Button boardBtn = boardGo.AddComponent<Button>();
            boardBtn.onClick.AddListener(TriggerNearbyBoarding);

            _boardButtonGo = boardGo;
            _boardButtonGo.SetActive(false);
        }

        private void TriggerNearbyBoarding()
        {
            var aiShips = FindObjectsByType<Plunderlust.AI.AIShipBrain>(FindObjectsInactive.Exclude);
            for (int i = 0; i < aiShips.Length; i++)
            {
                var ai = aiShips[i];
                if (ai != null && ai.IsSurrendered && !ai.IsPlundered)
                {
                    if (Vector3.Distance(transform.position, ai.transform.position) <= 22.0f)
                    {
                        ai.ExecuteBoardAndPlunder(gameObject);
                        if (_boardButtonGo != null) _boardButtonGo.SetActive(false);
                        break;
                    }
                }
            }
        }
    }
}
