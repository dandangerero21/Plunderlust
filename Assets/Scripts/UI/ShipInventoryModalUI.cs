using System;
using Plunderlust.Cargo;
using Plunderlust.Core.Data;
using Plunderlust.Ship;
using UnityEngine;
using UnityEngine.UI;

namespace Plunderlust.UI
{
    /// <summary>
    /// Dedicated Mobile Touch Ship Cargo Hold & Inventory Modal.
    /// Provides:
    /// 1. Prominent HUD Quick-Access touch button displaying live Hold units & Gold doubloons.
    /// 2. Full-featured 17th-Century Nautical Cargo Manifest modal displaying:
    ///    - Treasury doubloons.
    ///    - Total hold capacity gauge (used vs max).
    ///    - Itemized grid for all 8 cargo commodities (Timber, Canvas, Iron Shot, Sugar, Rum, Spices, Silk, Treasure Maps).
    ///    - Christened Ship Class and Flagship identity banner.
    /// 3. Brass wax-seal [✕] touch close button and outside tap dismissal.
    /// Zero heap allocations in runtime update loops.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class ShipInventoryModalUI : MonoBehaviour
    {
        public static ShipInventoryModalUI Instance { get; private set; }

        private Canvas _canvas;
        private GameObject _modalRootGo;
        private GameObject _hudButtonGo;
        private RectTransform _modalPanelRect;

        // Header / Identity UI
        private Text _hudButtonText;
        private Text _shipTitleText;
        private Text _treasuryDoubloonText;
        private Text _capacityGaugeText;
        private Image _capacityGaugeFill;

        // 8 Item Commodity Cards
        private struct CommodityCard
        {
            public CargoItemType ItemType;
            public Text QuantityText;
            public Image CardBg;
            public Text ItemNameText;
        }

        private readonly CommodityCard[] _cards = new CommodityCard[8];

        private ShipCargoHold _cargoHold;
        private ShipIdentitySystem _identity;
        private bool _isOpen = false;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            FindPlayerReferences();
            BuildInventoryCanvas();
            SetModalOpen(false);
        }

        private void Start()
        {
            if (_cargoHold == null) FindPlayerReferences();
            SubscribeCargoEvents();
            RefreshHUDButton();
        }

        private void OnDestroy()
        {
            UnsubscribeCargoEvents();
        }

        public void FindPlayerReferences()
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                _cargoHold = playerGo.GetComponent<ShipCargoHold>();
                _identity = playerGo.GetComponent<ShipIdentitySystem>();
            }
        }

        private void SubscribeCargoEvents()
        {
            if (_cargoHold != null)
            {
                _cargoHold.OnCargoChanged += HandleCargoChanged;
                _cargoHold.OnGoldChanged += HandleGoldChanged;
            }
        }

        private void UnsubscribeCargoEvents()
        {
            if (_cargoHold != null)
            {
                _cargoHold.OnCargoChanged -= HandleCargoChanged;
                _cargoHold.OnGoldChanged -= HandleGoldChanged;
            }
        }

        private void HandleCargoChanged(CargoItemType type, int delta, int newTotal)
        {
            RefreshHUDButton();
            if (_isOpen) RefreshModalContent();
        }

        private void HandleGoldChanged(int delta, int newTotal)
        {
            RefreshHUDButton();
            if (_isOpen) RefreshModalContent();
        }

        public void ToggleModal()
        {
            SetModalOpen(!_isOpen);
        }

        public void SetModalOpen(bool open)
        {
            _isOpen = open;
            if (_modalRootGo != null)
            {
                _modalRootGo.SetActive(_isOpen);
            }

            if (_isOpen)
            {
                if (_cargoHold == null) FindPlayerReferences();
                RefreshModalContent();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.I))
            {
                ToggleModal();
            }

            if (_isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                SetModalOpen(false);
            }
        }

        private void RefreshHUDButton()
        {
            if (_hudButtonText == null) return;

            if (_cargoHold != null)
            {
                int gold = _cargoHold.CurrentGold;
                int used = _cargoHold.TotalCargoUnits;
                int max = _cargoHold.MaxCargoCapacity;
                _hudButtonText.text = $"📦 <b>HOLD</b> [{used}/{max}]  ✦ <b>{gold}g</b>";
            }
            else
            {
                _hudButtonText.text = "📦 <b>CARGO HOLD</b>";
            }
        }

        private void RefreshModalContent()
        {
            if (_cargoHold == null) FindPlayerReferences();
            if (_cargoHold == null) return;

            // 1. Christened Identity & Class
            if (_shipTitleText != null)
            {
                string title = (_identity != null) ? _identity.FullShipTitle : "HMS Flagship";
                _shipTitleText.text = $"FLAGSHIP MANIFEST • {title.ToUpper()}";
            }

            // 2. Treasury
            int currentGold = _cargoHold.CurrentGold;
            if (_treasuryDoubloonText != null)
            {
                _treasuryDoubloonText.text = $"🪙 {currentGold:N0} GOLD DOUBLOONS";
            }

            // 3. Capacity Gauge
            int usedUnits = _cargoHold.TotalCargoUnits;
            int maxCap = _cargoHold.MaxCargoCapacity;
            float ratio = (maxCap > 0) ? Mathf.Clamp01((float)usedUnits / maxCap) : 0f;

            if (_capacityGaugeText != null)
            {
                _capacityGaugeText.text = $"CARGO HOLD: {usedUnits} / {maxCap} UNITS  ({ratio * 100f:F0}% FULL)";
            }

            if (_capacityGaugeFill != null)
            {
                _capacityGaugeFill.fillAmount = ratio;
                _capacityGaugeFill.color = (ratio >= 1.0f)
                    ? NauticalUITheme.CrimsonBlood
                    : (ratio >= 0.75f ? NauticalUITheme.AmberWarning : NauticalUITheme.EmeraldSea);
            }

            // 4. Item Commodity Cards
            for (int i = 0; i < _cards.Length; i++)
            {
                CargoItemType type = _cards[i].ItemType;
                int count = _cargoHold.GetItemCount(type);

                if (_cards[i].QuantityText != null)
                {
                    _cards[i].QuantityText.text = (count > 0)
                        ? $"<b>x {count}</b>"
                        : "<color=#8C7B6B>Empty</color>";
                }

                if (_cards[i].CardBg != null)
                {
                    _cards[i].CardBg.color = (count > 0)
                        ? Color.white
                        : new Color(0.85f, 0.82f, 0.78f, 0.70f);
                }
            }
        }

        // --- Procedural Canvas Construction ---

        private void BuildInventoryCanvas()
        {
            GameObject canvasGo = new GameObject("Plunderlust_InventoryCanvas");
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 85; // Directly below Sea Chart (90)

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Safe Area Wrapper
            GameObject safeAreaGo = new GameObject("SafeAreaContainer");
            safeAreaGo.transform.SetParent(canvasGo.transform, false);
            RectTransform safeAreaRect = safeAreaGo.AddComponent<RectTransform>();
            safeAreaRect.anchorMin = Vector2.zero;
            safeAreaRect.anchorMax = Vector2.one;
            safeAreaRect.sizeDelta = Vector2.zero;
            safeAreaGo.AddComponent<SafeAreaFitter>();

            BuildHUDQuickButton(safeAreaRect);
            BuildModalWindow(safeAreaRect);
        }

        private void BuildHUDQuickButton(RectTransform parent)
        {
            _hudButtonGo = new GameObject("HUD_CargoButton");
            _hudButtonGo.transform.SetParent(parent, false);
            RectTransform btnRt = _hudButtonGo.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0f, 1f);
            btnRt.anchorMax = new Vector2(0f, 1f);
            btnRt.pivot = new Vector2(0f, 1f);
            btnRt.anchoredPosition = new Vector2(24f, -218f); // Positioned directly below expanded ShipPaperDoll panel
            btnRt.sizeDelta = new Vector2(214f, 44f);

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
            btn.onClick.AddListener(ToggleModal);

            GameObject textGo = new GameObject("ButtonText");
            textGo.transform.SetParent(_hudButtonGo.transform, false);
            RectTransform textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;

            _hudButtonText = textGo.AddComponent<Text>();
            _hudButtonText.text = "📦 <b>HOLD [0/20]  ✦ 100g</b>";
            _hudButtonText.font = NauticalUITheme.GetDefaultFont();
            _hudButtonText.fontSize = 13;
            _hudButtonText.alignment = TextAnchor.MiddleCenter;
            _hudButtonText.color = NauticalUITheme.ParchmentLight;
            _hudButtonText.raycastTarget = false;
        }

        private void BuildModalWindow(RectTransform parent)
        {
            _modalRootGo = new GameObject("InventoryModalRoot");
            _modalRootGo.transform.SetParent(parent, false);
            RectTransform rootRt = _modalRootGo.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.sizeDelta = Vector2.zero;

            // Semi-transparent backdrop overlay
            GameObject overlayGo = new GameObject("DarkOverlay");
            overlayGo.transform.SetParent(_modalRootGo.transform, false);
            RectTransform overlayRt = overlayGo.AddComponent<RectTransform>();
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.sizeDelta = Vector2.zero;
            Image overlayImg = overlayGo.AddComponent<Image>();
            overlayImg.sprite = NauticalUITheme.SolidWhite;
            overlayImg.color = new Color(0.04f, 0.03f, 0.02f, 0.72f);
            Button overlayBtn = overlayGo.AddComponent<Button>();
            overlayBtn.onClick.AddListener(() => SetModalOpen(false));

            // Main Modal Dialog Frame
            GameObject frameGo = new GameObject("ModalFrame");
            frameGo.transform.SetParent(_modalRootGo.transform, false);
            _modalPanelRect = frameGo.AddComponent<RectTransform>();
            _modalPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
            _modalPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _modalPanelRect.pivot = new Vector2(0.5f, 0.5f);
            _modalPanelRect.anchoredPosition = Vector2.zero;
            _modalPanelRect.sizeDelta = new Vector2(760f, 560f);

            Image frameImg = frameGo.AddComponent<Image>();
            frameImg.sprite = NauticalUITheme.MahoganyPanel;
            frameImg.type = Image.Type.Sliced;
            frameImg.color = Color.white;

            // 1. Header Banner
            GameObject headerGo = new GameObject("HeaderBanner");
            headerGo.transform.SetParent(frameGo.transform, false);
            RectTransform headerRt = headerGo.AddComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0.5f, 1f);
            headerRt.anchorMax = new Vector2(0.5f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = new Vector2(0f, -14f);
            headerRt.sizeDelta = new Vector2(700f, 52f);
            Image headerBg = headerGo.AddComponent<Image>();
            headerBg.sprite = NauticalUITheme.ParchmentPanel;
            headerBg.type = Image.Type.Sliced;

            GameObject titleTextGo = new GameObject("HeaderTitle");
            titleTextGo.transform.SetParent(headerGo.transform, false);
            RectTransform ttRt = titleTextGo.AddComponent<RectTransform>();
            ttRt.anchorMin = new Vector2(0f, 0.45f);
            ttRt.anchorMax = new Vector2(1f, 1f);
            ttRt.sizeDelta = Vector2.zero;
            Text titleTxt = titleTextGo.AddComponent<Text>();
            titleTxt.text = "SHIP'S CARGO HOLD & MANIFEST";
            titleTxt.font = NauticalUITheme.GetDefaultFont();
            titleTxt.fontSize = 16;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = NauticalUITheme.MahoganyDark;

            GameObject subTextGo = new GameObject("HeaderSubtitle");
            subTextGo.transform.SetParent(headerGo.transform, false);
            RectTransform stRt = subTextGo.AddComponent<RectTransform>();
            stRt.anchorMin = new Vector2(0f, 0f);
            stRt.anchorMax = new Vector2(1f, 0.45f);
            stRt.sizeDelta = Vector2.zero;
            _shipTitleText = subTextGo.AddComponent<Text>();
            _shipTitleText.text = "FLAGSHIP MANIFEST • HARBOR SKIFF";
            _shipTitleText.font = NauticalUITheme.GetDefaultFont();
            _shipTitleText.fontSize = 11;
            _shipTitleText.fontStyle = FontStyle.Bold;
            _shipTitleText.alignment = TextAnchor.MiddleCenter;
            _shipTitleText.color = NauticalUITheme.BrassDark;

            // 2. Top-Right Close Wax-Seal Button [✕]
            GameObject closeGo = new GameObject("CloseWaxSealBtn");
            closeGo.transform.SetParent(frameGo.transform, false);
            RectTransform closeRt = closeGo.AddComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-12f, -12f);
            closeRt.sizeDelta = new Vector2(52f, 52f);

            Image closeImg = closeGo.AddComponent<Image>();
            closeImg.sprite = NauticalUITheme.CircleWaxSeal;
            closeImg.color = Color.white;

            GameObject xGo = new GameObject("CloseX");
            xGo.transform.SetParent(closeGo.transform, false);
            RectTransform xRt = xGo.AddComponent<RectTransform>();
            xRt.anchorMin = Vector2.zero;
            xRt.anchorMax = Vector2.one;
            xRt.sizeDelta = Vector2.zero;
            Text xText = xGo.AddComponent<Text>();
            xText.text = "✕";
            xText.font = NauticalUITheme.GetDefaultFont();
            xText.fontSize = 22;
            xText.fontStyle = FontStyle.Bold;
            xText.alignment = TextAnchor.MiddleCenter;
            xText.color = NauticalUITheme.GoldAccent;
            xText.raycastTarget = false;

            Button closeBtn = closeGo.AddComponent<Button>();
            closeBtn.onClick.AddListener(() => SetModalOpen(false));

            // 3. Treasury Doubloons Box
            GameObject goldGo = new GameObject("TreasuryBox");
            goldGo.transform.SetParent(frameGo.transform, false);
            RectTransform goldRt = goldGo.AddComponent<RectTransform>();
            goldRt.anchorMin = new Vector2(0f, 1f);
            goldRt.anchorMax = new Vector2(0.5f, 1f);
            goldRt.pivot = new Vector2(0f, 1f);
            goldRt.anchoredPosition = new Vector2(30f, -74f);
            goldRt.sizeDelta = new Vector2(-40f, 44f);
            Image goldBg = goldGo.AddComponent<Image>();
            goldBg.sprite = NauticalUITheme.ParchmentPanel;
            goldBg.type = Image.Type.Sliced;

            GameObject goldTxtGo = new GameObject("GoldText");
            goldTxtGo.transform.SetParent(goldGo.transform, false);
            RectTransform gtRt = goldTxtGo.AddComponent<RectTransform>();
            gtRt.anchorMin = Vector2.zero;
            gtRt.anchorMax = Vector2.one;
            gtRt.sizeDelta = Vector2.zero;
            _treasuryDoubloonText = goldTxtGo.AddComponent<Text>();
            _treasuryDoubloonText.text = "🪙 100 GOLD DOUBLOONS";
            _treasuryDoubloonText.font = NauticalUITheme.GetDefaultFont();
            _treasuryDoubloonText.fontSize = 13;
            _treasuryDoubloonText.fontStyle = FontStyle.Bold;
            _treasuryDoubloonText.alignment = TextAnchor.MiddleCenter;
            _treasuryDoubloonText.color = NauticalUITheme.MahoganyDark;

            // 4. Capacity Gauge Box
            GameObject capGo = new GameObject("CapacityBox");
            capGo.transform.SetParent(frameGo.transform, false);
            RectTransform capRt = capGo.AddComponent<RectTransform>();
            capRt.anchorMin = new Vector2(0.5f, 1f);
            capRt.anchorMax = new Vector2(1f, 1f);
            capRt.pivot = new Vector2(0.5f, 1f);
            capRt.anchoredPosition = new Vector2(10f, -74f);
            capRt.sizeDelta = new Vector2(-40f, 44f);
            Image capBg = capGo.AddComponent<Image>();
            capBg.sprite = NauticalUITheme.ParchmentPanel;
            capBg.type = Image.Type.Sliced;

            // Bar background & fill
            GameObject barFillGo = new GameObject("Fill");
            barFillGo.transform.SetParent(capGo.transform, false);
            RectTransform fillRt = barFillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0.04f, 0.12f);
            fillRt.anchorMax = new Vector2(0.96f, 0.38f);
            fillRt.sizeDelta = Vector2.zero;
            _capacityGaugeFill = barFillGo.AddComponent<Image>();
            _capacityGaugeFill.sprite = NauticalUITheme.SolidWhite;
            _capacityGaugeFill.type = Image.Type.Filled;
            _capacityGaugeFill.fillMethod = Image.FillMethod.Horizontal;
            _capacityGaugeFill.fillAmount = 0.5f;
            _capacityGaugeFill.color = NauticalUITheme.EmeraldSea;

            GameObject capTxtGo = new GameObject("CapText");
            capTxtGo.transform.SetParent(capGo.transform, false);
            RectTransform ctRt = capTxtGo.AddComponent<RectTransform>();
            ctRt.anchorMin = new Vector2(0f, 0.35f);
            ctRt.anchorMax = new Vector2(1f, 1f);
            ctRt.sizeDelta = Vector2.zero;
            _capacityGaugeText = capTxtGo.AddComponent<Text>();
            _capacityGaugeText.text = "HOLD: 0 / 20 UNITS (0% FULL)";
            _capacityGaugeText.font = NauticalUITheme.GetDefaultFont();
            _capacityGaugeText.fontSize = 11;
            _capacityGaugeText.fontStyle = FontStyle.Bold;
            _capacityGaugeText.alignment = TextAnchor.MiddleCenter;
            _capacityGaugeText.color = NauticalUITheme.MahoganyDark;

            // 5. Commodity 8-Card Grid (4 Columns x 2 Rows)
            BuildCommodityGrid(frameGo.transform);
        }

        private void BuildCommodityGrid(Transform parent)
        {
            // Grid Container
            GameObject gridGo = new GameObject("CommodityGridContainer");
            gridGo.transform.SetParent(parent, false);
            RectTransform gridRt = gridGo.AddComponent<RectTransform>();
            gridRt.anchorMin = new Vector2(0.04f, 0.05f);
            gridRt.anchorMax = new Vector2(0.96f, 0.74f);
            gridRt.sizeDelta = Vector2.zero;

            // 8 Cargo Items metadata: Type, Icon, Name, Role
            (CargoItemType type, string icon, string name, string role)[] items = new (CargoItemType, string, string, string)[]
            {
                (CargoItemType.Timber,       "🪵", "Timber",       "Hull Repairs (Carpenter)"),
                (CargoItemType.Canvas,       "⛵", "Canvas",       "Rigging (Sailmaker)"),
                (CargoItemType.Cannonballs,  "💣", "Iron Shot",    "Gun Deck Ordnance"),
                (CargoItemType.Sugar,        "🍬", "Sugar",        "Merchant Guild Cargo"),
                (CargoItemType.Rum,          "🍾", "Fine Rum",     "Corsair Tavern Spirits"),
                (CargoItemType.Spices,       "🌶️", "Rare Spices",  "High-Value Trade Cargo"),
                (CargoItemType.Silk,         "🧵", "Imperial Silk","Luxury Trade Cargo"),
                (CargoItemType.TreasureMap,  "🗺️", "Treasure Map", "Charts to Hidden Atolls")
            };

            const int cols = 4;
            const int rows = 2;
            const float padX = 10f;
            const float padY = 10f;

            for (int i = 0; i < items.Length; i++)
            {
                int c = i % cols;
                int r = i / cols;

                float xMin = (float)c / cols;
                float xMax = (float)(c + 1) / cols;
                float yMin = 1f - ((float)(r + 1) / rows);
                float yMax = 1f - ((float)r / rows);

                GameObject cardGo = new GameObject($"Card_{items[i].name}");
                cardGo.transform.SetParent(gridGo.transform, false);
                RectTransform cardRt = cardGo.AddComponent<RectTransform>();
                cardRt.anchorMin = new Vector2(xMin, yMin);
                cardRt.anchorMax = new Vector2(xMax, yMax);
                cardRt.offsetMin = new Vector2(padX * 0.5f, padY * 0.5f);
                cardRt.offsetMax = new Vector2(-padX * 0.5f, -padY * 0.5f);

                Image cardBg = cardGo.AddComponent<Image>();
                cardBg.sprite = NauticalUITheme.ParchmentPanel;
                cardBg.type = Image.Type.Sliced;
                cardBg.color = Color.white;

                // Emoji Icon
                GameObject iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(cardGo.transform, false);
                RectTransform iconRt = iconGo.AddComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.5f, 1f);
                iconRt.anchorMax = new Vector2(0.5f, 1f);
                iconRt.pivot = new Vector2(0.5f, 1f);
                iconRt.anchoredPosition = new Vector2(0f, -8f);
                iconRt.sizeDelta = new Vector2(48f, 40f);
                Text iconTxt = iconGo.AddComponent<Text>();
                iconTxt.text = items[i].icon;
                iconTxt.font = NauticalUITheme.GetDefaultFont();
                iconTxt.fontSize = 28;
                iconTxt.alignment = TextAnchor.MiddleCenter;

                // Commodity Name
                GameObject nameGo = new GameObject("Name");
                nameGo.transform.SetParent(cardGo.transform, false);
                RectTransform nameRt = nameGo.AddComponent<RectTransform>();
                nameRt.anchorMin = new Vector2(0.05f, 0.44f);
                nameRt.anchorMax = new Vector2(0.95f, 0.68f);
                nameRt.sizeDelta = Vector2.zero;
                Text nameTxt = nameGo.AddComponent<Text>();
                nameTxt.text = items[i].name.ToUpper();
                nameTxt.font = NauticalUITheme.GetDefaultFont();
                nameTxt.fontSize = 11;
                nameTxt.fontStyle = FontStyle.Bold;
                nameTxt.alignment = TextAnchor.MiddleCenter;
                nameTxt.color = NauticalUITheme.MahoganyDark;

                // Quantity
                GameObject qtyGo = new GameObject("Quantity");
                qtyGo.transform.SetParent(cardGo.transform, false);
                RectTransform qtyRt = qtyGo.AddComponent<RectTransform>();
                qtyRt.anchorMin = new Vector2(0.05f, 0.22f);
                qtyRt.anchorMax = new Vector2(0.95f, 0.46f);
                qtyRt.sizeDelta = Vector2.zero;
                Text qtyTxt = qtyGo.AddComponent<Text>();
                qtyTxt.text = "Empty";
                qtyTxt.font = NauticalUITheme.GetDefaultFont();
                qtyTxt.fontSize = 13;
                qtyTxt.fontStyle = FontStyle.Bold;
                qtyTxt.alignment = TextAnchor.MiddleCenter;
                qtyTxt.color = NauticalUITheme.InkBlack;

                // Role/Description
                GameObject roleGo = new GameObject("Role");
                roleGo.transform.SetParent(cardGo.transform, false);
                RectTransform roleRt = roleGo.AddComponent<RectTransform>();
                roleRt.anchorMin = new Vector2(0.05f, 0.04f);
                roleRt.anchorMax = new Vector2(0.95f, 0.24f);
                roleRt.sizeDelta = Vector2.zero;
                Text roleTxt = roleGo.AddComponent<Text>();
                roleTxt.text = items[i].role;
                roleTxt.font = NauticalUITheme.GetDefaultFont();
                roleTxt.fontSize = 8;
                roleTxt.alignment = TextAnchor.MiddleCenter;
                roleTxt.color = NauticalUITheme.BrassDark;

                _cards[i] = new CommodityCard
                {
                    ItemType = items[i].type,
                    QuantityText = qtyTxt,
                    CardBg = cardBg,
                    ItemNameText = nameTxt
                };
            }
        }
    }
}
