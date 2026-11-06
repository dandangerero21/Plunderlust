using Plunderlust.Ship;
using Plunderlust.Subsystems;
using UnityEngine;

namespace Plunderlust.UI
{
    /// <summary>
    /// Phase 6 Subsystem Paper-Doll Telemetry Widget (FTL / Naval Sim style).
    /// Renders an architectural layout of the flagship's 4 critical subsystems:
    /// - Sails & Rigging
    /// - Hull Planking
    /// - Rudder & Helm
    /// - Gun Deck & Broadside Batteries
    /// Features dynamic status color coding, bilge water slosh telemetry,
    /// and one-click Emergency Sea Repair triggers.
    /// Operates with zero heap allocations in OnGUI.
    /// </summary>
    public class ShipPaperDollWidget : MonoBehaviour
    {
        public static ShipPaperDollWidget Instance { get; private set; }

        [Header("Paper-Doll Position & Geometry")]
        [SerializeField] private float _panelWidth = 240f;
        [SerializeField] private float _panelHeight = 220f;
        [SerializeField] private bool _showOnGUI = false;

        private ShipSubsystemController _subsystems;
        private EmergencyRepairsController _repairs;

        // Cached procedural textures (Zero GC)
        private Texture2D _bgTex;
        private Texture2D _panelInnerTex;
        private Texture2D _greenTex;
        private Texture2D _yellowTex;
        private Texture2D _redTex;
        private Texture2D _bilgeTex;
        private Texture2D _blackTex;
        private Texture2D _highlightTex;

        private GUIStyle _titleStyle;
        private GUIStyle _subsystemTitleStyle;
        private GUIStyle _subsystemPercentStyle;
        private GUIStyle _btnStyle;
        private bool _stylesInitialized = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            FindPlayerShip();
        }

        public void FindPlayerShip()
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                _subsystems = playerGo.GetComponent<ShipSubsystemController>();
                _repairs = playerGo.GetComponent<EmergencyRepairsController>();
            }
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _bgTex = MakeSolidTex(new Color(0.04f, 0.08f, 0.13f, 0.94f));
            _panelInnerTex = MakeSolidTex(new Color(0.08f, 0.14f, 0.22f, 0.90f));
            _greenTex = MakeSolidTex(new Color(0.18f, 0.82f, 0.35f, 0.95f));
            _yellowTex = MakeSolidTex(new Color(0.92f, 0.72f, 0.18f, 0.95f));
            _redTex = MakeSolidTex(new Color(0.90f, 0.22f, 0.18f, 0.95f));
            _bilgeTex = MakeSolidTex(new Color(0.15f, 0.55f, 0.95f, 0.85f));
            _blackTex = MakeSolidTex(new Color(0.02f, 0.03f, 0.05f, 0.90f));
            _highlightTex = MakeSolidTex(new Color(0.25f, 0.65f, 0.85f, 0.35f));

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.95f, 0.85f, 0.4f) }
            };

            _subsystemTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _subsystemPercentStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.9f, 0.95f) }
            };

            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(2, 2, 2, 2)
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

        private void OnGUI()
        {
            if (!_showOnGUI) return;
            InitStyles();

            if (_subsystems == null || _subsystems.gameObject == null)
            {
                FindPlayerShip();
                if (_subsystems == null) return;
            }

            DrawPaperDoll();
        }

        private void DrawPaperDoll()
        {
            // Positioned at bottom-left corner above controls
            float screenH = Screen.height;
            float posX = 12f;
            float posY = screenH - _panelHeight - 55f;

            Rect outerRect = new Rect(posX, posY, _panelWidth, _panelHeight);
            GUI.DrawTexture(outerRect, _bgTex);

            // Title Header
            GUI.Label(new Rect(posX, posY + 4f, _panelWidth, 16f), "SHIP SUBSYSTEM PAPER-DOLL", _titleStyle);

            // Subsystem Integrity Ratios
            float sailsRatio = _subsystems.MaxSailsHealth > 0 ? _subsystems.SailsHealth / _subsystems.MaxSailsHealth : 1f;
            float hullRatio = _subsystems.HullIntegrityRatio;
            float rudderRatio = _subsystems.MaxRudderHealth > 0 ? _subsystems.RudderHealth / _subsystems.MaxRudderHealth : 1f;
            float gunsRatio = _subsystems.MaxGunDeckHealth > 0 ? _subsystems.GunDeckHealth / _subsystems.MaxGunDeckHealth : 1f;
            float bilgeRatio = _subsystems.FloodingRatio;

            // 1. SAILS & RIGGING (Top Center Box)
            Rect sailsRect = new Rect(posX + 45f, posY + 22f, _panelWidth - 90f, 32f);
            DrawSubsystemCell(sailsRect, "SAILS & RIGGING", sailsRatio, _subsystems.SailsHealth, _subsystems.MaxSailsHealth);

            // 2. PORT GUN BATTERIES (Left Wing)
            Rect portGunRect = new Rect(posX + 8f, posY + 58f, 50f, 56f);
            DrawSubsystemCell(portGunRect, "PORT\nBATTERY", gunsRatio, _subsystems.GunDeckHealth * 0.5f, _subsystems.MaxGunDeckHealth * 0.5f);

            // 3. HULL & PLANKING (Center Keel)
            Rect hullRect = new Rect(posX + 62f, posY + 58f, _panelWidth - 124f, 56f);
            DrawSubsystemCell(hullRect, "HULL & BILGE", hullRatio, _subsystems.HullHealth, _subsystems.MaxHullHealth);

            // 4. STARBOARD GUN BATTERIES (Right Wing)
            Rect stbdGunRect = new Rect(posX + _panelWidth - 58f, posY + 58f, 50f, 56f);
            DrawSubsystemCell(stbdGunRect, "STBD\nBATTERY", gunsRatio, _subsystems.GunDeckHealth * 0.5f, _subsystems.MaxGunDeckHealth * 0.5f);

            // 5. RUDDER & HELM (Rear Stern Box)
            Rect rudderRect = new Rect(posX + 55f, posY + 118f, _panelWidth - 110f, 28f);
            DrawSubsystemCell(rudderRect, "RUDDER & HELM", rudderRatio, _subsystems.RudderHealth, _subsystems.MaxRudderHealth);

            // 6. BILGE WATER SLOSH TANK BAR
            Rect bilgeBarOuter = new Rect(posX + 10f, posY + 150f, _panelWidth - 20f, 12f);
            GUI.DrawTexture(bilgeBarOuter, _blackTex);
            if (bilgeRatio > 0.005f)
            {
                GUI.DrawTexture(new Rect(posX + 10f, posY + 150f, (_panelWidth - 20f) * bilgeRatio, 12f), _bilgeTex);
            }
            string bilgeText = bilgeRatio <= 0.01f ? "Bilge: Dry (0%)" : $"Bilge Flooding: {bilgeRatio * 100:F0}% ({_subsystems.CurrentBilgeWater:F0}L)";
            GUI.Label(bilgeBarOuter, bilgeText, _subsystemPercentStyle);

            // 7. EMERGENCY AT-SEA REPAIR BUTTONS
            if (_repairs != null)
            {
                float btnW = (_panelWidth - 26f) * 0.5f;

                // [H] Patch Hull Button
                bool canHull = _repairs.CanRepairHull(out string hullMsg);
                string hullBtnText = _repairs.IsRepairingHull ? "Patching Hull..." : (canHull ? "🪵 [H] Patch Hull" : $"🪵 {hullMsg}");
                GUI.color = canHull ? new Color(0.7f, 1f, 0.7f) : (_repairs.IsRepairingHull ? Color.yellow : new Color(0.6f, 0.6f, 0.6f));
                if (GUI.Button(new Rect(posX + 10f, posY + 168f, btnW, 22f), hullBtnText, _btnStyle))
                {
                    _repairs.ExecuteHullRepair();
                }

                // [J] Mend Sails Button
                bool canSails = _repairs.CanRepairSails(out string sailsMsg);
                string sailsBtnText = _repairs.IsRepairingSails ? "Mending Sails..." : (canSails ? "⛵ [J] Mend Sails" : $"⛵ {sailsMsg}");
                GUI.color = canSails ? new Color(0.7f, 1f, 1f) : (_repairs.IsRepairingSails ? Color.cyan : new Color(0.6f, 0.6f, 0.6f));
                if (GUI.Button(new Rect(posX + 16f + btnW, posY + 168f, btnW, 22f), sailsBtnText, _btnStyle))
                {
                    _repairs.ExecuteSailsRepair();
                }

                GUI.color = Color.white;

                // Cooldown progress indicators underneath
                if (_repairs.HullCooldownNormalized > 0.001f)
                {
                    GUI.DrawTexture(new Rect(posX + 10f, posY + 192f, btnW * (1f - _repairs.HullCooldownNormalized), 3f), _greenTex);
                }
                if (_repairs.SailsCooldownNormalized > 0.001f)
                {
                    GUI.DrawTexture(new Rect(posX + 16f + btnW, posY + 192f, btnW * (1f - _repairs.SailsCooldownNormalized), 3f), _yellowTex);
                }
            }
        }

        private void DrawSubsystemCell(Rect rect, string title, float ratio, float current, float max)
        {
            GUI.DrawTexture(rect, _blackTex);

            Color statusCol = ratio > 0.70f ? _greenTex.GetPixel(0, 0)
                : (ratio > 0.30f ? _yellowTex.GetPixel(0, 0) : _redTex.GetPixel(0, 0));

            Texture2D fillTex = ratio > 0.70f ? _greenTex : (ratio > 0.30f ? _yellowTex : _redTex);

            // Inner fill bar
            float fillH = rect.height * Mathf.Clamp01(ratio);
            Rect fillRect = new Rect(rect.x + 1f, rect.y + rect.height - fillH, rect.width - 2f, fillH);
            GUI.DrawTexture(fillRect, fillTex);

            // Text Overlays
            float midY = rect.y + (rect.height * 0.5f) - 10f;
            GUI.Label(new Rect(rect.x, midY - 6f, rect.width, 14f), title, _subsystemTitleStyle);
            GUI.Label(new Rect(rect.x, midY + 7f, rect.width, 12f), $"{ratio * 100:F0}% ({current:F0})", _subsystemPercentStyle);
        }
    }
}
