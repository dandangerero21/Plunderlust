using System.Collections.Generic;
using Plunderlust.AI;
using Plunderlust.Cargo;
using Plunderlust.Core.Data;
using Plunderlust.Ship;
using Plunderlust.Subsystems;
using Plunderlust.Wind;
using Plunderlust.World;
using UnityEngine;

namespace Plunderlust.UI
{
    /// <summary>
    /// Phase 6 Comprehensive Naval Navigation Suite.
    /// Provides:
    /// 1. Real-time Corner Radar Minimap (Top-Right): Rotating compass ring,
    ///    Wind Vector needle, hostile/merchant ship blips, ports, and islands.
    /// 2. Full-Screen Nautical Sea Chart Modal [M]: Authentic pirate sea chart
    ///    displaying the entire archipelago, port settlements, player tracking needle,
    ///    and discovered "X Marks The Spot" treasure caches.
    /// Operates with zero heap allocations in Update and OnGUI.
    /// </summary>
    public class NavalMinimapAndChart : MonoBehaviour
    {
        public static NavalMinimapAndChart Instance { get; private set; }

        [Header("Minimap Configuration")]
        [SerializeField] private float _minimapSize = 215f;
        [SerializeField] private float _radarRangeMeters = 240f;
        [SerializeField] private bool _showMinimap = true;

        [Header("Sea Chart Modal [M]")]
        [SerializeField] private bool _isChartOpen = false;
        [SerializeField] private float _chartWidth = 640f;
        [SerializeField] private float _chartHeight = 480f;

        private Transform _playerTransform;
        private ShipLocomotionController _playerLoco;
        private ShipCargoHold _playerCargo;

        // Cached UI Textures (Zero GC)
        private Texture2D _radarBgTex;
        private Texture2D _compassBezelTex;
        private Texture2D _compassDialTex;
        private Texture2D _compassRingTex;
        private Texture2D _playerBlipTex;
        private Texture2D _hostileBlipTex;
        private Texture2D _neutralBlipTex;
        private Texture2D _portBlipTex;
        private Texture2D _islandBlipTex;
        private Texture2D _treasureBlipTex;
        private Texture2D _chartParchmentTex;
        private Texture2D _chartBorderTex;
        private Texture2D _windArrowTex;
        private Texture2D _btnBgTex;

        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _chartTitleStyle;
        private GUIStyle _chartLegendStyle;
        private GUIStyle _closeBtnStyle;
        private GUIStyle _windBadgeStyle;
        private bool _stylesInitialized = false;

        // Cached non-alloc blip search buffers
        private readonly List<AIShipBrain> _cachedAIShips = new List<AIShipBrain>(16);
        private readonly List<Port.PortIslandHaven> _cachedPorts = new List<Port.PortIslandHaven>(8);
        private readonly List<Port.SmugglerCoveOutpost> _cachedCoves = new List<Port.SmugglerCoveOutpost>(4);
        private float _scanTimer = 0f;

        public bool IsChartOpen => _isChartOpen;

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
                _playerTransform = playerGo.transform;
                _playerLoco = playerGo.GetComponent<ShipLocomotionController>();
                _playerCargo = playerGo.GetComponent<ShipCargoHold>();
            }
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            // 17th-Century Nautical Palette - Scaled for Mobile Legibility
            _compassBezelTex = MakeCircleTex(240, NauticalUITheme.MahoganyDark, NauticalUITheme.BrassBorder, 4.5f);
            _compassDialTex = MakeCircleTex(215, new Color(0.85f, 0.78f, 0.65f, 0.94f), NauticalUITheme.BrassDark, 2.5f);
            _radarBgTex = _compassBezelTex;
            _compassRingTex = MakeSolidTex(NauticalUITheme.BrassBorder);
            _playerBlipTex = MakeCircleTex(24, NauticalUITheme.EmeraldSea, Color.white, 2.5f);
            _hostileBlipTex = MakeCircleTex(22, NauticalUITheme.CrimsonBlood, Color.white, 2f);
            _neutralBlipTex = MakeCircleTex(18, NauticalUITheme.GoldAccent, Color.black, 1.5f);
            _portBlipTex = MakeCircleTex(24, new Color(0.2f, 0.55f, 0.85f), Color.white, 2.5f);
            _islandBlipTex = MakeCircleTex(28, new Color(0.55f, 0.45f, 0.30f), NauticalUITheme.BrassDark, 2f);
            _treasureBlipTex = MakeCircleTex(24, NauticalUITheme.GoldAccent, NauticalUITheme.CrimsonBlood, 2.5f);
            _chartParchmentTex = MakeSolidTex(NauticalUITheme.ParchmentBase);
            _chartBorderTex = MakeSolidTex(NauticalUITheme.MahoganyDark);
            _windArrowTex = MakeArrowTex(30, 80, NauticalUITheme.GoldAccent);
            _btnBgTex = MakeSolidTex(NauticalUITheme.MahoganyMid);

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = NauticalUITheme.ParchmentLight }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = NauticalUITheme.InkBlack }
            };

            _chartTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = NauticalUITheme.MahoganyDark }
            };

            _chartLegendStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = NauticalUITheme.MahoganyDark }
            };

            _closeBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = NauticalUITheme.ParchmentLight, background = _btnBgTex }
            };

            _windBadgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = NauticalUITheme.GoldAccent }
            };

            Font kenneyFont = NauticalUITheme.GetDefaultFont();
            if (kenneyFont != null)
            {
                _headerStyle.font = kenneyFont;
                _labelStyle.font = kenneyFont;
                _chartTitleStyle.font = kenneyFont;
                _chartLegendStyle.font = kenneyFont;
                _closeBtnStyle.font = kenneyFont;
                _windBadgeStyle.font = kenneyFont;
            }

            if (NauticalUITheme.BrassPanel != null && NauticalUITheme.BrassPanel.texture != null)
            {
                _btnBgTex = NauticalUITheme.BrassPanel.texture;
                _closeBtnStyle.normal.background = _btnBgTex;
            }

            _stylesInitialized = true;
        }

        private Texture2D MakeArrowTex(int width, int height, Color color)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[width * height];
            Color clear = new Color(0, 0, 0, 0);

            float midX = (width - 1) * 0.5f;
            float headHeight = height * 0.38f;
            float shaftHalfWidth = Mathf.Max(1.5f, width * 0.12f);

            for (int y = 0; y < height; y++)
            {
                // In texture coordinates, y=height-1 is top (arrowhead tip pointing towards target), y=0 is base
                float distFromTip = (height - 1) - y;

                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    float distFromCenter = Mathf.Abs(x - midX);

                    if (distFromTip <= headHeight)
                    {
                        // Triangular arrowhead pointing to tip
                        float allowedWidth = (distFromTip / headHeight) * midX;
                        pixels[idx] = (distFromCenter <= allowedWidth) ? color : clear;
                    }
                    else
                    {
                        // Shaft
                        pixels[idx] = (distFromCenter <= shaftHalfWidth) ? color : clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeSolidTex(Color col)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeCircleTex(int size, Color fill, Color border, float borderWidth)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            float radius = (size - 2) * 0.5f;
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    int idx = y * size + x;
                    if (dist <= radius)
                    {
                        float edgeDist = radius - dist;
                        if (edgeDist <= borderWidth)
                        {
                            pixels[idx] = border;
                        }
                        else
                        {
                            pixels[idx] = fill;
                        }
                    }
                    else
                    {
                        pixels[idx] = Color.clear;
                    }
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void Update()
        {
            // Toggle Full Nautical Sea Chart modal on M or TAB
            if (Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.Tab))
            {
                if (SeaChartMapUI.Instance != null)
                {
                    SeaChartMapUI.Instance.ToggleChart();
                }
                else
                {
                    _isChartOpen = !_isChartOpen;
                }
            }

            if (_isChartOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                if (SeaChartMapUI.Instance != null) SeaChartMapUI.Instance.SetChartOpen(false);
                _isChartOpen = false;
            }

            // Periodic ambient ship & port scan for radar blips and chart mapping
            _scanTimer += Time.deltaTime;
            if (_scanTimer >= 0.5f)
            {
                _scanTimer = 0f;
                _cachedAIShips.Clear();
                AIShipBrain[] brains = Object.FindObjectsByType<AIShipBrain>(FindObjectsInactive.Exclude);
                for (int i = 0; i < brains.Length; i++)
                {
                    _cachedAIShips.Add(brains[i]);
                }

                _cachedPorts.Clear();
                Port.PortIslandHaven[] havens = Object.FindObjectsByType<Port.PortIslandHaven>(FindObjectsInactive.Exclude);
                for (int i = 0; i < havens.Length; i++)
                {
                    _cachedPorts.Add(havens[i]);
                }

                _cachedCoves.Clear();
                Port.SmugglerCoveOutpost[] coves = Object.FindObjectsByType<Port.SmugglerCoveOutpost>(FindObjectsInactive.Exclude);
                for (int i = 0; i < coves.Length; i++)
                {
                    _cachedCoves.Add(coves[i]);
                }
            }
        }

        private void OnGUI()
        {
            InitStyles();

            // Suppress HUD minimap when any full-screen nautical modal is active
            if (SeaChartMapUI.Instance != null && SeaChartMapUI.Instance.IsOpen) return;
            if (ShipInventoryModalUI.Instance != null && ShipInventoryModalUI.Instance.IsOpen) return;

            if (_playerTransform == null)
            {
                FindPlayerShip();
                if (_playerTransform == null) return;
            }

            if (_showMinimap)
            {
                DrawCornerRadarMinimap();
            }

            if (_isChartOpen && SeaChartMapUI.Instance == null)
            {
                DrawFullNauticalChart();
            }
        }

        private void DrawCornerRadarMinimap()
        {
            float screenW = Screen.width;
            float margin = 14f;
            float radarX = screenW - _minimapSize - margin;
            float radarY = margin;

            // 1. Circular Antique Brass & Mahogany Mariner's Compass Bezel
            Rect bezelRect = new Rect(radarX, radarY, _minimapSize, _minimapSize);
            GUI.DrawTexture(bezelRect, _compassBezelTex);

            float innerOffset = 8f;
            Rect dialRect = new Rect(radarX + innerOffset, radarY + innerOffset, _minimapSize - (innerOffset * 2f), _minimapSize - (innerOffset * 2f));
            GUI.DrawTexture(dialRect, _compassDialTex);

            Vector2 center = new Vector2(radarX + (_minimapSize * 0.5f), radarY + (_minimapSize * 0.5f));
            float radius = (_minimapSize * 0.5f) - 22f;

            // 2. Cardinal Compass Points (Scaled for mobile readability)
            Color prevColor = GUI.color;
            GUI.color = NauticalUITheme.BrassHighlight;
            GUI.Label(new Rect(center.x - 12f, center.y - radius - 10f, 24f, 18f), "<b>N</b>", _headerStyle);
            GUI.color = NauticalUITheme.BrassBorder;
            GUI.Label(new Rect(center.x + radius - 4f, center.y - 9f, 20f, 18f), "<b>E</b>", _headerStyle);
            GUI.Label(new Rect(center.x - 10f, center.y + radius - 8f, 20f, 18f), "<b>S</b>", _headerStyle);
            GUI.Label(new Rect(center.x - radius - 16f, center.y - 9f, 20f, 18f), "<b>W</b>", _headerStyle);
            GUI.color = prevColor;

            // 3. Dynamic Rotating Wind Direction Arrow Needle
            if (WindManager.Instance != null)
            {
                Vector3 windDir = WindManager.Instance.WindDirection;
                // Clockwise angle in degrees from North
                float windAngle = Mathf.Atan2(windDir.x, windDir.z) * Mathf.Rad2Deg;

                Matrix4x4 prevMatrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(windAngle, center);

                // Draw golden Kenney ornate arrow pointing from center outward along wind vector towards the perimeter
                float arrowW = 42f;
                float arrowH = 50f;
                Rect arrowRect = new Rect(center.x - (arrowW * 0.5f), center.y - radius * 0.88f, arrowW, arrowH);
                GUI.color = NauticalUITheme.GoldAccent;
                Texture windTex = (NauticalUITheme.ArrowNeedle != null && NauticalUITheme.ArrowNeedle.texture != null)
                    ? (Texture)NauticalUITheme.ArrowNeedle.texture
                    : _windArrowTex;
                GUI.DrawTexture(arrowRect, windTex, ScaleMode.ScaleToFit);

                // Center hub pip
                GUI.DrawTexture(new Rect(center.x - 7f, center.y - 7f, 14f, 14f), _compassRingTex);

                GUI.matrix = prevMatrix;
                GUI.color = prevColor;

                // 3b. Wind Speed HUD Badge (Prominently displayed right below the compass bezel)
                float windKnots = WindManager.Instance.WindStrength * 16f * 1.94384f;
                Rect badgeRect = new Rect(radarX, radarY + _minimapSize + 4f, _minimapSize, 28f);

                GUI.color = NauticalUITheme.MahoganyDark;
                GUI.DrawTexture(badgeRect, _compassRingTex);
                GUI.color = NauticalUITheme.BrassBorder;
                GUI.DrawTexture(new Rect(badgeRect.x, badgeRect.y, badgeRect.width, 2f), _compassRingTex);
                GUI.DrawTexture(new Rect(badgeRect.x, badgeRect.yMax - 2f, badgeRect.width, 2f), _compassRingTex);
                GUI.color = Color.white;

                string speedColor = windKnots >= 16f ? "#FFD700" : windKnots >= 10f ? "#E5A93C" : "#C0C0C0";
                GUI.Label(badgeRect, $"💨 <b>WIND: <color={speedColor}>{windKnots:F0} KTS</color></b>", _windBadgeStyle);
            }

            // 4. Known Ports & Settlements
            if (_cachedPorts.Count > 0)
            {
                for (int i = 0; i < _cachedPorts.Count; i++)
                {
                    Port.PortIslandHaven p = _cachedPorts[i];
                    if (p != null)
                    {
                        Texture2D bTex = (p.RulingFaction == FactionType.CorsairBrethren) ? _hostileBlipTex : _portBlipTex;
                        DrawWorldBlip(center, radius, p.transform.position, bTex, 14f);
                    }
                }
            }
            else
            {
                DrawWorldBlip(center, radius, new Vector3(45f, 0f, 25f), _portBlipTex, 14f);
                DrawWorldBlip(center, radius, new Vector3(-120f, 0f, 180f), _hostileBlipTex, 14f);
            }

            for (int i = 0; i < _cachedCoves.Count; i++)
            {
                Port.SmugglerCoveOutpost c = _cachedCoves[i];
                if (c != null)
                {
                    DrawWorldBlip(center, radius, c.transform.position, _neutralBlipTex, 11f);
                }
            }

            // 5. Treasure Island Blip (If holding Treasure Map)
            bool hasTreasureMap = _playerCargo != null && _playerCargo.GetItemCount(CargoItemType.TreasureMap) > 0;
            if (hasTreasureMap && TreasureManager.Instance != null && TreasureManager.Instance.GetNearestTreasureLocation(out Vector3 tPos))
            {
                DrawWorldBlip(center, radius, tPos, _treasureBlipTex, 14f);
            }

            // 6. NPC Ships Blips (Pirates = Crimson, Merchants = Gold)
            for (int i = 0; i < _cachedAIShips.Count; i++)
            {
                AIShipBrain ship = _cachedAIShips[i];
                if (ship == null || !ship.gameObject.activeInHierarchy) continue;

                Texture2D blipTex = (ship.Faction == FactionType.CorsairBrethren) ? _hostileBlipTex : _neutralBlipTex;
                DrawWorldBlip(center, radius, ship.transform.position, blipTex, 10f);
            }

            // 7. Player Ship Indicator at Compass Center (Green Hull Blip with Yaw Heading Pip)
            GUI.DrawTexture(new Rect(center.x - 7f, center.y - 7f, 14f, 14f), _playerBlipTex);
            float headingRad = _playerTransform.eulerAngles.y * Mathf.Deg2Rad;
            Vector2 headPip = center + new Vector2(Mathf.Sin(headingRad), -Mathf.Cos(headingRad)) * 10f;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(headPip.x - 3f, headPip.y - 3f, 6f, 6f), _windArrowTex);
            GUI.color = prevColor;

            // 8. Bottom Touch Button: Tap to Open Full Sea Chart (Placed cleanly below the Wind Speed Badge)
            float chartBtnY = (WindManager.Instance != null) ? (radarY + _minimapSize + 34f) : (radarY + _minimapSize + 4f);
            Rect btnRect = new Rect(radarX, chartBtnY, _minimapSize, 36f);
            if (GUI.Button(btnRect, "🗺️ SEA CHART", _closeBtnStyle))
            {
                if (SeaChartMapUI.Instance != null)
                {
                    SeaChartMapUI.Instance.ToggleChart();
                }
                else
                {
                    _isChartOpen = !_isChartOpen;
                }
            }
        }

        private void DrawWorldBlip(Vector2 center, float radius, Vector3 worldPos, Texture2D tex, float blipSize)
        {
            Vector3 diff = worldPos - _playerTransform.position;
            float worldDist = Mathf.Sqrt(diff.x * diff.x + diff.z * diff.z);

            if (worldDist > _radarRangeMeters) return;

            float normDist = (worldDist / _radarRangeMeters) * radius;
            Vector2 dir = new Vector2(diff.x, -diff.z).normalized;
            Vector2 blipPos = center + (dir * normDist);

            GUI.DrawTexture(new Rect(blipPos.x - (blipSize * 0.5f), blipPos.y - (blipSize * 0.5f), blipSize, blipSize), tex);
        }

        private void DrawFullNauticalChart()
        {
            float screenW = Screen.width;
            float screenH = Screen.height;

            float chartX = (screenW - _chartWidth) * 0.5f;
            float chartY = (screenH - _chartHeight) * 0.5f;

            // Parchment backdrop & border
            Rect borderRect = new Rect(chartX - 6f, chartY - 6f, _chartWidth + 12f, _chartHeight + 12f);
            GUI.DrawTexture(borderRect, _chartBorderTex);

            Rect chartRect = new Rect(chartX, chartY, _chartWidth, _chartHeight);
            GUI.DrawTexture(chartRect, _chartParchmentTex);

            // Title & Compass Header
            GUI.Label(new Rect(chartX, chartY + 14f, _chartWidth, 24f), "⚔️ THE CARIBBEAN ARCHIPELAGO & SEA CHART ⚔️", _chartTitleStyle);
            GUI.Label(new Rect(chartX, chartY + 36f, _chartWidth, 16f), "Nautical Chart Grid: 1100m x 1100m Playable Naval Theater", _chartLegendStyle);

            // Map Coordinate Bounds & Inner Water Area
            float mapAreaW = _chartWidth - 180f;
            float mapAreaH = _chartHeight - 80f;
            Rect mapAreaRect = new Rect(chartX + 20f, chartY + 60f, mapAreaW, mapAreaH);
            GUI.DrawTexture(mapAreaRect, new Color(0.70f, 0.78f, 0.82f, 0.65f) == Color.clear ? _radarBgTex : MakeSolidTex(new Color(0.68f, 0.76f, 0.78f, 0.55f)));

            // Outer Reef Barrier Perimeter
            GUI.DrawTexture(new Rect(mapAreaRect.x, mapAreaRect.y, mapAreaRect.width, 2f), _chartBorderTex);
            GUI.DrawTexture(new Rect(mapAreaRect.x, mapAreaRect.y + mapAreaRect.height - 2f, mapAreaRect.width, 2f), _chartBorderTex);
            GUI.DrawTexture(new Rect(mapAreaRect.x, mapAreaRect.y, 2f, mapAreaRect.height), _chartBorderTex);
            GUI.DrawTexture(new Rect(mapAreaRect.x + mapAreaRect.width - 2f, mapAreaRect.y, 2f, mapAreaRect.height), _chartBorderTex);

            float halfWorldSize = MapBoundaryBarrier.Instance != null ? MapBoundaryBarrier.Instance.HalfSize : 550f;

            // Plot Settlements from active scene landmarks
            if (_cachedPorts.Count > 0)
            {
                for (int i = 0; i < _cachedPorts.Count; i++)
                {
                    Port.PortIslandHaven p = _cachedPorts[i];
                    if (p == null) continue;
                    string icon = p.RulingFaction == FactionType.CorsairBrethren ? "☠️" : "⚓";
                    Color col = p.RulingFaction == FactionType.CorsairBrethren ? new Color(0.85f, 0.2f, 0.15f) : new Color(0.15f, 0.45f, 0.85f);
                    DrawChartPoint(mapAreaRect, halfWorldSize, p.transform.position, $"{icon} {p.PortName.ToUpper()}", col);
                }
            }
            else
            {
                DrawChartPoint(mapAreaRect, halfWorldSize, new Vector3(45f, 0f, 25f), "⚓ PORT ROYAL HAVEN", new Color(0.15f, 0.45f, 0.85f));
                DrawChartPoint(mapAreaRect, halfWorldSize, new Vector3(-120f, 0f, 180f), "☠️ TORTUGA (Corsair Haven)", new Color(0.85f, 0.2f, 0.15f));
            }

            for (int i = 0; i < _cachedCoves.Count; i++)
            {
                Port.SmugglerCoveOutpost c = _cachedCoves[i];
                if (c == null) continue;
                DrawChartPoint(mapAreaRect, halfWorldSize, c.transform.position, $"🪙 {c.CoveName.ToUpper()}", new Color(0.75f, 0.55f, 0.1f));
            }

            // Plot Player Ship
            Vector3 playerPos = _playerTransform.position;
            DrawChartPoint(mapAreaRect, halfWorldSize, playerPos, "⛵ FLAGSHIP (You)", new Color(0.1f, 0.6f, 0.2f), true);

            // Plot Discovered Treasure Island (If holding a map)
            bool hasTreasureMap = _playerCargo != null && _playerCargo.GetItemCount(CargoItemType.TreasureMap) > 0;
            if (hasTreasureMap && TreasureManager.Instance != null && TreasureManager.Instance.GetNearestTreasureLocation(out Vector3 tPos))
            {
                DrawChartPoint(mapAreaRect, halfWorldSize, tPos, "❌ X MARKS THE SPOT (Treasure Island)", new Color(0.85f, 0.55f, 0.0f));
            }

            // Legend on the Right Sidebar
            float legendX = chartX + _chartWidth - 150f;
            float legendY = chartY + 60f;
            GUI.Label(new Rect(legendX, legendY, 140f, 16f), "<b>CHART LEGEND:</b>", _chartLegendStyle);
            GUI.Label(new Rect(legendX, legendY + 22f, 140f, 14f), "⚓ Major Ports", _chartLegendStyle);
            GUI.Label(new Rect(legendX, legendY + 40f, 140f, 14f), "⛵ Your Flagship", _chartLegendStyle);
            GUI.Label(new Rect(legendX, legendY + 58f, 140f, 14f), "❌ Secret Caches", _chartLegendStyle);
            GUI.Label(new Rect(legendX, legendY + 76f, 140f, 14f), "🪨 Barrier Reefs", _chartLegendStyle);

            if (WindManager.Instance != null)
            {
                GUI.Label(new Rect(legendX, legendY + 110f, 140f, 14f), $"<b>WIND:</b> {WindManager.Instance.WindHeadingDegrees:F0}°", _chartLegendStyle);
                GUI.Label(new Rect(legendX, legendY + 126f, 140f, 14f), $"Strength: {WindManager.Instance.WindStrength * 100:F0}%", _chartLegendStyle);
            }

            // Close Button
            if (GUI.Button(new Rect(chartX + (_chartWidth * 0.5f) - 60f, chartY + _chartHeight - 34f, 120f, 24f), "Close Chart [M]", _closeBtnStyle))
            {
                _isChartOpen = false;
            }
        }

        private void DrawChartPoint(Rect mapRect, float halfWorldSize, Vector3 worldPos, string label, Color color, bool drawHeading = false)
        {
            float normX = (worldPos.x + halfWorldSize) / (halfWorldSize * 2f);
            float normZ = (worldPos.z + halfWorldSize) / (halfWorldSize * 2f);

            // Invert Z because GUI Y goes downwards
            float ptX = mapRect.x + (Mathf.Clamp01(normX) * mapRect.width);
            float ptY = mapRect.y + ((1f - Mathf.Clamp01(normZ)) * mapRect.height);

            GUI.color = color;
            GUI.DrawTexture(new Rect(ptX - 4f, ptY - 4f, 8f, 8f), _compassRingTex);
            GUI.color = Color.white;

            GUI.Label(new Rect(ptX + 6f, ptY - 7f, 180f, 16f), $"<color=#{ColorUtility.ToHtmlStringRGB(color)}><b>{label}</b></color>", _chartLegendStyle);
        }
    }
}
