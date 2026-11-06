using System.Collections.Generic;
using Plunderlust.Cargo;
using Plunderlust.Core.Data;
using Plunderlust.Crew;
using UnityEngine;

namespace Plunderlust.Port
{
    /// <summary>
    /// Interactive Port Haven and Smuggler Cove trading/hiring view.
    /// Allows the player to replenish lost Deckhands, hire specialized Officers,
    /// conduct material barter exchanges, and replace existing crew when quarters are full.
    /// Scaled with high-visibility, large pixel-art modal styling and zero runtime allocations.
    /// </summary>
    [DefaultExecutionOrder(110)]
    public class PortTavernView : MonoBehaviour
    {
        private static PortTavernView _instance;
        public static PortTavernView Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<PortTavernView>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("PortTavernView");
                        _instance = go.AddComponent<PortTavernView>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        private PortIslandHaven _activePortHaven;
        private SmugglerCoveOutpost _activeSmugglerCove;
        private bool _isOpen = false;
        private bool _isSmugglerMode = false;
        private int _openedFrame = -1;

        // Crew Replacement Sub-Modal State
        private bool _isReplacing = false;
        private CrewSpecialistSO _pendingOfficer = null;
        private int _pendingGoldCost = 0;
        private CargoItemType _pendingBarterType = CargoItemType.Gold;
        private int _pendingBarterCount = 0;
        private bool _pendingIsDeckhand = false;

        private CrewManager _playerCrew;
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
        public PortIslandHaven ActiveHaven => _activePortHaven;
        public SmugglerCoveOutpost ActiveCove => _activeSmugglerCove;

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
            FindPlayerReferences();
        }

        private void FindPlayerReferences()
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                _playerCrew = playerGo.GetComponent<CrewManager>();
                _playerCargo = playerGo.GetComponent<ShipCargoHold>();
            }
        }

        public void OpenPortHaven(PortIslandHaven haven)
        {
            FindPlayerReferences();
            _activePortHaven = haven;
            _activeSmugglerCove = null;
            _isSmugglerMode = false;
            _isOpen = true;
            _openedFrame = Time.frameCount;
            _isReplacing = false;
            _pendingOfficer = null;
        }

        public void OpenSmugglerCove(SmugglerCoveOutpost cove)
        {
            FindPlayerReferences();
            _activePortHaven = null;
            _activeSmugglerCove = cove;
            _isSmugglerMode = true;
            _isOpen = true;
            _openedFrame = Time.frameCount;
            _isReplacing = false;
            _pendingOfficer = null;
        }

        public void CloseView()
        {
            _isOpen = false;
            _isReplacing = false;
            _pendingOfficer = null;
            _activePortHaven = null;
            _activeSmugglerCove = null;
        }

        private void Update()
        {
            if (!_isOpen) return;
            // Prevent immediate toggle closed on the exact frame it opened via T key
            if (Time.frameCount == _openedFrame) return;

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.T))
            {
                if (_isReplacing)
                {
                    _isReplacing = false;
                    _pendingOfficer = null;
                }
                else
                {
                    CloseView();
                }
            }
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _bgTexture = MakeTex(new Color(0.05f, 0.07f, 0.11f, 0.98f));
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

            // Large, spacious dialog window
            float dialogWidth = 860f;
            float dialogHeight = 580f;
            float startX = (Screen.width - dialogWidth) * 0.5f;
            float startY = (Screen.height - dialogHeight) * 0.5f;

            Rect dialogRect = new Rect(startX, startY, dialogWidth, dialogHeight);
            GUI.DrawTexture(dialogRect, _bgTexture);

            // 1. Dialog Header & Close Button
            string headerText = _isSmugglerMode
                ? $"💀 {_activeSmugglerCove?.CoveName.ToUpper()} (BARTER COVE)"
                : $"⚓ {_activePortHaven?.PortName.ToUpper()} (TAVERN RECRUITMENT)";
            GUI.Label(new Rect(startX + 40f, startY + 14f, dialogWidth - 80f, 30f), headerText, _windowTitleStyle);

            if (GUI.Button(new Rect(startX + dialogWidth - 46f, startY + 12f, 34f, 30f), "X"))
            {
                CloseView();
            }

            // Treasury & Complete Cargo summary bar (Displays ALL commodities)
            int gold = _playerCargo != null ? _playerCargo.CurrentGold : 0;
            System.Text.StringBuilder invSb = new System.Text.StringBuilder();
            invSb.Append($"Your Purse: ✦ {gold} Gold  |  Cargo Hold: ");
            int nonZeroCommodities = 0;
            for (int i = 1; i <= 7; i++)
            {
                CargoItemType type = (CargoItemType)i;
                int count = _playerCargo != null ? _playerCargo.GetItemCount(type) : 0;
                if (count > 0)
                {
                    string icon = type switch
                    {
                        CargoItemType.Timber => "🪵",
                        CargoItemType.Canvas => "⛵",
                        CargoItemType.Cannonballs => "💣",
                        CargoItemType.Sugar => "🍬",
                        CargoItemType.Rum => "🍾",
                        CargoItemType.Spices => "🌿",
                        CargoItemType.TreasureMap => "🗺️",
                        _ => ""
                    };
                    if (nonZeroCommodities > 0) invSb.Append(", ");
                    invSb.Append($"{icon} {count} {type}");
                    nonZeroCommodities++;
                }
            }
            if (nonZeroCommodities == 0) invSb.Append("Empty (0 units)");
            GUI.Label(new Rect(startX + 24f, startY + 46f, dialogWidth - 48f, 22f), invSb.ToString(), _bodyStyle);

            // 2. Deckhand Manpower Section
            DrawDeckhandSection(startX + 24f, startY + 74f, dialogWidth - 48f);

            // 3. Officer Specialist Roster Section
            DrawOfficerRosterSection(startX + 24f, startY + 152f, dialogWidth - 48f);

            // 4. Commodity Trading Market Button
            if (GUI.Button(new Rect(startX + 24f, startY + dialogHeight - 48f, 220f, 36f), "⚖️ Open Commodity Market"))
            {
                PortCommodityMarket market = _isSmugglerMode
                    ? _activeSmugglerCove?.GetComponent<PortCommodityMarket>()
                    : _activePortHaven?.GetComponent<PortCommodityMarket>();
                string pName = _isSmugglerMode ? _activeSmugglerCove?.CoveName : _activePortHaven?.PortName;
                if (market != null && PortMarketView.Instance != null)
                {
                    CloseView();
                    PortMarketView.Instance.OpenMarket(market, pName);
                }
            }

            // 5. Weigh Anchor Close Button
            if (GUI.Button(new Rect(startX + dialogWidth - 210f, startY + dialogHeight - 48f, 190f, 36f), "Weigh Anchor"))
            {
                CloseView();
            }

            // 5. Overlay Replacement Modal (If active)
            if (_isReplacing)
            {
                DrawReplacementModal(startX, startY, dialogWidth, dialogHeight);
            }
        }

        private void DrawDeckhandSection(float x, float y, float width)
        {
            GUI.DrawTexture(new Rect(x, y, width, 68f), _cardBgTexture);

            int currentCrew = _playerCrew != null ? _playerCrew.CurrentDeckhands : 1;
            int maxCrew = _playerCrew != null ? _playerCrew.MaxCrewCapacity : 2;
            int minCrew = _playerCrew != null ? _playerCrew.MinRequiredCrew : 1;
            int currentOfficers = _playerCrew != null ? _playerCrew.AssignedOfficerCount : 0;
            int maxOfficers = _playerCrew != null ? _playerCrew.MaxOfficerCapacity : 1;

            string crewStatus = $"Deckhands: {currentCrew} / {maxCrew} (Min: {minCrew}) | Officer Berths: {currentOfficers} / {maxOfficers}";
            GUI.Label(new Rect(x + 16f, y + 10f, 440f, 22f), crewStatus, _subTitleStyle);

            string penaltyText = _playerCrew != null && _playerCrew.IsUndercrewed
                ? "<color=red>UNDERCREWED: -65% Maneuver, Reload & Bilge Pumping!</color>"
                : "<color=lime>Full Manpower: 100% Ship Handling Efficiency</color>";
            GUI.Label(new Rect(x + 16f, y + 36f, 420f, 22f), penaltyText, _bodyStyle);

            // Recruit / Replace Button
            bool isFull = currentCrew >= maxCrew;
            int goldCost = _isSmugglerMode ? _activeSmugglerCove.DeckhandGoldCost : _activePortHaven.DeckhandGoldCost;
            CargoItemType barterItem = _isSmugglerMode ? _activeSmugglerCove.DeckhandBarterType : CargoItemType.Gold;
            int barterCount = _isSmugglerMode ? _activeSmugglerCove.DeckhandBarterCount : 0;

            string btnText = isFull
                ? "Berths Full: Replace Crew"
                : (_isSmugglerMode ? $"Hire Deckhand ({goldCost}g + {barterCount} {barterItem})" : $"Hire Deckhand ({goldCost}g)");

            if (GUI.Button(new Rect(x + width - 280f, y + 14f, 268f, 40f), btnText))
            {
                if (!isFull)
                {
                    RecruitDeckhandAction(goldCost, barterItem, barterCount);
                }
                else
                {
                    TriggerReplacementMode(null, goldCost, barterItem, barterCount, true);
                }
            }
        }

        private void RecruitDeckhandAction(int goldCost, CargoItemType barterType, int barterCount)
        {
            if (_playerCargo == null || _playerCrew == null) return;

            if (_playerCargo.CurrentGold < goldCost)
            {
                Debug.LogWarning("[Tavern] Insufficient gold to recruit deckhand!");
                return;
            }

            if (_isSmugglerMode && barterCount > 0 && !_playerCargo.HasCargo(barterType, barterCount))
            {
                Debug.LogWarning($"[Smuggler Cove] Missing barter goods: {barterCount}x {barterType} required!");
                return;
            }

            _playerCargo.ConsumeCargo(CargoItemType.Gold, goldCost);
            if (_isSmugglerMode && barterCount > 0)
            {
                _playerCargo.ConsumeCargo(barterType, barterCount);
            }

            _playerCrew.RecruitDeckhands(1);
            Debug.Log("<color=green>[Tavern Recruitment]</color> Recruited 1 Deckhand!");
        }

        private void DrawOfficerRosterSection(float x, float y, float width)
        {
            GUI.Label(new Rect(x, y, width, 22f), "Available Officer Specialists for Hire:", _subTitleStyle);

            IReadOnlyList<CrewSpecialistSO> roster = _isSmugglerMode
                ? _activeSmugglerCove?.SmugglerRoster
                : _activePortHaven?.TavernRoster;

            if (roster == null || roster.Count == 0) return;

            float cardY = y + 26f;
            float cardHeight = 100f;

            for (int i = 0; i < roster.Count; i++)
            {
                CrewSpecialistSO officer = roster[i];
                if (officer == null) continue;

                Rect cardRect = new Rect(x, cardY, width, cardHeight);
                GUI.DrawTexture(cardRect, _cardBgTexture);

                // Specialist Title & Name
                string officerHeader = $"{officer.OfficerName}  [{officer.Tier} {officer.Specialty}]";
                GUI.Label(new Rect(x + 16f, cardY + 10f, 460f, 22f), officerHeader, _cardTitleStyle);

                // Passive Perk Description
                GUI.Label(new Rect(x + 16f, cardY + 34f, width - 260f, 54f), officer.GetEffectSummary(), _bodyStyle);

                // Cost & Hire / Replace Button
                int goldCost = _isSmugglerMode ? officer.SmugglerGoldCost : officer.BaseGoldCost;
                bool isAssigned = _playerCrew != null && _playerCrew.GetOfficer(officer.Specialty) != null;
                bool noFreeBerths = _playerCrew != null && !_playerCrew.HasFreeOfficerSlot;

                string costLabel = _isSmugglerMode
                    ? $"Cost: {goldCost}g + {officer.BarterMaterialCount} {officer.BarterMaterialType}"
                    : $"Cost: {goldCost} Gold";
                GUI.Label(new Rect(x + width - 260f, cardY + 12f, 246f, 22f), costLabel, _costStyle);

                string hireBtnText = isAssigned
                    ? "Replace Officer"
                    : (noFreeBerths ? "Berths Full: Replace" : "Hire Specialist");

                if (GUI.Button(new Rect(x + width - 190f, cardY + 44f, 180f, 42f), hireBtnText))
                {
                    if (!isAssigned && !noFreeBerths)
                    {
                        HireOfficerAction(officer, goldCost);
                    }
                    else
                    {
                        CargoItemType barterType = _isSmugglerMode ? officer.BarterMaterialType : CargoItemType.Gold;
                        int barterCount = _isSmugglerMode ? officer.BarterMaterialCount : 0;
                        TriggerReplacementMode(officer, goldCost, barterType, barterCount, false);
                    }
                }

                cardY += cardHeight + 10f;
            }
        }

        private void HireOfficerAction(CrewSpecialistSO officer, int goldCost)
        {
            if (_playerCargo == null || _playerCrew == null || officer == null) return;

            if (_playerCargo.CurrentGold < goldCost)
            {
                Debug.LogWarning("[Tavern] Not enough gold to hire this specialist!");
                return;
            }

            if (_isSmugglerMode && officer.BarterMaterialCount > 0)
            {
                if (!_playerCargo.HasCargo(officer.BarterMaterialType, officer.BarterMaterialCount))
                {
                    Debug.LogWarning($"[Smuggler Barter] You lack {officer.BarterMaterialCount}x {officer.BarterMaterialType} to hire this smuggler!");
                    return;
                }
            }

            // Deduct Gold and Barter Materials
            _playerCargo.ConsumeCargo(CargoItemType.Gold, goldCost);
            if (_isSmugglerMode && officer.BarterMaterialCount > 0)
            {
                _playerCargo.ConsumeCargo(officer.BarterMaterialType, officer.BarterMaterialCount);
            }

            _playerCrew.AssignOfficer(officer);
            Debug.Log($"<color=gold>[Officer Recruited]</color> {officer.OfficerName} is now your {officer.Specialty}!");
        }

        private void TriggerReplacementMode(CrewSpecialistSO officer, int goldCost, CargoItemType barterType, int barterCount, bool isDeckhand)
        {
            _isReplacing = true;
            _pendingOfficer = officer;
            _pendingGoldCost = goldCost;
            _pendingBarterType = barterType;
            _pendingBarterCount = barterCount;
            _pendingIsDeckhand = isDeckhand;
        }

        private void DrawReplacementModal(float startX, float startY, float dialogWidth, float dialogHeight)
        {
            float modalW = dialogWidth - 80f;
            float modalH = dialogHeight - 80f;
            float modalX = startX + 40f;
            float modalY = startY + 40f;

            Rect modalRect = new Rect(modalX, modalY, modalW, modalH);
            GUI.DrawTexture(modalRect, _cardBgTexture);
            GUI.Box(modalRect, GUIContent.none);

            string targetName = _pendingIsDeckhand ? "1 Fresh Deckhand" : $"{_pendingOfficer.OfficerName} ({_pendingOfficer.Specialty})";
            GUI.Label(new Rect(modalX + 20f, modalY + 16f, modalW - 40f, 28f), "⚠️ SHIP CREW QUARTERS FULL - SELECT CREW TO REPLACE", _cardTitleStyle);

            string subPrompt = $"Choose a crew member to dismiss from your vessel and replace with {targetName}:";
            GUI.Label(new Rect(modalX + 20f, modalY + 46f, modalW - 40f, 22f), subPrompt, _bodyStyle);

            float rowY = modalY + 76f;

            // Option 1: Dismiss Deckhand (if deckhands > 0)
            if (_playerCrew != null && _playerCrew.CurrentDeckhands > 0)
            {
                Rect rowRect = new Rect(modalX + 20f, rowY, modalW - 40f, 48f);
                GUI.DrawTexture(rowRect, _bgTexture);

                GUI.Label(new Rect(modalX + 32f, rowY + 14f, 320f, 22f), $"Deckhand Berth ({_playerCrew.CurrentDeckhands} currently assigned)", _subTitleStyle);

                if (GUI.Button(new Rect(modalX + modalW - 240f, rowY + 8f, 220f, 32f), "Dismiss Deckhand & Replace"))
                {
                    ExecuteReplacement(true, OfficerSpecialtyType.Carpenter);
                }
                rowY += 56f;
            }

            // Option 2+: Assigned Officers
            if (_playerCrew != null)
            {
                List<CrewSpecialistSO> assigned = _playerCrew.GetAssignedOfficers();
                for (int i = 0; i < assigned.Count; i++)
                {
                    CrewSpecialistSO off = assigned[i];
                    if (off == null) continue;

                    Rect rowRect = new Rect(modalX + 20f, rowY, modalW - 40f, 48f);
                    GUI.DrawTexture(rowRect, _bgTexture);

                    string offDesc = $"{off.OfficerName}  [{off.Tier} {off.Specialty}]";
                    GUI.Label(new Rect(modalX + 32f, rowY + 14f, 340f, 22f), offDesc, _cardTitleStyle);

                    if (GUI.Button(new Rect(modalX + modalW - 240f, rowY + 8f, 220f, 32f), $"Replace {off.Specialty}"))
                    {
                        ExecuteReplacement(false, off.Specialty);
                    }
                    rowY += 56f;
                }
            }

            // Cancel button
            if (GUI.Button(new Rect(modalX + (modalW - 200f) * 0.5f, modalY + modalH - 46f, 200f, 34f), "Cancel - Keep Crew"))
            {
                _isReplacing = false;
                _pendingOfficer = null;
            }
        }

        private void ExecuteReplacement(bool replaceDeckhand, OfficerSpecialtyType replaceOfficerType)
        {
            if (_playerCargo == null || _playerCrew == null) return;

            // Verify costs
            if (_playerCargo.CurrentGold < _pendingGoldCost)
            {
                Debug.LogWarning("[Tavern] Insufficient gold for replacement!");
                return;
            }

            if (_isSmugglerMode && _pendingBarterCount > 0 && !_playerCargo.HasCargo(_pendingBarterType, _pendingBarterCount))
            {
                Debug.LogWarning($"[Smuggler Cove] Missing barter goods: {_pendingBarterCount}x {_pendingBarterType} required!");
                return;
            }

            // 1. Remove the chosen replaced crew member
            if (replaceDeckhand)
            {
                _playerCrew.DismissDeckhand();
                Debug.Log("<color=yellow>[Crew Replaced]</color> A deckhand was dismissed to make room!");
            }
            else
            {
                _playerCrew.DismissOfficer(replaceOfficerType);
                Debug.Log($"<color=yellow>[Crew Replaced]</color> {replaceOfficerType} was dismissed to make room!");
            }

            // 2. Deduct resources
            _playerCargo.ConsumeCargo(CargoItemType.Gold, _pendingGoldCost);
            if (_isSmugglerMode && _pendingBarterCount > 0)
            {
                _playerCargo.ConsumeCargo(_pendingBarterType, _pendingBarterCount);
            }

            // 3. Hire new crew/officer
            if (_pendingIsDeckhand)
            {
                _playerCrew.RecruitDeckhands(1);
            }
            else if (_pendingOfficer != null)
            {
                _playerCrew.AssignOfficer(_pendingOfficer);
            }

            // 4. Close replacement modal
            _isReplacing = false;
            _pendingOfficer = null;
        }
    }
}
