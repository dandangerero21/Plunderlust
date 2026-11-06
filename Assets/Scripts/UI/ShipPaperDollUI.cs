using Plunderlust.Cargo;
using Plunderlust.Combat;
using Plunderlust.Core.Data;
using Plunderlust.Crew;
using Plunderlust.Ship;
using Plunderlust.Subsystems;
using UnityEngine;
using UnityEngine.UI;

namespace Plunderlust.UI
{
    /// <summary>
    /// Production UGUI Ship Subsystem Paper-Doll Silhouette Widget.
    /// Positioned in the Top-Left Safe Area to replace prototype OnGUI text boxes.
    /// Visual Features:
    /// 1. Weathered Mahogany & Antique Brass Parchment frame.
    /// 2. Christened Ship Name and authentic Class plaque.
    /// 3. Central Top-Down Hull Silhouette: Color-lerps Gold (100%) -> Amber (50%) -> Crimson (20%).
    /// 4. Crossed Yardarm Sail Icon: Displays Sail % and deployment status.
    /// 5. Port & Starboard Gunwale Cannon Pips: Active vs Disabled cannon status dots.
    /// 6. Stern Rudder Tail: Directional deflection and integrity color.
    /// 7. Vertical Rising Bilge Wave Gauge.
    /// 8. Manpower Deckhand readout.
    /// Runs with zero per-frame heap allocations.
    /// </summary>
    public class ShipPaperDollUI : MonoBehaviour
    {
        public static ShipPaperDollUI Instance { get; private set; }

        [Header("Telemetry Source Tracking")]
        [SerializeField] private ShipLocomotionController _locomotion;
        [SerializeField] private ShipSubsystemController _subsystems;
        [SerializeField] private NavalCombatController _combat;
        [SerializeField] private ShipIdentitySystem _identity;
        [SerializeField] private CrewManager _crew;
        [SerializeField] private ShipCargoHold _cargo;

        // Runtime UI Elements
        private Canvas _canvas;
        private RectTransform _rootPanelRect;
        private Text _shipNameText;
        private Text _shipClassText;
        private Image _hullSilhouetteImg;
        private Image _sailIconImg;
        private Text _sailPercentText;
        private Image _rudderTailImg;
        private Image _bilgeFillImg;
        private Text _bilgeText;
        private Text _speedText;
        private Text _crewText;

        // Cannon Pips along Gunwales
        private Image[] _portPips = new Image[4];
        private Image[] _starboardPips = new Image[4];

        private float _waveAnimTimer = 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            FindPlayerShip();

            var existingCanvas = GameObject.Find("Plunderlust_PaperDollCanvas");
            if (existingCanvas != null)
            {
                DestroyImmediate(existingCanvas);
            }

            BuildPaperDollCanvas();
        }

        private void Start()
        {
            if (_subsystems == null) FindPlayerShip();
            UpdateIdentityLabels();
        }

        public void FindPlayerShip()
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                _locomotion = playerGo.GetComponent<ShipLocomotionController>();
                _subsystems = playerGo.GetComponent<ShipSubsystemController>();
                _combat = playerGo.GetComponent<NavalCombatController>();
                _crew = playerGo.GetComponent<CrewManager>();
                _cargo = playerGo.GetComponent<ShipCargoHold>();
                _identity = playerGo.GetComponent<ShipIdentitySystem>();
                if (_identity == null)
                {
                    _identity = playerGo.AddComponent<ShipIdentitySystem>();
                }
                _identity.OnShipRenamed += (name, className) => UpdateIdentityLabels();
            }
        }

        private void UpdateIdentityLabels()
        {
            if (_shipNameText != null && _identity != null)
            {
                _shipNameText.text = _identity.ChristenedName.ToUpper();
            }
            if (_shipClassText != null && _identity != null)
            {
                _shipClassText.text = _identity.ShipClassName;
            }
        }

        private void Update()
        {
            if (_subsystems == null) FindPlayerShip();
            if (_subsystems == null) return;

            float dt = Time.deltaTime;
            _waveAnimTimer += dt * 3.0f;

            // 1. Central Hull Silhouette Color-Lerp:
            // 100% = Brass Gold (#C59B27), 50% = Amber (#E5A93C), 20% = Crimson (#B22222)
            float hullRatio = _subsystems.HullIntegrityRatio;
            Color hullColor;
            if (hullRatio > 0.50f)
            {
                float t = (hullRatio - 0.50f) / 0.50f;
                hullColor = Color.Lerp(NauticalUITheme.AmberWarning, NauticalUITheme.BrassBorder, t);
            }
            else
            {
                float t = Mathf.Clamp01((hullRatio - 0.20f) / 0.30f);
                hullColor = Color.Lerp(NauticalUITheme.CrimsonBlood, NauticalUITheme.AmberWarning, t);
            }
            _hullSilhouetteImg.color = hullColor;

            // 2. Crossed Yardarm Sail Icon & Integrity %
            float sailRatio = _subsystems.SailsEfficiency;
            if (_sailPercentText != null)
            {
                _sailPercentText.text = $"{sailRatio * 100f:F0}%";
                _sailPercentText.color = sailRatio > 0.5f ? NauticalUITheme.ParchmentLight : NauticalUITheme.CrimsonBlood;
            }
            if (_sailIconImg != null)
            {
                _sailIconImg.color = sailRatio > 0.5f ? Color.white : NauticalUITheme.AmberWarning;
            }

            // 3. Stern Rudder Tail (deflection & health color)
            if (_rudderTailImg != null)
            {
                float rudderRatio = _subsystems.RudderEfficiency;
                Color rudderColor = rudderRatio > 0.5f ? NauticalUITheme.EmeraldSea : (rudderRatio > 0.25f ? NauticalUITheme.AmberWarning : NauticalUITheme.CrimsonBlood);
                _rudderTailImg.color = rudderColor;

                float steerAngle = (_locomotion != null) ? -_locomotion.SteerInput * 30f : 0f;
                _rudderTailImg.rectTransform.localRotation = Quaternion.Euler(0f, 0f, steerAngle);
            }

            // 4. Port & Starboard Cannon Pips
            UpdateCannonPips();

            // 5. Bilge Water Rising Wave Gauge
            float floodRatio = _subsystems.FloodingRatio;
            if (_bilgeFillImg != null)
            {
                _bilgeFillImg.fillAmount = floodRatio;
                // Subtle slosh wave pulse when flooded
                float pulse = (floodRatio > 0.05f) ? (0.85f + Mathf.Sin(_waveAnimTimer) * 0.15f) : 1.0f;
                _bilgeFillImg.color = new Color(NauticalUITheme.BilgeOcean.r * pulse, NauticalUITheme.BilgeOcean.g * pulse, NauticalUITheme.BilgeOcean.b * pulse, 0.95f);
            }
            if (_bilgeText != null)
            {
                if (floodRatio > 0.02f)
                {
                    _bilgeText.text = $"BILGE: {floodRatio * 100f:F0}%";
                    _bilgeText.color = (floodRatio > 0.6f) ? NauticalUITheme.CrimsonBlood : NauticalUITheme.ParchmentLight;
                }
                else
                {
                    _bilgeText.text = "BILGE: DRY";
                    _bilgeText.color = NauticalUITheme.EmeraldSea;
                }
            }

            // 6. Speed & Manpower Readouts
            if (_speedText != null && _locomotion != null)
            {
                float speedKts = _locomotion.CurrentSpeedKnots;
                string stateStr = _locomotion.CurrentSailState switch
                {
                    SailState.Anchored => "ANCH",
                    SailState.Rowing => "ROW",
                    SailState.BattleSails => "BATTLE",
                    SailState.FullCanvas => "FULL",
                    _ => ""
                };
                _speedText.text = $"{speedKts:F1} kts [{stateStr}]";
            }

            if (_crewText != null && _crew != null)
            {
                _crewText.text = $"CREW: {_crew.CurrentDeckhands}/{_crew.MaxCrewCapacity}";
            }
        }

        private void UpdateCannonPips()
        {
            if (_combat == null) return;

            int activePort = _combat.GetActivePortCannons();
            int totalPort = _combat.PortCannonsCount;
            for (int i = 0; i < _portPips.Length; i++)
            {
                if (_portPips[i] == null) continue;
                if (i < totalPort)
                {
                    _portPips[i].gameObject.SetActive(true);
                    _portPips[i].color = (i < activePort) ? NauticalUITheme.GoldAccent : NauticalUITheme.MahoganyDark;
                }
                else
                {
                    _portPips[i].gameObject.SetActive(false);
                }
            }

            int activeStar = _combat.GetActiveStarboardCannons();
            int totalStar = _combat.StarboardCannonsCount;
            for (int i = 0; i < _starboardPips.Length; i++)
            {
                if (_starboardPips[i] == null) continue;
                if (i < totalStar)
                {
                    _starboardPips[i].gameObject.SetActive(true);
                    _starboardPips[i].color = (i < activeStar) ? NauticalUITheme.GoldAccent : NauticalUITheme.MahoganyDark;
                }
                else
                {
                    _starboardPips[i].gameObject.SetActive(false);
                }
            }
        }

        // --- Procedural Canvas Hierarchy Construction ---

        public void BuildPaperDollCanvas()
        {
            GameObject canvasGo = new GameObject("Plunderlust_PaperDollCanvas");
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 45;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Safe Area Container
            GameObject safeAreaGo = new GameObject("SafeAreaContainer");
            safeAreaGo.transform.SetParent(canvasGo.transform, false);
            RectTransform safeAreaRect = safeAreaGo.AddComponent<RectTransform>();
            safeAreaRect.anchorMin = Vector2.zero;
            safeAreaRect.anchorMax = Vector2.one;
            safeAreaRect.offsetMin = Vector2.zero;
            safeAreaRect.offsetMax = Vector2.zero;
            safeAreaGo.AddComponent<SafeAreaFitter>();

            // Paper Doll Main Frame (Top-Left Corner - Expanded for mobile phones)
            GameObject panelGo = new GameObject("ShipPaperDollPanel");
            panelGo.transform.SetParent(safeAreaRect, false);
            _rootPanelRect = panelGo.AddComponent<RectTransform>();
            _rootPanelRect.anchorMin = new Vector2(0f, 1f);
            _rootPanelRect.anchorMax = new Vector2(0f, 1f);
            _rootPanelRect.pivot = new Vector2(0f, 1f);
            _rootPanelRect.anchoredPosition = new Vector2(24f, -20f);
            _rootPanelRect.sizeDelta = new Vector2(340f, 190f);

            Image panelBg = panelGo.AddComponent<Image>();
            panelBg.sprite = NauticalUITheme.MahoganyPanel;
            panelBg.type = Image.Type.Sliced;
            panelBg.color = Color.white;

            // 1. Header Plaque: Christened Ship Name & Authentic Class
            GameObject headerGo = new GameObject("ParchmentHeader");
            headerGo.transform.SetParent(panelGo.transform, false);
            RectTransform headerRt = headerGo.AddComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = new Vector2(0f, -6f);
            headerRt.sizeDelta = new Vector2(-16f, 42f);
            Image headerBg = headerGo.AddComponent<Image>();
            headerBg.sprite = NauticalUITheme.ParchmentPanel;
            headerBg.type = Image.Type.Sliced;
            headerBg.color = Color.white;

            // Ship Christened Name Text
            GameObject nameGo = new GameObject("ShipNameText");
            nameGo.transform.SetParent(headerGo.transform, false);
            RectTransform nameRt = nameGo.AddComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0.45f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.sizeDelta = Vector2.zero;
            _shipNameText = nameGo.AddComponent<Text>();
            _shipNameText.text = "THE BARNACLE";
            _shipNameText.font = NauticalUITheme.GetDefaultFont();
            _shipNameText.fontSize = 15;
            _shipNameText.fontStyle = FontStyle.Bold;
            _shipNameText.alignment = TextAnchor.MiddleCenter;
            _shipNameText.color = NauticalUITheme.MahoganyDark;

            // Ship Class Text
            GameObject classGo = new GameObject("ShipClassText");
            classGo.transform.SetParent(headerGo.transform, false);
            RectTransform classRt = classGo.AddComponent<RectTransform>();
            classRt.anchorMin = new Vector2(0f, 0f);
            classRt.anchorMax = new Vector2(1f, 0.48f);
            classRt.sizeDelta = Vector2.zero;
            _shipClassText = classGo.AddComponent<Text>();
            _shipClassText.text = "Harbor Skiff";
            _shipClassText.font = NauticalUITheme.GetDefaultFont();
            _shipClassText.fontSize = 12;
            _shipClassText.fontStyle = FontStyle.Italic;
            _shipClassText.alignment = TextAnchor.MiddleCenter;
            _shipClassText.color = NauticalUITheme.MahoganyMid;

            // 2. Central Silhouette Bay (Left Side)
            GameObject bayGo = new GameObject("SilhouetteBay");
            bayGo.transform.SetParent(panelGo.transform, false);
            RectTransform bayRt = bayGo.AddComponent<RectTransform>();
            bayRt.anchorMin = new Vector2(0f, 0f);
            bayRt.anchorMax = new Vector2(0f, 0f);
            bayRt.pivot = new Vector2(0.5f, 0.5f);
            bayRt.anchoredPosition = new Vector2(72f, 70f);
            bayRt.sizeDelta = new Vector2(95f, 125f);

            // Hull Silhouette
            GameObject hullGo = new GameObject("CentralHullSilhouette");
            hullGo.transform.SetParent(bayGo.transform, false);
            RectTransform hullRt = hullGo.AddComponent<RectTransform>();
            hullRt.anchorMin = Vector2.zero;
            hullRt.anchorMax = Vector2.one;
            hullRt.sizeDelta = Vector2.zero;
            _hullSilhouetteImg = hullGo.AddComponent<Image>();
            _hullSilhouetteImg.sprite = NauticalUITheme.ShipHullSilhouette;
            _hullSilhouetteImg.color = NauticalUITheme.BrassBorder;

            // Crossed Yardarms / Sail Icon (Center of Hull)
            GameObject sailGo = new GameObject("CrossedYardarmsSail");
            sailGo.transform.SetParent(hullGo.transform, false);
            RectTransform sailRt = sailGo.AddComponent<RectTransform>();
            sailRt.anchorMin = new Vector2(0.5f, 0.55f);
            sailRt.anchorMax = new Vector2(0.5f, 0.55f);
            sailRt.sizeDelta = new Vector2(40f, 40f);
            _sailIconImg = sailGo.AddComponent<Image>();
            _sailIconImg.sprite = NauticalUITheme.CrossedYardarms;
            _sailIconImg.color = Color.white;

            // Sail % Text Label inside Yardarms
            GameObject sailTxtGo = new GameObject("SailPercent");
            sailTxtGo.transform.SetParent(sailGo.transform, false);
            RectTransform sailTxtRt = sailTxtGo.AddComponent<RectTransform>();
            sailTxtRt.anchorMin = Vector2.zero;
            sailTxtRt.anchorMax = Vector2.one;
            sailTxtRt.sizeDelta = Vector2.zero;
            _sailPercentText = sailTxtGo.AddComponent<Text>();
            _sailPercentText.text = "100%";
            _sailPercentText.font = NauticalUITheme.GetDefaultFont();
            _sailPercentText.fontSize = 10;
            _sailPercentText.fontStyle = FontStyle.Bold;
            _sailPercentText.alignment = TextAnchor.MiddleCenter;
            _sailPercentText.color = NauticalUITheme.ParchmentLight;

            // Stern Rudder Tail (Bottom of Hull)
            GameObject rudderGo = new GameObject("SternRudderTail");
            rudderGo.transform.SetParent(hullGo.transform, false);
            RectTransform rudderRt = rudderGo.AddComponent<RectTransform>();
            rudderRt.anchorMin = new Vector2(0.5f, 0.05f);
            rudderRt.anchorMax = new Vector2(0.5f, 0.05f);
            rudderRt.pivot = new Vector2(0.5f, 1f);
            rudderRt.sizeDelta = new Vector2(18f, 24f);
            _rudderTailImg = rudderGo.AddComponent<Image>();
            _rudderTailImg.sprite = NauticalUITheme.RudderStern;
            _rudderTailImg.color = NauticalUITheme.EmeraldSea;

            // Gunwale Cannon Pips (Port & Starboard)
            BuildGunwalePips(hullGo.transform);

            // 3. Bilge Water Vertical Wave Gauge (Beside Silhouette)
            GameObject bilgeGo = new GameObject("BilgeGauge");
            bilgeGo.transform.SetParent(panelGo.transform, false);
            RectTransform bilgeRt = bilgeGo.AddComponent<RectTransform>();
            bilgeRt.anchorMin = new Vector2(0f, 0f);
            bilgeRt.anchorMax = new Vector2(0f, 0f);
            bilgeRt.pivot = new Vector2(0.5f, 0.5f);
            bilgeRt.anchoredPosition = new Vector2(138f, 70f);
            bilgeRt.sizeDelta = new Vector2(16f, 115f);

            // Gauge Background Groove
            Image bilgeBg = bilgeGo.AddComponent<Image>();
            bilgeBg.sprite = NauticalUITheme.SolidWhite;
            bilgeBg.color = new Color(0.05f, 0.05f, 0.05f, 0.85f);

            // Gauge Fill (Bottom-to-Top)
            GameObject fillGo = new GameObject("BilgeFill");
            fillGo.transform.SetParent(bilgeGo.transform, false);
            RectTransform fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.sizeDelta = Vector2.zero;
            _bilgeFillImg = fillGo.AddComponent<Image>();
            _bilgeFillImg.sprite = NauticalUITheme.SolidWhite;
            _bilgeFillImg.type = Image.Type.Filled;
            _bilgeFillImg.fillMethod = Image.FillMethod.Vertical;
            _bilgeFillImg.fillOrigin = (int)Image.OriginVertical.Bottom;
            _bilgeFillImg.fillAmount = 0.0f;
            _bilgeFillImg.color = NauticalUITheme.BilgeOcean;

            // 4. Right Status Readouts Area
            GameObject statsGo = new GameObject("StatusReadouts");
            statsGo.transform.SetParent(panelGo.transform, false);
            RectTransform statsRt = statsGo.AddComponent<RectTransform>();
            statsRt.anchorMin = new Vector2(0f, 0f);
            statsRt.anchorMax = new Vector2(1f, 0f);
            statsRt.pivot = new Vector2(0f, 0f);
            statsRt.anchoredPosition = new Vector2(164f, 14f);
            statsRt.sizeDelta = new Vector2(-174f, 120f);

            // Bilge Label
            _bilgeText = CreateStatLabel(statsGo.transform, "BilgeLabel", new Vector2(0f, 90f), "BILGE: DRY", NauticalUITheme.EmeraldSea);
            // Speed Label
            _speedText = CreateStatLabel(statsGo.transform, "SpeedLabel", new Vector2(0f, 58f), "0.0 kts [ANCH]", NauticalUITheme.ParchmentLight);
            // Crew Label
            _crewText = CreateStatLabel(statsGo.transform, "CrewLabel", new Vector2(0f, 26f), "CREW: 1/2", NauticalUITheme.GoldAccent);
        }

        private void BuildGunwalePips(Transform hullParent)
        {
            float[] yOffsets = { 20f, 5f, -10f, -25f };

            // Port Pips (Left side, x = -30)
            for (int i = 0; i < 4; i++)
            {
                GameObject pipGo = new GameObject($"PortPip_{i}");
                pipGo.transform.SetParent(hullParent, false);
                RectTransform rt = pipGo.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(-36f, yOffsets[i]);
                rt.sizeDelta = new Vector2(8f, 8f);
                _portPips[i] = pipGo.AddComponent<Image>();
                _portPips[i].sprite = NauticalUITheme.CannonPip;
                _portPips[i].color = NauticalUITheme.GoldAccent;
            }

            // Starboard Pips (Right side, x = +30)
            for (int i = 0; i < 4; i++)
            {
                GameObject pipGo = new GameObject($"StarboardPip_{i}");
                pipGo.transform.SetParent(hullParent, false);
                RectTransform rt = pipGo.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(36f, yOffsets[i]);
                rt.sizeDelta = new Vector2(8f, 8f);
                _starboardPips[i] = pipGo.AddComponent<Image>();
                _starboardPips[i].sprite = NauticalUITheme.CannonPip;
                _starboardPips[i].color = NauticalUITheme.GoldAccent;
            }
        }

        private Text CreateStatLabel(Transform parent, string name, Vector2 pos, string defaultText, Color color)
        {
            GameObject labelGo = new GameObject(name);
            labelGo.transform.SetParent(parent, false);
            RectTransform rt = labelGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(0f, 22f);

            Text t = labelGo.AddComponent<Text>();
            t.text = defaultText;
            t.font = NauticalUITheme.GetDefaultFont();
            t.fontSize = 13;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleLeft;
            t.color = color;
            return t;
        }
    }
}
