using Plunderlust.Combat;
using Plunderlust.Core.Data;
using Plunderlust.Ship;
using Plunderlust.Subsystems;
using UnityEngine;

namespace Plunderlust.UI
{
    /// <summary>
    /// Tactical naval combat HUD rendering crisp pixel-art telemetry overlays.
    /// Displays Flagship status (Hull, Bilge, Knots, Ammo Reloads),
    /// Target Ship locked telemetry (Distance, Subsystems, Flooding),
    /// and keyboard/touch control prompts with zero garbage collection allocations.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class PirateCombatHUD : MonoBehaviour
    {
        public static PirateCombatHUD Instance { get; private set; }

        [Header("Target Tracking")]
        [SerializeField] private float _maxTargetRange = 85.0f;

        private ShipLocomotionController _playerLocomotion;
        private ShipSubsystemController _playerSubsystems;
        private NavalCombatController _playerCombat;
        private Plunderlust.Cargo.ShipCargoHold _playerCargo;
        private Plunderlust.Crew.CrewManager _playerCrew;

        private ShipSubsystemController _nearestTarget;
        private float _targetDistance = 0f;
        private float _targetCheckTimer = 0f;

        // Cached UI Textures & Styles (Zero GC)
        private Texture2D _panelBgTex;
        private Texture2D _hpGreenTex;
        private Texture2D _hpYellowTex;
        private Texture2D _hpRedTex;
        private Texture2D _bilgeTex;
        private Texture2D _cooldownTex;
        private Texture2D _barBgTex;

        private GUIStyle _headerStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _statLabelStyle;
        private GUIStyle _barTextStyle;
        private GUIStyle _controlHintStyle;
        private GUIStyle _alertStyle;

        private string _bannerMessage = "";
        private float _bannerTimer = 0f;
        private Color _bannerColor = Color.white;
        private bool _stylesInitialized = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Plunderlust.Loot.LootCrate.OnPlayerLootCollected += HandleLootCollected;
            FindPlayerShip();
        }

        private void OnDestroy()
        {
            Plunderlust.Loot.LootCrate.OnPlayerLootCollected -= HandleLootCollected;
        }

        private void HandleLootCollected(CargoItemType type, int count, Vector3 pos)
        {
            ShowBanner($"+{count} {type.ToString().ToUpper()} SALVAGED!", new Color(1f, 0.85f, 0.2f), 2.5f);
        }

        public void FindPlayerShip()
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                _playerLocomotion = playerGo.GetComponent<ShipLocomotionController>();
                _playerSubsystems = playerGo.GetComponent<ShipSubsystemController>();
                _playerCombat = playerGo.GetComponent<NavalCombatController>();
                _playerCargo = playerGo.GetComponent<Plunderlust.Cargo.ShipCargoHold>();
                _playerCrew = playerGo.GetComponent<Plunderlust.Crew.CrewManager>();
            }
        }

        public void ShowBanner(string message, Color color, float duration = 3.0f)
        {
            _bannerMessage = message;
            _bannerColor = color;
            _bannerTimer = duration;
        }

        private void Update()
        {
            if (_playerLocomotion == null) FindPlayerShip();

            _targetCheckTimer += Time.deltaTime;
            if (_targetCheckTimer >= 0.25f)
            {
                _targetCheckTimer = 0f;
                UpdateNearestTarget();
            }

            if (_bannerTimer > 0f)
            {
                _bannerTimer -= Time.deltaTime;
            }
        }

        private void UpdateNearestTarget()
        {
            if (_playerLocomotion == null) return;

            Vector3 playerPos = _playerLocomotion.transform.position;
            ShipSubsystemController[] allShips = Object.FindObjectsByType<ShipSubsystemController>(FindObjectsInactive.Exclude);

            ShipSubsystemController bestTarget = null;
            float bestDist = _maxTargetRange;

            foreach (var ship in allShips)
            {
                if (ship == null || ship.gameObject == _playerLocomotion.gameObject) continue;
                if (ship.IsDead) continue;

                float dist = Vector3.Distance(playerPos, ship.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestTarget = ship;
                }
            }

            _nearestTarget = bestTarget;
            _targetDistance = bestDist;
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _panelBgTex = MakeSolidTex(new Color(0.06f, 0.08f, 0.12f, 0.88f));
            _barBgTex = MakeSolidTex(new Color(0.12f, 0.14f, 0.18f, 0.95f));
            _hpGreenTex = MakeSolidTex(new Color(0.2f, 0.85f, 0.35f, 1f));
            _hpYellowTex = MakeSolidTex(new Color(0.95f, 0.78f, 0.15f, 1f));
            _hpRedTex = MakeSolidTex(new Color(0.95f, 0.25f, 0.2f, 1f));
            _bilgeTex = MakeSolidTex(new Color(0.15f, 0.65f, 0.95f, 1f));
            _cooldownTex = MakeSolidTex(new Color(0.85f, 0.55f, 0.15f, 1f));

            _headerStyle = new GUIStyle
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _headerStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);

            _subHeaderStyle = new GUIStyle
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _subHeaderStyle.normal.textColor = Color.white;

            _statLabelStyle = new GUIStyle
            {
                fontSize = 10,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };
            _statLabelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            _barTextStyle = new GUIStyle
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _barTextStyle.normal.textColor = Color.white;

            _controlHintStyle = new GUIStyle
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _controlHintStyle.normal.textColor = new Color(0.92f, 0.92f, 0.95f);

            _alertStyle = new GUIStyle
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _stylesInitialized = true;
        }

        private Texture2D MakeSolidTex(Color col)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }

        [Header("Legacy Debug Overhaul")]
        [SerializeField] private bool _enableLegacyOnGUI = false;

        private void OnGUI()
        {
            if (GameOverUI.IsGameOver) return;
            InitStyles();

            if (_enableLegacyOnGUI)
            {
                DrawPlayerStatusPanel();

                if (_nearestTarget != null && !_nearestTarget.IsDead)
                {
                    DrawTargetStatusPanel();
                }

                DrawControlHintsPanel();
            }

            DrawCenterBanner();
        }

        private void DrawPlayerStatusPanel()
        {
            int officerLines = (_playerCrew != null) ? _playerCrew.AssignedOfficerCount : 0;
            bool hasMap = (_playerCargo != null && _playerCargo.GetItemCount(CargoItemType.TreasureMap) > 0);
            float panelWidth = 280f;
            float panelHeight = 175f + (officerLines * 16f) + (hasMap ? 18f : 0f);
            Rect panelRect = new Rect(12f, 12f, panelWidth, panelHeight);

            GUI.DrawTexture(panelRect, _panelBgTex);

            string shipName = "FLAGSHIP: LEAKY ROWBOAT";
            if (_playerSubsystems != null && _playerSubsystems.HullData != null)
            {
                shipName = $"FLAGSHIP: {_playerSubsystems.HullData.ShipClassName.ToUpper()}";
            }
            GUI.Label(new Rect(20f, 16f, panelWidth - 20f, 18f), shipName, _headerStyle);

            if (_playerSubsystems != null)
            {
                // Hull Health Bar
                float hullRatio = _playerSubsystems.HullIntegrityRatio;
                Rect barRect = new Rect(20f, 38f, panelWidth - 40f, 13f);
                DrawProgressBar(barRect, hullRatio, $"Hull: {_playerSubsystems.HullHealth:F0} / {_playerSubsystems.HullIntegrityRatio * 100:F0}%");

                // Bilge Water Flooding Bar (if taking water)
                float bilgeRatio = _playerSubsystems.FloodingRatio;
                if (bilgeRatio > 0.01f)
                {
                    Rect bilgeRect = new Rect(20f, 54f, panelWidth - 40f, 10f);
                    GUI.DrawTexture(bilgeRect, _barBgTex);
                    GUI.DrawTexture(new Rect(20f, 54f, (panelWidth - 40f) * bilgeRatio, 10f), _bilgeTex);
                    GUI.Label(bilgeRect, $"Bilge Flooding: {bilgeRatio * 100:F0}%", _barTextStyle);
                }
            }

            // Speed & Propulsion
            if (_playerLocomotion != null)
            {
                float speedKnots = _playerLocomotion.CurrentSpeedKnots;
                float throttle = _playerLocomotion.ThrottleInput;
                bool isRowboat = _playerLocomotion.HullData != null && _playerLocomotion.HullData.SupportsRowing && !_playerLocomotion.HullData.SupportsSails;

                string sailMode;
                if (throttle < -0.05f)
                {
                    sailMode = $"Propulsion: <color=yellow>REVERSE ({Mathf.Abs(throttle) * 100:F0}%)</color>";
                }
                else if (isRowboat)
                {
                    sailMode = Mathf.Abs(throttle) > 0.05f
                        ? $"Rowing (Oars: {throttle * 100:F0}%)"
                        : "Oars: Resting (Hold W forward, S reverse)";
                }
                else
                {
                    sailMode = _playerLocomotion.CurrentSailState switch
                    {
                        SailState.Anchored => "Canvas: Anchored (Hold W forward)",
                        SailState.BattleSails => "Canvas: Battle Sails (Hold W for Full)",
                        SailState.FullCanvas => "Canvas: Full Canvas",
                        _ => $"Canvas: {_playerLocomotion.CurrentSailState}"
                    };
                }

                GUI.Label(new Rect(20f, 68f, panelWidth - 20f, 16f), $"{sailMode} | {speedKnots:F1} kts", _statLabelStyle);
            }

            // Ordnance & Swivel Gun Cooldown
            if (_playerCombat != null)
            {
                bool canFire = _playerCombat.CanFireForward;
                float reloadNorm = _playerCombat.ForwardReloadNormalized;
                string statusText = canFire ? "SWIVEL GUN READY [SPACE]" : $"RELOADING: {(1f - reloadNorm) * 100:F0}%";
                Color statusCol = canFire ? new Color(0.2f, 0.95f, 0.4f) : new Color(0.95f, 0.65f, 0.2f);

                GUI.color = statusCol;
                GUI.Label(new Rect(20f, 84f, panelWidth - 20f, 16f), statusText, _statLabelStyle);
                GUI.color = Color.white;
            }

            // Cargo Hold Inventory & Treasury (Lists ALL commodities)
            float curY = 100f;
            if (_playerCargo != null)
            {
                int gold = _playerCargo.CurrentGold;
                int used = _playerCargo.TotalCargoUnits;
                int max = _playerCargo.MaxCargoCapacity;

                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.Append($"Hold: {used}/{max} | ✦{gold}g");
                for (int i = 1; i <= 8; i++)
                {
                    CargoItemType type = (CargoItemType)i;
                    int count = _playerCargo.GetItemCount(type);
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
                            CargoItemType.Silk => "🧣",
                            _ => ""
                        };
                        sb.Append($" | {icon}{type}:{count}");
                    }
                }

                GUI.Label(new Rect(20f, curY, panelWidth - 20f, 16f), sb.ToString(), _statLabelStyle);
                curY += 15f;

                // Small Hold Space Bar
                float holdRatio = Mathf.Clamp01((float)used / Mathf.Max(1, max));
                Rect holdBarRect = new Rect(20f, curY, panelWidth - 40f, 5f);
                GUI.DrawTexture(holdBarRect, _barBgTex);
                Color holdCol = holdRatio >= 0.9f ? Color.red : new Color(0.85f, 0.65f, 0.2f);
                GUI.color = holdCol;
                GUI.DrawTexture(new Rect(20f, curY, (panelWidth - 40f) * holdRatio, 5f), _cooldownTex);
                GUI.color = Color.white;
                curY += 9f;
            }

            // Crew & Officers Status + Real Active Perks
            if (_playerCrew != null)
            {
                string crewStr = $"Crew: {_playerCrew.CurrentDeckhands}/{_playerCrew.MaxCrewCapacity} ({_playerCrew.BaseManpowerEfficiency * 100:F0}% Eff) | Officers: {_playerCrew.AssignedOfficerCount}/{_playerCrew.MaxOfficerCapacity}";
                GUI.Label(new Rect(20f, curY, panelWidth - 20f, 16f), crewStr, _statLabelStyle);
                curY += 15f;

                if (_playerCrew.Carpenter != null)
                {
                    GUI.Label(new Rect(20f, curY, panelWidth - 20f, 14f), $"<color=lime>• Carp:</color> {_playerCrew.Carpenter.OfficerName} (+{_playerCrew.Carpenter.PrimaryPassiveValue:F1} HP/s Repair)", _statLabelStyle);
                    curY += 14f;
                }
                if (_playerCrew.MasterGunner != null)
                {
                    GUI.Label(new Rect(20f, curY, panelWidth - 20f, 14f), $"<color=yellow>• Gunn:</color> {_playerCrew.MasterGunner.OfficerName} (-{_playerCrew.MasterGunner.PrimaryPassiveValue * 100:F0}% CD, +25% Dmg)", _statLabelStyle);
                    curY += 14f;
                }
                if (_playerCrew.Sailmaster != null)
                {
                    GUI.Label(new Rect(20f, curY, panelWidth - 20f, 14f), $"<color=cyan>• Sail:</color> {_playerCrew.Sailmaster.OfficerName} (+{_playerCrew.Sailmaster.SecondaryPassiveValue * 100:F0}% Spd/Oars)", _statLabelStyle);
                    curY += 14f;
                }
                if (_playerCrew.Bosun != null)
                {
                    GUI.Label(new Rect(20f, curY, panelWidth - 20f, 14f), $"<color=orange>• Bosn:</color> {_playerCrew.Bosun.OfficerName} (-20% Dmg, Auto-Pump)", _statLabelStyle);
                    curY += 14f;
                }
                if (_playerCrew.Quartermaster != null)
                {
                    GUI.Label(new Rect(20f, curY, panelWidth - 20f, 14f), $"<color=gold>• Q-M:</color> {_playerCrew.Quartermaster.OfficerName} (+35% Salvage Loot)", _statLabelStyle);
                    curY += 14f;
                }
            }

            // Factions & Bounty Telemetry
            if (Plunderlust.Factions.FactionManager.Instance != null)
            {
                int bounty = Plunderlust.Factions.FactionManager.Instance.ActiveBounty;
                int navy = Plunderlust.Factions.FactionManager.Instance.GetReputation(FactionType.ImperialNavy);
                int cors = Plunderlust.Factions.FactionManager.Instance.GetReputation(FactionType.CorsairBrethren);
                string repText = $"Bounty: {bounty}g | Navy: {navy} | Corsairs: {cors}";
                GUI.Label(new Rect(20f, curY, panelWidth - 20f, 16f), repText, _statLabelStyle);
                curY += 16f;
            }

            // Treasure Map Compass Bearing
            if (hasMap && Plunderlust.World.TreasureManager.Instance != null)
            {
                if (Plunderlust.World.TreasureManager.Instance.GetNearestTreasureBearing(out float dist, out float bearing))
                {
                    string cardinal = GetCardinalHeading(bearing);
                    GUI.Label(new Rect(20f, curY, panelWidth - 20f, 16f), $"<color=#FFD700>🗺️ MAP BEARING:</color> {dist:F0}m [{cardinal} {bearing:F0}°]", _statLabelStyle);
                    curY += 16f;
                }
            }
        }

        private static string GetCardinalHeading(float degrees)
        {
            string[] cardinals = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            int index = Mathf.RoundToInt(degrees / 45f) % 8;
            if (index < 0) index += 8;
            return cardinals[index];
        }

        private void DrawTargetStatusPanel()
        {
            float panelWidth = 240f;
            float panelHeight = 126f;
            float startX = Screen.width - panelWidth - 12f;
            Rect panelRect = new Rect(startX, 12f, panelWidth, panelHeight);

            GUI.DrawTexture(panelRect, _panelBgTex);

            string targetName = _nearestTarget.gameObject.name.Replace("(Clone)", "").Replace("TargetVessel_", "TARGET: ").ToUpper();
            GUI.Label(new Rect(startX + 10f, 16f, 220f, 18f), $"{targetName} ({_targetDistance:F0}m)", _headerStyle);

            // Hull Health Bar
            float hullRatio = _nearestTarget.HullIntegrityRatio;
            Rect barRect = new Rect(startX + 10f, 38f, 220f, 14f);
            DrawProgressBar(barRect, hullRatio, $"Hull: {_nearestTarget.HullHealth:F0} / {_nearestTarget.HullIntegrityRatio * 100:F0}%");

            // Subsystem health breakdown
            string subText = $"Sails: {_nearestTarget.SailsEfficiency * 100:F0}% | Rudder: {_nearestTarget.RudderEfficiency * 100:F0}% | Guns: {_nearestTarget.GunDeckEfficiency * 100:F0}%";
            GUI.Label(new Rect(startX + 10f, 56f, 220f, 16f), subText, _statLabelStyle);

            // Bilge Flooding Bar
            float bilgeRatio = _nearestTarget.FloodingRatio;
            Rect bilgeRect = new Rect(startX + 10f, 74f, 220f, 11f);
            GUI.DrawTexture(bilgeRect, _barBgTex);
            if (bilgeRatio > 0.01f)
            {
                GUI.DrawTexture(new Rect(startX + 10f, 74f, 220f * bilgeRatio, 11f), _bilgeTex);
            }
            GUI.Label(bilgeRect, $"Bilge Flooding: {bilgeRatio * 100:F0}%", _barTextStyle);

            // Sinking / Surrendered / Tactical Status
            Plunderlust.AI.AIShipBrain aiBrain = _nearestTarget.GetComponent<Plunderlust.AI.AIShipBrain>();
            if (_nearestTarget.IsSinking)
            {
                GUI.color = Color.cyan;
                GUI.Label(new Rect(startX + 10f, 92f, 220f, 18f), "VESSEL FOUNDERING & SINKING!", _subHeaderStyle);
                GUI.color = Color.white;
            }
            else if (aiBrain != null && aiBrain.IsSurrendered)
            {
                GUI.color = Color.yellow;
                string plunderStatus = aiBrain.IsPlundered ? "STRUCK COLORS - [ALREADY PLUNDERED]" : "STRUCK COLORS - [E] BOARD & PLUNDER";
                GUI.Label(new Rect(startX + 10f, 92f, 220f, 18f), plunderStatus, _subHeaderStyle);
                GUI.color = Color.white;
            }
            else
            {
                string weakSpot = hullRatio < 0.5f ? "WATERLINE CRITICALLY BREACHED" : "FIRE AT WATERLINE TO SINK";
                GUI.Label(new Rect(startX + 10f, 92f, 220f, 16f), weakSpot, _statLabelStyle);
            }
        }

        private void DrawProgressBar(Rect rect, float ratio, string label)
        {
            GUI.DrawTexture(rect, _barBgTex);

            Texture2D fillTex = ratio > 0.55f ? _hpGreenTex : (ratio > 0.25f ? _hpYellowTex : _hpRedTex);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(ratio), rect.height), fillTex);

            GUI.Label(rect, label, _barTextStyle);
        }

        private void DrawControlHintsPanel()
        {
            float panelWidth = 640f;
            float panelHeight = 24f;
            float startX = (Screen.width - panelWidth) * 0.5f;
            float startY = Screen.height - panelHeight - 10f;

            Rect panelRect = new Rect(startX, startY, panelWidth, panelHeight);
            GUI.DrawTexture(panelRect, _panelBgTex);

            string hints = "[W/S] Row / Sails  |  [A/D] Steer  |  [SPACE] Swivel  |  [Q/E] Broadsides  |  [T] Moor at Port / Cove";
            GUI.Label(panelRect, hints, _controlHintStyle);
        }

        private void DrawCenterBanner()
        {
            if (_bannerTimer <= 0f) return;

            float bannerWidth = 460f;
            float bannerHeight = 40f;
            float startX = (Screen.width - bannerWidth) * 0.5f;
            float startY = Screen.height * 0.26f;

            _alertStyle.normal.textColor = _bannerColor;
            GUI.DrawTexture(new Rect(startX - 10f, startY - 4f, bannerWidth + 20f, bannerHeight + 8f), _panelBgTex);
            GUI.Label(new Rect(startX, startY, bannerWidth, bannerHeight), _bannerMessage, _alertStyle);
        }
    }
}
