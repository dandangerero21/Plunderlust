using Plunderlust.Cargo;
using Plunderlust.Core.Data;
using UnityEngine;

namespace Plunderlust.Port
{
    /// <summary>
    /// Interactive In-World Commodity Trading Market View.
    /// Allows the player to buy provisions and shipwright materials (Timber, Canvas, Cannonballs)
    /// and sell harvested cargo (Sugar, Rum, Spices, Treasure Maps) according to regional supply and demand.
    /// </summary>
    [DefaultExecutionOrder(110)]
    public class PortMarketView : MonoBehaviour
    {
        private static PortMarketView _instance;
        public static PortMarketView Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<PortMarketView>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("PortMarketView");
                        _instance = go.AddComponent<PortMarketView>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        private PortCommodityMarket _activeMarket;
        private string _portTitle = "Port Market";
        private bool _isOpen = false;
        private int _openedFrame = -1;

        private GameObject _playerShip;
        private ShipCargoHold _playerCargo;

        // UI Styles
        private GUIStyle _windowTitleStyle;
        private GUIStyle _subTitleStyle;
        private GUIStyle _cardTitleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _costStyle;
        private Texture2D _bgTexture;
        private Texture2D _cardBgTexture;
        private bool _stylesInitialized = false;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Start()
        {
            FindPlayerShip();
        }

        private void FindPlayerShip()
        {
            _playerShip = GameObject.FindGameObjectWithTag("Player");
            if (_playerShip != null)
            {
                _playerCargo = _playerShip.GetComponent<ShipCargoHold>();
            }
        }

        public void OpenMarket(PortCommodityMarket market, string portName)
        {
            FindPlayerShip();
            _activeMarket = market;
            _portTitle = portName;
            _isOpen = true;
            _openedFrame = Time.frameCount;
        }

        public void CloseView()
        {
            _isOpen = false;
            _activeMarket = null;
        }

        private void Update()
        {
            if (!_isOpen) return;
            if (Time.frameCount == _openedFrame) return;

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.T))
            {
                CloseView();
            }
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            // 17th-Century Nautical Palette: Mahogany (#1B140E), Parchment (#D8C39E), Antique Brass (#C59B27)
            _bgTexture = MakeTex(new Color(0.106f, 0.078f, 0.055f, 0.98f));
            _cardBgTexture = MakeTex(new Color(0.18f, 0.14f, 0.10f, 0.95f));

            _windowTitleStyle = new GUIStyle
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _windowTitleStyle.normal.textColor = new Color(0.77f, 0.61f, 0.15f); // Antique Brass

            _subTitleStyle = new GUIStyle
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _subTitleStyle.normal.textColor = new Color(0.85f, 0.76f, 0.62f); // Parchment

            _cardTitleStyle = new GUIStyle
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _cardTitleStyle.normal.textColor = new Color(0.77f, 0.61f, 0.15f);

            _bodyStyle = new GUIStyle
            {
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            _bodyStyle.normal.textColor = new Color(0.85f, 0.76f, 0.62f);

            _costStyle = new GUIStyle
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            _costStyle.normal.textColor = new Color(0.12f, 0.55f, 0.40f); // Emerald Sea

            _stylesInitialized = true;
        }

        private Texture2D MakeTex(Color col)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            if (!_isOpen || _activeMarket == null) return;

            GUI.depth = -105;
            InitStyles();

            float dialogWidth = 860f;
            float dialogHeight = 580f;
            float startX = (Screen.width - dialogWidth) * 0.5f;
            float startY = (Screen.height - dialogHeight) * 0.5f;

            Rect dialogRect = new Rect(startX, startY, dialogWidth, dialogHeight);
            GUI.DrawTexture(dialogRect, _bgTexture);

            // Title & Close Button
            string title = $"⚖️ {_portTitle.ToUpper()} (COMMODITY EXCHANGE)";
            GUI.Label(new Rect(startX + 40f, startY + 14f, dialogWidth - 80f, 30f), title, _windowTitleStyle);

            if (GUI.Button(new Rect(startX + dialogWidth - 46f, startY + 12f, 34f, 30f), "X"))
            {
                CloseView();
            }

            // Purse & Cargo Hold Telemetry
            int gold = _playerCargo != null ? _playerCargo.CurrentGold : 0;
            int usedHold = _playerCargo != null ? _playerCargo.TotalCargoUnits : 0;
            int maxHold = _playerCargo != null ? _playerCargo.MaxCargoCapacity : 24;

            string purseText = $"Your Purse: ✦ {gold} Gold  |  Cargo Hold Space: {usedHold} / {maxHold} Units";
            GUI.Label(new Rect(startX + 24f, startY + 46f, dialogWidth - 48f, 22f), purseText, _bodyStyle);

            // Column Header
            float listY = startY + 74f;
            GUI.Label(new Rect(startX + 36f, listY, 200f, 20f), "Commodity", _subTitleStyle);
            GUI.Label(new Rect(startX + 220f, listY, 80f, 20f), "Port Stock", _subTitleStyle);
            GUI.Label(new Rect(startX + 320f, listY, 100f, 20f), "Buy Price", _subTitleStyle);
            GUI.Label(new Rect(startX + 430f, listY, 100f, 20f), "Sell Payout", _subTitleStyle);
            GUI.Label(new Rect(startX + 550f, listY, 80f, 20f), "Your Hold", _subTitleStyle);
            GUI.Label(new Rect(startX + 650f, listY, 170f, 20f), "Trade Actions", _subTitleStyle);

            listY += 24f;
            float rowHeight = 44f;

            var items = _activeMarket.Commodities;
            for (int i = 0; i < items.Count; i++)
            {
                CommodityPrice item = items[i];
                Rect rowRect = new Rect(startX + 24f, listY, dialogWidth - 48f, rowHeight);
                GUI.DrawTexture(rowRect, _cardBgTexture);

                string icon = item.itemType switch
                {
                    CargoItemType.Timber => "🪵",
                    CargoItemType.Canvas => "⛵",
                    CargoItemType.Cannonballs => "💣",
                    CargoItemType.Sugar => "🍬",
                    CargoItemType.Rum => "🍾",
                    CargoItemType.Spices => "🌿",
                    CargoItemType.Silk => "🧣",
                    CargoItemType.TreasureMap => "🗺️",
                    _ => "📦"
                };

                // Commodity Name & High Demand Tag
                string nameText = $"{icon} {item.itemType}";
                if (item.isHighDemand) nameText += " <color=lime>[HIGH DEMAND]</color>";
                GUI.Label(new Rect(startX + 36f, listY + 12f, 180f, 22f), nameText, _bodyStyle);

                // Port Stock
                GUI.Label(new Rect(startX + 224f, listY + 12f, 80f, 22f), $"{item.stockCount}x", _bodyStyle);

                // Buy Price (Port sells to player)
                GUI.Label(new Rect(startX + 320f, listY + 12f, 90f, 22f), $"✦ {item.buyPrice}g", _costStyle);

                // Sell Payout (Port buys from player)
                GUI.Label(new Rect(startX + 430f, listY + 12f, 90f, 22f), $"✦ {item.sellPrice}g", _costStyle);

                // Player Hold Count
                int inHold = _playerCargo != null ? _playerCargo.GetItemCount(item.itemType) : 0;
                GUI.Label(new Rect(startX + 550f, listY + 12f, 80f, 22f), $"{inHold} in hold", _bodyStyle);

                // Trade Buttons
                bool canBuy = gold >= item.buyPrice && usedHold < maxHold && item.stockCount > 0;
                bool canSell = inHold > 0;

                // [Buy 1]
                GUI.enabled = canBuy;
                if (GUI.Button(new Rect(startX + 650f, listY + 8f, 54f, 28f), "+1"))
                {
                    _activeMarket.BuyCommodity(_playerCargo, item.itemType, 1, out _);
                }

                // [Sell 1]
                GUI.enabled = canSell;
                if (GUI.Button(new Rect(startX + 710f, listY + 8f, 54f, 28f), "-1"))
                {
                    _activeMarket.SellCommodity(_playerCargo, item.itemType, 1, out _);
                }

                // [Sell All]
                if (GUI.Button(new Rect(startX + 770f, listY + 8f, 68f, 28f), "Sell All"))
                {
                    _activeMarket.SellCommodity(_playerCargo, item.itemType, inHold, out _);
                }
                GUI.enabled = true;

                listY += rowHeight + 6f;
            }

            // Bottom Return Button
            if (GUI.Button(new Rect(startX + dialogWidth - 210f, startY + dialogHeight - 46f, 190f, 36f), "Return to Sea"))
            {
                CloseView();
            }
        }
    }
}
