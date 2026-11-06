using Plunderlust.Cargo;
using Plunderlust.Core.Data;
using Plunderlust.Ship;
using Plunderlust.Subsystems;
using UnityEngine;

namespace Plunderlust.Shipyard
{
    /// <summary>
    /// Interactive In-World Drydock & Shipyard UI.
    /// Provides drydock refit options for upgrading ship hulls (Rowboat -> Cutter -> Brigantine)
    /// and executing full repairs/overhauls of damaged subsystems in port.
    /// Rendered with large, crisp pixel-art styling.
    /// </summary>
    [DefaultExecutionOrder(110)]
    public class ShipyardView : MonoBehaviour
    {
        private static ShipyardView _instance;
        public static ShipyardView Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<ShipyardView>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("ShipyardView");
                        _instance = go.AddComponent<ShipyardView>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        private ShipyardDrydock _activeDrydock;
        private bool _isOpen = false;
        private int _openedFrame = -1;

        private GameObject _playerShip;
        private ShipSubsystemController _playerSubsystems;
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
        public ShipyardDrydock ActiveDrydock => _activeDrydock;

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
                _playerSubsystems = _playerShip.GetComponent<ShipSubsystemController>();
                _playerCargo = _playerShip.GetComponent<ShipCargoHold>();
            }
        }

        public void OpenDrydock(ShipyardDrydock drydock)
        {
            FindPlayerShip();
            _activeDrydock = drydock;
            _isOpen = true;
            _openedFrame = Time.frameCount;
        }

        public void CloseView()
        {
            _isOpen = false;
            _activeDrydock = null;
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

            _bgTexture = MakeTex(new Color(0.06f, 0.08f, 0.12f, 0.98f));
            _cardBgTexture = MakeTex(new Color(0.12f, 0.16f, 0.22f, 0.95f));

            _windowTitleStyle = new GUIStyle
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _windowTitleStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);

            _subTitleStyle = new GUIStyle
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _subTitleStyle.normal.textColor = Color.white;

            _cardTitleStyle = new GUIStyle
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _cardTitleStyle.normal.textColor = new Color(0.98f, 0.84f, 0.36f);

            _bodyStyle = new GUIStyle
            {
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            _bodyStyle.normal.textColor = new Color(0.88f, 0.90f, 0.92f);

            _costStyle = new GUIStyle
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            _costStyle.normal.textColor = new Color(0.35f, 1f, 0.45f);

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
            if (!_isOpen) return;

            GUI.depth = -100;
            InitStyles();

            float dialogWidth = 860f;
            float dialogHeight = 600f;
            float startX = (Screen.width - dialogWidth) * 0.5f;
            float startY = (Screen.height - dialogHeight) * 0.5f;

            Rect dialogRect = new Rect(startX, startY, dialogWidth, dialogHeight);
            GUI.DrawTexture(dialogRect, _bgTexture);

            // Title Header & Close Button
            string title = $"🛠️ {_activeDrydock?.ShipyardName.ToUpper()} (HULL REFIT & OVERHAUL)";
            GUI.Label(new Rect(startX + 40f, startY + 14f, dialogWidth - 80f, 30f), title, _windowTitleStyle);

            if (GUI.Button(new Rect(startX + dialogWidth - 46f, startY + 12f, 34f, 30f), "X"))
            {
                CloseView();
            }

            // Current Resources Bar
            int gold = _playerCargo != null ? _playerCargo.CurrentGold : 0;
            int timber = _playerCargo != null ? _playerCargo.GetItemCount(CargoItemType.Timber) : 0;
            int canvas = _playerCargo != null ? _playerCargo.GetItemCount(CargoItemType.Canvas) : 0;
            string resText = $"Shipwright Materials: ✦ {gold} Gold  |  🪵 {timber} Timber  |  ⛵ {canvas} Canvas";
            GUI.Label(new Rect(startX + 24f, startY + 46f, dialogWidth - 48f, 22f), resText, _bodyStyle);

            // 1. Current Flagship Status Card
            DrawCurrentFlagshipCard(startX + 24f, startY + 74f, dialogWidth - 48f);

            // 2. Full Hull Overhaul Button
            DrawOverhaulCard(startX + 24f, startY + 170f, dialogWidth - 48f);

            // 3. Available Hull Upgrade Section
            DrawUpgradeSection(startX + 24f, startY + 252f, dialogWidth - 48f);

            // Depart Slipway Button
            if (GUI.Button(new Rect(startX + dialogWidth - 230f, startY + dialogHeight - 46f, 210f, 36f), "Depart Drydock"))
            {
                CloseView();
            }
        }

        private void DrawCurrentFlagshipCard(float x, float y, float width)
        {
            GUI.DrawTexture(new Rect(x, y, width, 84f), _cardBgTexture);

            ShipTier currentTier = _playerSubsystems != null && _playerSubsystems.HullData != null
                ? _playerSubsystems.HullData.Tier
                : ShipTier.Tier1_LeakyRowboat;

            string tierName = currentTier switch
            {
                ShipTier.Tier1_LeakyRowboat => "Tier 1: Leaky Rowboat (Light Dinghy - 2.4m)",
                ShipTier.Tier2_CoastalCutter => "Tier 2: Coastal Cutter (Single-Mast Sloop - 4.8m)",
                ShipTier.Tier3_PirateBrigantine => "Tier 3: Pirate Brigantine (Two-Mast Warship - 9.0m)",
                ShipTier.Tier4_RoyalGalleon => "Tier 4: Royal Galleon (Three-Deck Flagship - 15.0m)",
                _ => currentTier.ToString()
            };

            GUI.Label(new Rect(x + 16f, y + 10f, 400f, 22f), $"Active Hull: {tierName}", _cardTitleStyle);

            float hull = _playerSubsystems != null ? _playerSubsystems.HullHealth : 100f;
            float maxHull = _playerSubsystems != null ? _playerSubsystems.MaxHullHealth : 100f;
            float bilge = _playerSubsystems != null ? _playerSubsystems.CurrentBilgeWater : 0f;
            int cannons = currentTier == ShipTier.Tier1_LeakyRowboat ? 1 : (currentTier == ShipTier.Tier2_CoastalCutter ? 4 : 8);
            int hold = _playerCargo != null ? _playerCargo.MaxCargoCapacity : 24;

            string statsLine = $"Hull Integrity: {hull:F0}/{maxHull:F0} HP  |  Bilge Flooding: {bilge:F0}L  |  Cannons: {cannons}  |  Hold: {hold} units";
            GUI.Label(new Rect(x + 16f, y + 36f, width - 32f, 20f), statsLine, _bodyStyle);

            string statusNotice = bilge > 10f || hull < maxHull * 0.9f
                ? "<color=orange>Status: Hull damage or bilge flooding detected. Drydock overhaul recommended.</color>"
                : "<color=lime>Status: Hull seaworthy and battle-ready.</color>";
            GUI.Label(new Rect(x + 16f, y + 58f, width - 32f, 20f), statusNotice, _bodyStyle);
        }

        private void DrawOverhaulCard(float x, float y, float width)
        {
            GUI.DrawTexture(new Rect(x, y, width, 70f), _cardBgTexture);

            GUI.Label(new Rect(x + 16f, y + 12f, 340f, 22f), "Complete Subsystem Overhaul & Bilge Pump", _subTitleStyle);
            GUI.Label(new Rect(x + 16f, y + 36f, width - 260f, 22f), "Fully restores Hull, Sails, Rudder & Guns to 100% and pumps bilge dry.", _bodyStyle);

            int overhaulCost = 45;
            GUI.Label(new Rect(x + width - 250f, y + 12f, 230f, 20f), $"Fee: {overhaulCost} Gold", _costStyle);

            if (GUI.Button(new Rect(x + width - 220f, y + 32f, 200f, 32f), "Execute Overhaul"))
            {
                if (ShipyardManager.Instance != null && _playerShip != null)
                {
                    ShipyardManager.Instance.RepairAllSubsystems(_playerShip, overhaulCost);
                }
            }
        }

        private void DrawUpgradeSection(float x, float y, float width)
        {
            GUI.Label(new Rect(x, y, width, 22f), "Available Hull Upgrades & Refits:", _subTitleStyle);

            ShipTier currentTier = _playerSubsystems != null && _playerSubsystems.HullData != null
                ? _playerSubsystems.HullData.Tier
                : ShipTier.Tier1_LeakyRowboat;

            float cardY = y + 26f;
            float cardHeight = 94f;

            // Tier 2 Coastal Cutter Upgrade
            if (currentTier < ShipTier.Tier2_CoastalCutter)
            {
                DrawUpgradeCard(x, cardY, width, cardHeight, ShipTier.Tier2_CoastalCutter,
                    "Coastal Cutter [Tier 2 Sloop - 4.8m Hull]",
                    "450 HP Hull, agile single-mast sloop, 4 Broadside Cannons (2 Port, 2 Starboard), 60 Cargo Hold, 2 Officer Berths.",
                    350, 8, 4);
                cardY += cardHeight + 10f;
            }

            // Tier 3 Pirate Brigantine Upgrade
            if (currentTier < ShipTier.Tier3_PirateBrigantine)
            {
                DrawUpgradeCard(x, cardY, width, cardHeight, ShipTier.Tier3_PirateBrigantine,
                    "Pirate Brigantine [Tier 3 Warship - 9.0m Hull]",
                    "900 HP Hull, twin-mast warship, 8 Heavy Broadside Cannons (4 Port, 4 Starboard), 140 Cargo Hold, 4 Officer Berths.",
                    900, 20, 12);
                cardY += cardHeight + 10f;
            }

            // Tier 4 Royal Galleon Upgrade
            if (currentTier < ShipTier.Tier4_RoyalGalleon && currentTier >= ShipTier.Tier2_CoastalCutter)
            {
                DrawUpgradeCard(x, cardY, width, cardHeight, ShipTier.Tier4_RoyalGalleon,
                    "Royal Galleon [Tier 4 Flagship - 15.0m Hull]",
                    "2000 HP Hull, 3-deck fortress, 16 Heavy Cannons (8 Port, 8 Starboard), 320 Cargo Hold, 8 Officer Berths.",
                    2200, 45, 25);
            }
            else if (currentTier >= ShipTier.Tier4_RoyalGalleon)
            {
                Rect maxRect = new Rect(x, cardY, width, 60f);
                GUI.DrawTexture(maxRect, _cardBgTexture);
                GUI.Label(new Rect(x + 20f, cardY + 20f, width - 40f, 24f), "👑 Flagship is at Maximum Hull Tier (Royal Galleon Three-Deck Fortress)!", _cardTitleStyle);
            }
        }

        private void DrawUpgradeCard(float x, float y, float width, float height, ShipTier tier, string title, string specs, int gold, int timber, int canvas)
        {
            Rect cardRect = new Rect(x, y, width, height);
            GUI.DrawTexture(cardRect, _cardBgTexture);

            GUI.Label(new Rect(x + 16f, y + 10f, 400f, 22f), title, _cardTitleStyle);
            GUI.Label(new Rect(x + 16f, y + 34f, width - 260f, 40f), specs, _bodyStyle);

            string costText = $"Cost: {gold}g + {timber} Timb + {canvas} Canv";
            GUI.Label(new Rect(x + width - 260f, y + 10f, 240f, 22f), costText, _costStyle);

            string missingReason = string.Empty;
            bool canUpgrade = ShipyardManager.Instance != null && ShipyardManager.Instance.CanUpgradeTo(_playerShip, tier, out missingReason);
            string btnLabel = canUpgrade ? "Refit & Upgrade Hull" : "Materials Lacking";

            GUI.enabled = canUpgrade;
            if (GUI.Button(new Rect(x + width - 220f, y + 42f, 200f, 38f), btnLabel))
            {
                if (ShipyardManager.Instance != null && _playerShip != null)
                {
                    ShipyardManager.Instance.ExecuteHullUpgrade(_playerShip, tier);
                    FindPlayerShip();
                }
            }
            GUI.enabled = true;

            if (!canUpgrade && !string.IsNullOrEmpty(missingReason))
            {
                GUI.Label(new Rect(x + 16f, y + 74f, width - 240f, 18f), $"<color=orange>{missingReason}</color>", _bodyStyle);
            }
        }
    }
}
