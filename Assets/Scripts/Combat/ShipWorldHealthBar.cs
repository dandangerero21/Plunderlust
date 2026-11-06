using Plunderlust.AI;
using Plunderlust.Core.Data;
using Plunderlust.Subsystems;
using UnityEngine;

namespace Plunderlust.Combat
{
    /// <summary>
    /// World-space visual telemetry and floating health bar for naval vessels.
    /// Styled with an authentic 17th-century pirate parchment palette, burnished bronze trims,
    /// and dynamic combat status badges with zero per-frame heap allocations.
    /// </summary>
    [RequireComponent(typeof(ShipSubsystemController))]
    public class ShipWorldHealthBar : MonoBehaviour
    {
        [Header("Display Settings")]
        [SerializeField] private Vector3 _worldOffset = new Vector3(0f, 2.6f, 0f);
        [SerializeField] private float _displayDuration = 10.0f;
        [SerializeField] private float _maxVisibleDistance = 60.0f;
        [SerializeField] private bool _alwaysShowForNearbyShips = true;

        private ShipSubsystemController _subsystems;
        private UnityEngine.Camera _mainCam;
        private float _lastDamageTime = -99f;
        private string _lastHitText = "";
        private float _lastHitTextTimer = 0f;
        private Color _lastHitColor = Color.white;

        // GUI drawing styles & textures
        private GUIStyle _nameStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _hpTextStyle;
        private GUIStyle _hitTextStyle;
        private GUIStyle _badgeStyle;

        private Texture2D _borderTexture;
        private Texture2D _bgTexture;
        private Texture2D _hpTexture;
        private bool _stylesInitialized = false;

        private void Awake()
        {
            _subsystems = GetComponent<ShipSubsystemController>();
            if (_subsystems != null)
            {
                _subsystems.OnSubsystemDamaged += HandleSubsystemDamaged;
                _subsystems.OnSubsystemRepaired += HandleSubsystemDamaged;
                _subsystems.OnSubsystemHitDetailed += HandleSubsystemHitDetailed;
                _subsystems.OnShipStartedSinking += HandleShipSinking;
            }
        }

        private void OnDestroy()
        {
            if (_subsystems != null)
            {
                _subsystems.OnSubsystemDamaged -= HandleSubsystemDamaged;
                _subsystems.OnSubsystemRepaired -= HandleSubsystemDamaged;
                _subsystems.OnSubsystemHitDetailed -= HandleSubsystemHitDetailed;
                _subsystems.OnShipStartedSinking -= HandleShipSinking;
            }
        }

        private void HandleSubsystemDamaged(ShipSubsystemType system, float currentHP, float maxHP)
        {
            _lastDamageTime = Time.time;
            _lastHitTextTimer = 2.2f;
        }

        private void HandleSubsystemHitDetailed(ShipSubsystemType system, float damageDealt, float currentHP, Vector3 hitWorldPos)
        {
            _lastDamageTime = Time.time;
            _lastHitTextTimer = 2.2f;

            switch (system)
            {
                case ShipSubsystemType.Hull:
                    _lastHitText = $"-{damageDealt:F0} HULL BREACH!";
                    _lastHitColor = new Color(1f, 0.25f, 0.2f);
                    break;
                case ShipSubsystemType.SailsRigging:
                    _lastHitText = $"-{damageDealt:F0} SAILS HIT!";
                    _lastHitColor = new Color(1f, 0.85f, 0.2f);
                    break;
                case ShipSubsystemType.RudderHelm:
                    _lastHitText = $"-{damageDealt:F0} RUDDER RAKED!";
                    _lastHitColor = new Color(1f, 0.55f, 0.15f);
                    break;
                case ShipSubsystemType.GunDeck:
                    _lastHitText = $"-{damageDealt:F0} GUN DECK HIT!";
                    _lastHitColor = new Color(0.95f, 0.35f, 0.9f);
                    break;
            }
        }

        private void HandleShipSinking()
        {
            _lastDamageTime = Time.time + 30f;
            _lastHitText = "VESSEL SINKING!";
            _lastHitColor = Color.cyan;
            _lastHitTextTimer = 10f;
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            // Burnished bronze / gold trim
            _borderTexture = new Texture2D(1, 1);
            _borderTexture.SetPixel(0, 0, new Color(0.65f, 0.52f, 0.18f, 0.95f));
            _borderTexture.Apply();

            // Dark charred oak hull background
            _bgTexture = new Texture2D(1, 1);
            _bgTexture.SetPixel(0, 0, new Color(0.12f, 0.08f, 0.05f, 0.92f));
            _bgTexture.Apply();

            // Fill texture tinted dynamically
            _hpTexture = new Texture2D(1, 1);
            _hpTexture.SetPixel(0, 0, Color.white);
            _hpTexture.Apply();

            Font font = UI.NauticalUITheme.GetDefaultFont();

            // Warm antique parchment vessel title
            _nameStyle = new GUIStyle
            {
                font = font,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _nameStyle.normal.textColor = new Color(0.98f, 0.92f, 0.75f);

            // Nautical faction / class subtitle
            _subtitleStyle = new GUIStyle
            {
                font = font,
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _subtitleStyle.normal.textColor = new Color(0.78f, 0.74f, 0.65f, 0.90f);

            // Clean crisp HP readout inside bar
            _hpTextStyle = new GUIStyle
            {
                font = font,
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _hpTextStyle.normal.textColor = Color.white;

            // Floating combat hit notifications
            _hitTextStyle = new GUIStyle
            {
                font = font,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            // Warning tactical badge (sails damaged, rudder raked, taking water)
            _badgeStyle = new GUIStyle
            {
                font = font,
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _badgeStyle.normal.textColor = new Color(1.0f, 0.68f, 0.22f);

            _stylesInitialized = true;
        }

        private string GetFactionSubtitle()
        {
            AIShipBrain brain = GetComponent<AIShipBrain>();
            if (brain != null)
            {
                return brain.Faction switch
                {
                    FactionType.ImperialNavy => "Royal Navy Patrol",
                    FactionType.MerchantGuild => "Merchant Guild Trader",
                    FactionType.CorsairBrethren => "Pirate Corsair Raider",
                    FactionType.FreeportTraders => "Freeport Smuggler",
                    _ => "Vessel"
                };
            }
            return "Naval Vessel";
        }

        private void OnGUI()
        {
            if (_subsystems == null) return;

            // Never draw floating OnGUI bars through modals, Game Over, or Pause Menu
            if (UI.GameOverUI.IsGameOver) return;
            if (UI.PauseMenuUI.Instance != null && UI.PauseMenuUI.IsPaused) return;
            if (UI.SeaChartMapUI.Instance != null && UI.SeaChartMapUI.Instance.IsOpen) return;

            // Player flagship is monitored by dedicated Safe-Area ShipPaperDollUI HUD
            if (CompareTag("Player") || transform.root.CompareTag("Player")) return;

            if (_mainCam == null) _mainCam = UnityEngine.Camera.main;
            if (_mainCam == null) return;

            float distToCam = Vector3.Distance(_mainCam.transform.position, transform.position);
            if (distToCam > _maxVisibleDistance) return;

            bool isRecentDamage = (Time.time - _lastDamageTime <= _displayDuration);
            if (!_alwaysShowForNearbyShips && !isRecentDamage && !_subsystems.IsSinking) return;

            InitStyles();

            Vector3 worldPos = transform.position + _worldOffset;
            Vector3 screenPos = _mainCam.WorldToScreenPoint(worldPos);

            // Hide if behind camera
            if (screenPos.z < 0.1f) return;

            float barWidth = 140f;
            float barHeight = 11f;

            // Clamp screen coordinates so health plate stays safely on screen
            float screenX = Mathf.Clamp(screenPos.x, barWidth * 0.5f + 10f, Screen.width - (barWidth * 0.5f + 10f));
            float screenY = Mathf.Clamp(Screen.height - screenPos.y, 65f, Screen.height - 40f);
            float startX = screenX - (barWidth * 0.5f);

            // 1. Draw Vessel Name with dark drop shadow for contrast against sky & sea
            string rawName = gameObject.name;
            string shipName = rawName.Replace("(Clone)", "").Replace("TargetVessel_", "").Replace("Ship_", "").Replace('_', ' ').Trim();
            string subtitle = GetFactionSubtitle();

            Rect nameRect = new Rect(startX - 20f, screenY - 34f, barWidth + 40f, 16f);
            Rect nameShadowRect = new Rect(nameRect.x + 1f, nameRect.y + 1f, nameRect.width, nameRect.height);
            _nameStyle.normal.textColor = Color.black;
            GUI.Label(nameShadowRect, shipName, _nameStyle);
            _nameStyle.normal.textColor = new Color(0.98f, 0.92f, 0.75f);
            GUI.Label(nameRect, shipName, _nameStyle);

            // 2. Draw Faction Subtitle Tag
            Rect subRect = new Rect(startX - 20f, screenY - 19f, barWidth + 40f, 13f);
            GUI.Label(subRect, subtitle, _subtitleStyle);

            // 3. Draw Floating Hit Alert
            if (_lastHitTextTimer > 0f)
            {
                _lastHitTextTimer -= Time.deltaTime;
                _hitTextStyle.normal.textColor = _lastHitColor;
                float bounceY = Mathf.Sin(_lastHitTextTimer * 3.5f) * 6f;
                GUI.Label(new Rect(startX - 40f, screenY - 54f - bounceY, barWidth + 80f, 20f), _lastHitText, _hitTextStyle);
            }

            // 4. Draw Pirate Plank Health Bar
            float hullRatio = Mathf.Clamp01(_subsystems.HullIntegrityRatio);
            // Bronze outer border
            GUI.DrawTexture(new Rect(startX - 2f, screenY - 2f, barWidth + 4f, barHeight + 4f), _borderTexture);
            // Charred oak background
            GUI.DrawTexture(new Rect(startX - 1f, screenY - 1f, barWidth + 2f, barHeight + 2f), _bgTexture);

            // Dynamic color: Emerald Green (>50%) -> Amber Gold (25-50%) -> Blood Crimson (<25%)
            Color hpColor = (hullRatio > 0.5f)
                ? Color.Lerp(new Color(0.95f, 0.75f, 0.15f), new Color(0.18f, 0.75f, 0.32f), (hullRatio - 0.5f) * 2f)
                : Color.Lerp(new Color(0.85f, 0.18f, 0.15f), new Color(0.95f, 0.75f, 0.15f), hullRatio * 2f);

            GUI.color = hpColor;
            GUI.DrawTexture(new Rect(startX, screenY, barWidth * hullRatio, barHeight), _hpTexture);
            GUI.color = Color.white;

            // Compact HP readout
            GUI.Label(new Rect(startX, screenY - 1f, barWidth, barHeight), $"{_subsystems.HullHealth:F0} / {_subsystems.MaxHullHealth:F0} HP", _hpTextStyle);

            // 5. Tactical Status Badges: ONLY displayed when subsystems are actually damaged or flooded (Zero clutter when healthy)
            string statusBadges = "";
            if (_subsystems.SailsEfficiency < 0.65f)
            {
                statusBadges += (_subsystems.SailsEfficiency < 0.35f ? " [SAILS SHREDDED]" : " [SAILS TORN]");
            }
            if (_subsystems.RudderEfficiency < 0.65f)
            {
                statusBadges += " [RUDDER CRIPPLED]";
            }
            if (_subsystems.GunDeckEfficiency < 0.50f)
            {
                statusBadges += " [BATTERY SILENCED]";
            }
            if (_subsystems.FloodingRatio > 0.15f)
            {
                statusBadges += $" [FLOODING {_subsystems.FloodingRatio * 100:F0}%]";
            }

            if (!string.IsNullOrEmpty(statusBadges))
            {
                float badgeY = screenY + barHeight + 3f;
                GUI.Label(new Rect(startX - 30f, badgeY, barWidth + 60f, 14f), statusBadges.Trim(), _badgeStyle);
            }
        }
    }
}
