using System.Collections.Generic;
using Plunderlust.Cargo;
using Plunderlust.Core.Data;
using Plunderlust.Crew;
using Plunderlust.Quests;
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
        private bool _isBountyBoardOpen = false;
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
        private GUIStyle _btnStyle;
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
            _isBountyBoardOpen = false;
            _pendingOfficer = null;

            if (BountyQuestManager.Instance != null && BountyQuestManager.Instance.AvailableContracts.Count == 0)
            {
                BountyQuestManager.Instance.RefreshAvailableBounties(haven.transform.position);
            }
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
            _isBountyBoardOpen = false;
            _pendingOfficer = null;

            if (BountyQuestManager.Instance != null && BountyQuestManager.Instance.AvailableContracts.Count == 0)
            {
                BountyQuestManager.Instance.RefreshAvailableBounties(cove.transform.position);
            }
        }

        public void CloseView()
        {
            _isOpen = false;
            _isReplacing = false;
            _isBountyBoardOpen = false;
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
                if (_isBountyBoardOpen)
                {
                    _isBountyBoardOpen = false;
                }
                else if (_isReplacing)
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
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _windowTitleStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);

            _subTitleStyle = new GUIStyle
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _subTitleStyle.normal.textColor = Color.white;

            _cardTitleStyle = new GUIStyle
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _cardTitleStyle.normal.textColor = new Color(0.98f, 0.84f, 0.36f);

            _bodyStyle = new GUIStyle
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            _bodyStyle.normal.textColor = new Color(0.88f, 0.90f, 0.92f);

            _costStyle = new GUIStyle
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            _costStyle.normal.textColor = new Color(0.35f, 1f, 0.45f);

            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

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
            if (UI.SeaChartMapUI.Instance != null && UI.SeaChartMapUI.Instance.IsOpen) return;

            GUI.depth = -100;
            InitStyles();

            // Responsive large dialog window for mobile readability
            float dialogWidth = Mathf.Min(Screen.width * 0.94f, 1020f);
            float dialogHeight = Mathf.Min(Screen.height * 0.92f, 680f);
            float startX = (Screen.width - dialogWidth) * 0.5f;
            float startY = (Screen.height - dialogHeight) * 0.5f;

            // 1. Overlay Bounty Noticeboard Modal (If active, draw and return early to prevent click bleed)
            if (_isBountyBoardOpen)
            {
                DrawBountyBoardModal(startX, startY, dialogWidth, dialogHeight);
                return;
            }

            // 2. Overlay Replacement Modal (If active, draw and return early)
            if (_isReplacing)
            {
                DrawReplacementModal(startX, startY, dialogWidth, dialogHeight);
                return;
            }

            Rect dialogRect = new Rect(startX, startY, dialogWidth, dialogHeight);
            GUI.DrawTexture(dialogRect, _bgTexture);

            // Dialog Header & Close Button
            string headerText = _isSmugglerMode
                ? $"{_activeSmugglerCove?.CoveName.ToUpper()} (BARTER COVE)"
                : $"{_activePortHaven?.PortName.ToUpper()} (TAVERN RECRUITMENT)";
            GUI.Label(new Rect(startX + 40f, startY + 14f, dialogWidth - 80f, 32f), headerText, _windowTitleStyle);

            if (GUI.Button(new Rect(startX + dialogWidth - 52f, startY + 12f, 40f, 36f), "X", _btnStyle))
            {
                CloseView();
            }

            // Treasury & Complete Cargo summary bar
            int gold = _playerCargo != null ? _playerCargo.CurrentGold : 0;
            System.Text.StringBuilder invSb = new System.Text.StringBuilder();
            invSb.Append($"Your Purse: {gold} Gold  |  Cargo Hold: ");
            int nonZeroCommodities = 0;
            for (int i = 1; i <= 7; i++)
            {
                CargoItemType type = (CargoItemType)i;
                int count = _playerCargo != null ? _playerCargo.GetItemCount(type) : 0;
                if (count > 0)
                {
                    if (nonZeroCommodities > 0) invSb.Append(", ");
                    invSb.Append($"{count} {type}");
                    nonZeroCommodities++;
                }
            }
            if (nonZeroCommodities == 0) invSb.Append("Empty (0 units)");
            GUI.Label(new Rect(startX + 24f, startY + 50f, dialogWidth - 48f, 26f), invSb.ToString(), _bodyStyle);

            // Deckhand Manpower Section
            DrawDeckhandSection(startX + 24f, startY + 80f, dialogWidth - 48f);

            // Officer Specialist Roster Section
            DrawOfficerRosterSection(startX + 24f, startY + 168f, dialogWidth - 48f);

            // Bottom Action Navigation Bar
            float bottomY = startY + dialogHeight - 56f;
            float btnH = 46f;

            // Commodity Trading Market Button
            if (GUI.Button(new Rect(startX + 24f, bottomY, 230f, btnH), "[Market] Open Trade", _btnStyle))
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

            // High Seas Bounty Noticeboard Button
            string bountyBtnLabel = (BountyQuestManager.Instance != null && BountyQuestManager.Instance.ActiveBounty != null)
                ? "Active Bounty (1/1)"
                : "Bounty Noticeboard";

            if (GUI.Button(new Rect(startX + 266f, bottomY, 240f, btnH), bountyBtnLabel, _btnStyle))
            {
                _isBountyBoardOpen = true;
                Vector3 pPos = _isSmugglerMode ? _activeSmugglerCove.transform.position : _activePortHaven.transform.position;
                if (BountyQuestManager.Instance != null && BountyQuestManager.Instance.AvailableContracts.Count == 0)
                {
                    BountyQuestManager.Instance.RefreshAvailableBounties(pPos);
                }
            }

            // Weigh Anchor Close Button
            if (GUI.Button(new Rect(startX + dialogWidth - 230f, bottomY, 206f, btnH), "Weigh Anchor", _btnStyle))
            {
                CloseView();
            }
        }

        private void DrawDeckhandSection(float x, float y, float width)
        {
            GUI.DrawTexture(new Rect(x, y, width, 76f), _cardBgTexture);

            int currentCrew = _playerCrew != null ? _playerCrew.CurrentDeckhands : 1;
            int maxCrew = _playerCrew != null ? _playerCrew.MaxCrewCapacity : 2;
            int minCrew = _playerCrew != null ? _playerCrew.MinRequiredCrew : 1;
            int currentOfficers = _playerCrew != null ? _playerCrew.AssignedOfficerCount : 0;
            int maxOfficers = _playerCrew != null ? _playerCrew.MaxOfficerCapacity : 1;

            string crewStatus = $"Deckhands: {currentCrew} / {maxCrew} (Min: {minCrew}) | Officer Berths: {currentOfficers} / {maxOfficers}";
            GUI.Label(new Rect(x + 16f, y + 10f, width - 320f, 24f), crewStatus, _subTitleStyle);

            string penaltyText = _playerCrew != null && _playerCrew.IsUndercrewed
                ? "<color=red>UNDERCREWED: -65% Maneuver, Reload & Bilge Pumping!</color>"
                : "<color=lime>Full Manpower: 100% Ship Handling Efficiency</color>";
            GUI.Label(new Rect(x + 16f, y + 38f, width - 320f, 26f), penaltyText, _bodyStyle);

            // Recruit / Replace Button
            bool isFull = currentCrew >= maxCrew;
            int goldCost = _isSmugglerMode ? _activeSmugglerCove.DeckhandGoldCost : _activePortHaven.DeckhandGoldCost;
            CargoItemType barterItem = _isSmugglerMode ? _activeSmugglerCove.DeckhandBarterType : CargoItemType.Gold;
            int barterCount = _isSmugglerMode ? _activeSmugglerCove.DeckhandBarterCount : 0;

            string btnText = isFull
                ? "Berths Full: Replace Crew"
                : (_isSmugglerMode ? $"Hire Deckhand ({goldCost}g + {barterCount} {barterItem})" : $"Hire Deckhand ({goldCost}g)");

            if (GUI.Button(new Rect(x + width - 295f, y + 14f, 280f, 48f), btnText, _btnStyle))
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
            GUI.Label(new Rect(x, y, width, 24f), "Available Officer Specialists for Hire:", _subTitleStyle);

            IReadOnlyList<CrewSpecialistSO> roster = _isSmugglerMode
                ? _activeSmugglerCove?.SmugglerRoster
                : _activePortHaven?.TavernRoster;

            if (roster == null || roster.Count == 0) return;

            float cardY = y + 28f;
            float cardHeight = 108f;

            for (int i = 0; i < roster.Count; i++)
            {
                CrewSpecialistSO officer = roster[i];
                if (officer == null) continue;

                Rect cardRect = new Rect(x, cardY, width, cardHeight);
                GUI.DrawTexture(cardRect, _cardBgTexture);

                // Specialist Title & Name
                string officerHeader = $"{officer.OfficerName}  [{officer.Tier} {officer.Specialty}]";
                GUI.Label(new Rect(x + 16f, cardY + 10f, width - 290f, 24f), officerHeader, _cardTitleStyle);

                // Passive Perk Description
                GUI.Label(new Rect(x + 16f, cardY + 36f, width - 290f, 60f), officer.GetEffectSummary(), _bodyStyle);

                // Cost & Hire / Replace Button
                int goldCost = _isSmugglerMode ? officer.SmugglerGoldCost : officer.BaseGoldCost;
                bool isAssigned = _playerCrew != null && _playerCrew.GetOfficer(officer.Specialty) != null;
                bool noFreeBerths = _playerCrew != null && !_playerCrew.HasFreeOfficerSlot;

                string costLabel = _isSmugglerMode
                    ? $"Cost: {goldCost}g + {officer.BarterMaterialCount} {officer.BarterMaterialType}"
                    : $"Cost: {goldCost} Gold";
                GUI.Label(new Rect(x + width - 275f, cardY + 10f, 260f, 24f), costLabel, _costStyle);

                string hireBtnText = isAssigned
                    ? "Replace Officer"
                    : (noFreeBerths ? "Berths Full: Replace" : "Hire Specialist");

                if (GUI.Button(new Rect(x + width - 210f, cardY + 44f, 195f, 50f), hireBtnText, _btnStyle))
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
            float modalW = dialogWidth - 60f;
            float modalH = dialogHeight - 60f;
            float modalX = startX + 30f;
            float modalY = startY + 30f;

            Rect modalRect = new Rect(modalX, modalY, modalW, modalH);
            GUI.DrawTexture(modalRect, _cardBgTexture);
            GUI.Box(modalRect, GUIContent.none);

            string targetName = _pendingIsDeckhand ? "1 Fresh Deckhand" : $"{_pendingOfficer.OfficerName} ({_pendingOfficer.Specialty})";
            GUI.Label(new Rect(modalX + 20f, modalY + 16f, modalW - 40f, 28f), "[CREW QUARTERS FULL - SELECT CREW TO REPLACE]", _cardTitleStyle);

            string subPrompt = $"Choose a crew member to dismiss from your vessel and replace with {targetName}:";
            GUI.Label(new Rect(modalX + 20f, modalY + 48f, modalW - 40f, 24f), subPrompt, _bodyStyle);

            float rowY = modalY + 82f;

            // Option 1: Dismiss Deckhand (if deckhands > 0)
            if (_playerCrew != null && _playerCrew.CurrentDeckhands > 0)
            {
                Rect rowRect = new Rect(modalX + 20f, rowY, modalW - 40f, 54f);
                GUI.DrawTexture(rowRect, _bgTexture);

                GUI.Label(new Rect(modalX + 32f, rowY + 16f, modalW - 320f, 24f), $"Deckhand Berth ({_playerCrew.CurrentDeckhands} currently assigned)", _subTitleStyle);

                if (GUI.Button(new Rect(modalX + modalW - 270f, rowY + 7f, 250f, 40f), "Dismiss Deckhand & Replace", _btnStyle))
                {
                    ExecuteReplacement(true, OfficerSpecialtyType.Carpenter);
                }
                rowY += 62f;
            }

            // Option 2+: Assigned Officers
            if (_playerCrew != null)
            {
                List<CrewSpecialistSO> assigned = _playerCrew.GetAssignedOfficers();
                for (int i = 0; i < assigned.Count; i++)
                {
                    CrewSpecialistSO off = assigned[i];
                    if (off == null) continue;

                    Rect rowRect = new Rect(modalX + 20f, rowY, modalW - 40f, 54f);
                    GUI.DrawTexture(rowRect, _bgTexture);

                    string offDesc = $"{off.OfficerName}  [{off.Tier} {off.Specialty}]";
                    GUI.Label(new Rect(modalX + 32f, rowY + 16f, modalW - 320f, 24f), offDesc, _cardTitleStyle);

                    if (GUI.Button(new Rect(modalX + modalW - 270f, rowY + 7f, 250f, 40f), $"Replace {off.Specialty}", _btnStyle))
                    {
                        ExecuteReplacement(false, off.Specialty);
                    }
                    rowY += 62f;
                }
            }

            // Cancel button
            if (GUI.Button(new Rect(modalX + (modalW - 240f) * 0.5f, modalY + modalH - 52f, 240f, 42f), "Cancel - Keep Current Crew", _btnStyle))
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

        private void DrawBountyBoardModal(float parentX, float parentY, float parentW, float parentH)
        {
            // Dim background overlay
            GUI.color = new Color(0.04f, 0.08f, 0.12f, 0.88f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _cardBgTexture);
            GUI.color = Color.white;

            float modalW = parentW;
            float modalH = parentH;
            float modalX = parentX;
            float modalY = parentY;

            GUI.DrawTexture(new Rect(modalX, modalY, modalW, modalH), _bgTexture);

            // Title & Subtitle
            GUI.Label(new Rect(modalX, modalY + 14f, modalW, 28f), "[HIGH SEAS BOUNTY NOTICEBOARD]", _windowTitleStyle);
            GUI.Label(new Rect(modalX, modalY + 44f, modalW, 22f), "Guaranteed target vessel coordinates. Only 1 active contract can be held at a time.", _subTitleStyle);

            float contentY = modalY + 72f;

            // Active Bounty Banner Box
            BountyContract active = BountyQuestManager.Instance != null ? BountyQuestManager.Instance.ActiveBounty : null;
            if (active != null)
            {
                Rect activeRect = new Rect(modalX + 24f, contentY, modalW - 48f, 48f);
                GUI.color = new Color(1f, 0.85f, 0.3f, 0.25f);
                GUI.DrawTexture(activeRect, _cardBgTexture);
                GUI.color = Color.white;

                string activeDesc = $"CURRENT HUNT: {active.CaptainName} [{active.ShipName} - {active.TargetTier}] in {active.SectorDescription} | REWARD: {active.GoldReward} Gold";
                GUI.Label(new Rect(activeRect.x + 12f, activeRect.y + 12f, activeRect.width - 180f, 26f), activeDesc, _bodyStyle);

                if (GUI.Button(new Rect(activeRect.xMax - 160f, activeRect.y + 6f, 150f, 36f), "Abandon Hunt", _btnStyle))
                {
                    BountyQuestManager.Instance.AbandonBounty();
                }

                contentY += 56f;
            }

            // Available Bounties (3 Columns)
            var contracts = BountyQuestManager.Instance != null ? BountyQuestManager.Instance.AvailableContracts : null;
            if (contracts != null && contracts.Count > 0)
            {
                float colW = (modalW - 48f - 24f) / 3f;
                float cardH = modalH - (contentY - modalY) - 64f;

                for (int i = 0; i < contracts.Count && i < 3; i++)
                {
                    BountyContract c = contracts[i];
                    float colX = modalX + 24f + (i * (colW + 12f));

                    Rect cardRect = new Rect(colX, contentY, colW, cardH);
                    GUI.DrawTexture(cardRect, _cardBgTexture);

                    // Difficulty Tag
                    string badge = c.IsHardContract
                        ? "<color=#FFD700><b>[HARD BOUNTY (+1 TIER)]</b></color>"
                        : "<color=#87CEEB><b>[STANDARD BOUNTY]</b></color>";
                    GUI.Label(new Rect(colX + 12f, contentY + 12f, colW - 24f, 22f), badge, _bodyStyle);

                    // Captain Name
                    GUI.Label(new Rect(colX + 12f, contentY + 38f, colW - 24f, 24f), c.CaptainName, _cardTitleStyle);

                    // Ship & Sector
                    string details = $"Vessel: {c.ShipName}\nClass: {c.TargetTier}\nAllegiance: {c.Faction}\nSector: {c.SectorDescription}";
                    GUI.Label(new Rect(colX + 12f, contentY + 66f, colW - 24f, 85f), details, _bodyStyle);

                    // Gold Reward
                    GUI.Label(new Rect(colX + 12f, contentY + cardH - 100f, colW - 24f, 26f), $"<color=#FFD700><b>BOUNTY: {c.GoldReward} GOLD</b></color>", _cardTitleStyle);

                    // Action Button: Interactive and responsive
                    bool isThisActive = active != null && active.BountyId == c.BountyId;
                    bool hasOtherActive = active != null && !isThisActive;

                    if (isThisActive)
                    {
                        GUI.enabled = false;
                        GUI.Button(new Rect(colX + 10f, contentY + cardH - 56f, colW - 20f, 46f), "[Active Target]", _btnStyle);
                        GUI.enabled = true;
                    }
                    else if (hasOtherActive)
                    {
                        // Player can swap active contracts directly!
                        if (GUI.Button(new Rect(colX + 10f, contentY + cardH - 56f, colW - 20f, 46f), "Swap To This Hunt", _btnStyle))
                        {
                            BountyQuestManager.Instance.AbandonBounty();
                            BountyQuestManager.Instance.AcceptBounty(c);
                        }
                    }
                    else
                    {
                        if (GUI.Button(new Rect(colX + 10f, contentY + cardH - 56f, colW - 20f, 46f), "Accept Bounty", _btnStyle))
                        {
                            BountyQuestManager.Instance.AcceptBounty(c);
                        }
                    }
                }
            }

            // Return to Tavern Button
            if (GUI.Button(new Rect(modalX + (modalW - 280f) * 0.5f, modalY + modalH - 52f, 280f, 42f), "<< Back to Port Tavern", _btnStyle))
            {
                _isBountyBoardOpen = false;
            }
        }
    }
}
