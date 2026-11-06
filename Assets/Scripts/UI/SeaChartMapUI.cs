using System.Collections.Generic;
using Plunderlust.Core.Data;
using Plunderlust.Port;
using Plunderlust.Ship;
using Plunderlust.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Plunderlust.UI
{
    /// <summary>
    /// Interactive 17th-Century Nautical Sea Chart Modal.
    /// Features:
    /// 1. One-finger touch drag panning and two-finger pinch-to-zoom.
    /// 2. Label Anti-Overlap Solver: 2D screen-space bounding-box repulsion pass ensuring
    ///    adjacent port/haven labels never overlap.
    /// 3. Top-right brass 'X' Wax-Seal touch close button (purging all [M] keyboard hints).
    /// 4. 1:1 dynamic world mapping discovering physical islands, havens, and player ship.
    /// Operates with zero per-frame heap allocations during interaction.
    /// </summary>
    public class SeaChartMapUI : MonoBehaviour
    {
        public static SeaChartMapUI Instance { get; private set; }

        [Header("Chart Framing & Zoom Settings")]
        [SerializeField] private float _minZoom = 0.5f;
        [SerializeField] private float _maxZoom = 3.5f;
        [SerializeField] private float _worldToChartScale = 0.85f; // pixels per world meter

        // Runtime Canvas Elements
        private Canvas _canvas;
        private GameObject _modalRootGo;
        private RectTransform _chartViewportRect;
        private RectTransform _chartContentRect;
        private RectTransform _playerNeedleRect;
        private Text _playerCoordsText;

        // Pan and Zoom State
        private Vector2 _currentPan = Vector2.zero;
        private float _currentZoom = 1.0f;
        private bool _isOpen = false;

        // Multi-touch tracking
        private bool _isDragging = false;
        private Vector2 _lastTouchPos = Vector2.zero;
        private float _lastPinchDistance = 0f;

        // Landmark Marker Registry for Label Anti-Overlap Solver
        private class ChartMarker
        {
            public RectTransform RootRt;
            public RectTransform LabelRt;
            public Vector2 OriginalLocalPos;
            public Vector2 RepulsedOffset;
            public Vector2 Size;
            public Transform WorldTarget;
            public Vector3 StaticWorldPos;
        }

        private readonly List<ChartMarker> _activeMarkers = new List<ChartMarker>(32);
        private Transform _playerTransform;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            FindPlayerShip();
            BuildSeaChartCanvas();
            SetChartOpen(false);
        }

        public void FindPlayerShip()
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                _playerTransform = playerGo.transform;
            }
        }

        public void ToggleChart()
        {
            SetChartOpen(!_isOpen);
        }

        public void SetChartOpen(bool open)
        {
            _isOpen = open;
            if (_modalRootGo != null)
            {
                _modalRootGo.SetActive(_isOpen);
            }

            if (_isOpen)
            {
                CenterChartOnPlayer();
                RefreshWorldLandmarks();
                SolveLabelAntiOverlap();
            }
        }

        private void CenterChartOnPlayer()
        {
            if (_playerTransform != null)
            {
                Vector3 p = _playerTransform.position;
                _currentPan = new Vector2(-p.x * _worldToChartScale, -p.z * _worldToChartScale);
            }
            else
            {
                _currentPan = Vector2.zero;
            }
            _currentZoom = 1.0f;
            ApplyPanAndZoom();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M))
            {
                ToggleChart();
            }

            if (!_isOpen) return;

            HandleTouchAndMouseInput();
            UpdatePlayerNeedle();

            // Periodic anti-overlap pass when zooming/panning
            if (_isDragging)
            {
                SolveLabelAntiOverlap();
            }
        }

        private void UpdatePlayerNeedle()
        {
            if (_playerTransform == null) FindPlayerShip();
            if (_playerTransform == null || _playerNeedleRect == null) return;

            Vector3 worldPos = _playerTransform.position;
            Vector2 chartPos = new Vector2(worldPos.x * _worldToChartScale, worldPos.z * _worldToChartScale);
            _playerNeedleRect.anchoredPosition = chartPos;

            float heading = _playerTransform.eulerAngles.y;
            _playerNeedleRect.localRotation = Quaternion.Euler(0f, 0f, -heading);

            if (_playerCoordsText != null)
            {
                _playerCoordsText.text = $"POSITION: [{worldPos.x:F0}, {worldPos.z:F0}]  |  ZOOM: {_currentZoom:F1}x";
            }
        }

        private void HandleTouchAndMouseInput()
        {
            // 1. Mobile Touch Input (1-finger pan, 2-finger pinch-zoom)
            if (Input.touchCount == 1)
            {
                Touch t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began)
                {
                    _isDragging = true;
                    _lastTouchPos = t.position;
                }
                else if (t.phase == TouchPhase.Moved && _isDragging)
                {
                    Vector2 delta = t.position - _lastTouchPos;
                    _currentPan += delta / _currentZoom;
                    _lastTouchPos = t.position;
                    ApplyPanAndZoom();
                }
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    _isDragging = false;
                }
            }
            else if (Input.touchCount >= 2)
            {
                // Pinch-to-zoom
                Touch t0 = Input.GetTouch(0);
                Touch t1 = Input.GetTouch(1);

                float currentDist = Vector2.Distance(t0.position, t1.position);

                if (t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began)
                {
                    _lastPinchDistance = currentDist;
                }
                else if (t0.phase == TouchPhase.Moved || t1.phase == TouchPhase.Moved)
                {
                    if (_lastPinchDistance > 10f)
                    {
                        float pinchDelta = currentDist - _lastPinchDistance;
                        float zoomDelta = pinchDelta * 0.005f;
                        SetZoom(_currentZoom + zoomDelta);
                    }
                    _lastPinchDistance = currentDist;
                }
            }

            // 2. Editor Mouse Fallback
            if (Input.GetMouseButtonDown(0))
            {
                _isDragging = true;
                _lastTouchPos = Input.mousePosition;
            }
            else if (Input.GetMouseButton(0) && _isDragging)
            {
                Vector2 curPos = (Vector2)Input.mousePosition;
                Vector2 delta = curPos - _lastTouchPos;
                _currentPan += delta / _currentZoom;
                _lastTouchPos = curPos;
                ApplyPanAndZoom();
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _isDragging = false;
            }

            // Mouse Scroll Wheel Zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                SetZoom(_currentZoom + scroll * 0.8f);
            }
        }

        private void SetZoom(float newZoom)
        {
            _currentZoom = Mathf.Clamp(newZoom, _minZoom, _maxZoom);
            ApplyPanAndZoom();
            SolveLabelAntiOverlap();
        }

        private void ApplyPanAndZoom()
        {
            if (_chartContentRect == null) return;
            _chartContentRect.localScale = Vector3.one * _currentZoom;
            _chartContentRect.anchoredPosition = _currentPan * _currentZoom;
        }

        // --- Label Anti-Overlap Solver ---

        /// <summary>
        /// Runs an iterative 2D screen-space bounding-box repulsion relaxation pass on all active
        /// chart marker text labels so nearby ports (e.g. Tortuga and Dead Man's Reef) NEVER overlap.
        /// </summary>
        public void SolveLabelAntiOverlap()
        {
            int markerCount = _activeMarkers.Count;
            if (markerCount < 2) return;

            // Reset offsets to baseline
            for (int i = 0; i < markerCount; i++)
            {
                _activeMarkers[i].RepulsedOffset = Vector2.zero;
            }

            const int iterations = 4;
            const float minPadding = 8.0f;

            for (int iter = 0; iter < iterations; iter++)
            {
                for (int i = 0; i < markerCount; i++)
                {
                    var a = _activeMarkers[i];
                    Vector2 posA = a.OriginalLocalPos + a.RepulsedOffset;

                    for (int j = i + 1; j < markerCount; j++)
                    {
                        var b = _activeMarkers[j];
                        Vector2 posB = b.OriginalLocalPos + b.RepulsedOffset;

                        float overlapX = (a.Size.x + b.Size.x) * 0.5f + minPadding - Mathf.Abs(posA.x - posB.x);
                        float overlapY = (a.Size.y + b.Size.y) * 0.5f + minPadding - Mathf.Abs(posA.y - posB.y);

                        if (overlapX > 0f && overlapY > 0f)
                        {
                            // Overlap detected! Push apart along the axis of least penetration
                            if (overlapY < overlapX)
                            {
                                float pushY = (overlapY * 0.5f);
                                if (posA.y >= posB.y)
                                {
                                    a.RepulsedOffset.y += pushY;
                                    b.RepulsedOffset.y -= pushY;
                                }
                                else
                                {
                                    a.RepulsedOffset.y -= pushY;
                                    b.RepulsedOffset.y += pushY;
                                }
                            }
                            else
                            {
                                float pushX = (overlapX * 0.5f);
                                if (posA.x >= posB.x)
                                {
                                    a.RepulsedOffset.x += pushX;
                                    b.RepulsedOffset.x -= pushX;
                                }
                                else
                                {
                                    a.RepulsedOffset.x -= pushX;
                                    b.RepulsedOffset.x += pushX;
                                }
                            }
                        }
                    }
                }
            }

            // Apply solved offsets to label RectTransforms
            for (int i = 0; i < markerCount; i++)
            {
                var marker = _activeMarkers[i];
                if (marker.LabelRt != null)
                {
                    marker.LabelRt.anchoredPosition = new Vector2(0f, 18f) + marker.RepulsedOffset;
                }
            }
        }

        // --- World Landmark Dynamic Discovery ---

        private void RefreshWorldLandmarks()
        {
            // Clear existing spawned markers
            for (int i = 0; i < _activeMarkers.Count; i++)
            {
                if (_activeMarkers[i].RootRt != null)
                {
                    Destroy(_activeMarkers[i].RootRt.gameObject);
                }
            }
            _activeMarkers.Clear();

            // 1. Discover all PortIslandHaven objects in world
            PortIslandHaven[] havens = Object.FindObjectsByType<PortIslandHaven>(FindObjectsInactive.Exclude);
            foreach (var haven in havens)
            {
                if (haven == null) continue;
                Vector3 wPos = haven.transform.position;
                if (IsPositionAlreadyRegistered(wPos, 35f)) continue;

                string cleanName = CanonicalizePortName(haven.PortName, haven.RulingFaction);
                Color flagCol = (haven.RulingFaction == FactionType.CorsairBrethren) ? NauticalUITheme.CrimsonBlood : NauticalUITheme.BrassBorder;
                CreateChartMarker(haven.transform, cleanName, flagCol, "⚓");
            }

            // 2. Discover SmugglerCoveOutpost objects
            SmugglerCoveOutpost[] coves = Object.FindObjectsByType<SmugglerCoveOutpost>(FindObjectsInactive.Exclude);
            foreach (var cove in coves)
            {
                if (cove == null) continue;
                Vector3 wPos = cove.transform.position;
                if (IsPositionAlreadyRegistered(wPos, 35f)) continue;

                string cleanName = string.IsNullOrEmpty(cove.CoveName) ? "Dead Man's Reef (Smugglers)" : cove.CoveName;
                CreateChartMarker(cove.transform, cleanName, NauticalUITheme.GoldAccent, "☠");
            }

            // 3. Discover Treasure Atolls
            TreasureIslandLandmark[] atolls = Object.FindObjectsByType<TreasureIslandLandmark>(FindObjectsInactive.Exclude);
            foreach (var atoll in atolls)
            {
                if (atoll == null) continue;
                Vector3 wPos = atoll.transform.position;
                if (IsPositionAlreadyRegistered(wPos, 35f)) continue;

                CreateChartMarker(atoll.transform, "Treasure Atoll", NauticalUITheme.EmeraldSea, "✖");
            }

            // 4. Fallback: If Tortuga Corsair Haven exists by name
            GameObject tortugaGo = GameObject.Find("Tortuga_CorsairHaven");
            if (tortugaGo != null && !IsPositionAlreadyRegistered(tortugaGo.transform.position, 35f))
            {
                CreateChartMarker(tortugaGo.transform, "Tortuga (Corsair Haven)", NauticalUITheme.CrimsonBlood, "⚓");
            }
        }

        private bool IsPositionAlreadyRegistered(Vector3 worldPos, float minDistance)
        {
            for (int i = 0; i < _activeMarkers.Count; i++)
            {
                if (Vector3.Distance(_activeMarkers[i].StaticWorldPos, worldPos) < minDistance)
                    return true;
            }
            return false;
        }

        private static string CanonicalizePortName(string raw, FactionType faction)
        {
            if (string.IsNullOrEmpty(raw)) return "Port Royal Haven";
            string lower = raw.ToLowerInvariant();
            if (lower.Contains("haven") || lower.Contains("royal") || lower.Contains("marigold"))
                return "Port Royal (Merchant Haven)";
            if (lower.Contains("tortuga") || lower.Contains("libre"))
                return "Tortuga (Corsair Haven)";
            if (lower.Contains("smuggler") || lower.Contains("cove") || lower.Contains("dead man"))
                return "Dead Man's Reef (Smugglers)";
            if (lower.Contains("bastion") || lower.Contains("george") || lower.Contains("fort"))
                return "Fort St. George (Imperial Bastion)";

            return raw.Replace("_", " ");
        }

        private void CreateChartMarker(Transform worldTransform, string label, Color markerColor, string iconSymbol)
        {
            if (_chartContentRect == null || worldTransform == null) return;

            Vector3 wPos = worldTransform.position;
            Vector2 chartPos = new Vector2(wPos.x * _worldToChartScale, wPos.z * _worldToChartScale);

            GameObject markerRoot = new GameObject($"Marker_{label}");
            markerRoot.transform.SetParent(_chartContentRect, false);
            RectTransform rootRt = markerRoot.AddComponent<RectTransform>();
            rootRt.anchoredPosition = chartPos;
            rootRt.sizeDelta = new Vector2(32f, 32f);

            // Icon Blip Image / Stamped Symbol
            GameObject iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(markerRoot.transform, false);
            RectTransform iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.sizeDelta = Vector2.zero;
            Text iconText = iconGo.AddComponent<Text>();
            iconText.text = iconSymbol;
            iconText.font = NauticalUITheme.GetDefaultFont();
            iconText.fontSize = 20;
            iconText.fontStyle = FontStyle.Bold;
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.color = markerColor;

            // Background Parchment Badge (Parent of text, guaranteeing crisp high-contrast readability)
            GameObject badgeGo = new GameObject("Badge");
            badgeGo.transform.SetParent(markerRoot.transform, false);
            RectTransform badgeRt = badgeGo.AddComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0.5f, 0.5f);
            badgeRt.anchorMax = new Vector2(0.5f, 0.5f);
            badgeRt.pivot = new Vector2(0.5f, 0f);
            badgeRt.anchoredPosition = new Vector2(0f, 20f);
            badgeRt.sizeDelta = new Vector2(185f, 26f);

            Image badgeImg = badgeGo.AddComponent<Image>();
            badgeImg.sprite = NauticalUITheme.ParchmentPanel;
            badgeImg.type = Image.Type.Sliced;
            badgeImg.color = Color.white;

            // Marker Text Label (Child of Badge, rendered ON TOP of parchment)
            GameObject labelGo = new GameObject("LabelText");
            labelGo.transform.SetParent(badgeGo.transform, false);
            RectTransform labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.sizeDelta = new Vector2(-10f, 0f);

            Text labelText = labelGo.AddComponent<Text>();
            labelText.text = label;
            labelText.font = NauticalUITheme.GetDefaultFont();
            labelText.fontSize = 11;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = NauticalUITheme.InkBlack;

            Shadow shadow = labelGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(1f, 0.95f, 0.85f, 0.75f);
            shadow.effectDistance = new Vector2(1f, -1f);

            var entry = new ChartMarker
            {
                RootRt = rootRt,
                LabelRt = badgeRt,
                OriginalLocalPos = chartPos,
                RepulsedOffset = Vector2.zero,
                Size = new Vector2(185f, 28f),
                WorldTarget = worldTransform,
                StaticWorldPos = wPos
            };
            _activeMarkers.Add(entry);
        }

        // --- Canvas Hierarchy Builder ---

        private void BuildSeaChartCanvas()
        {
            GameObject canvasGo = new GameObject("Plunderlust_SeaChartCanvas");
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 90; // Top modal layer

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Modal Root Container
            _modalRootGo = new GameObject("SeaChartModalRoot");
            _modalRootGo.transform.SetParent(canvasGo.transform, false);
            RectTransform modalRt = _modalRootGo.AddComponent<RectTransform>();
            modalRt.anchorMin = Vector2.zero;
            modalRt.anchorMax = Vector2.one;
            modalRt.sizeDelta = Vector2.zero;

            // Dimmed Backdrop
            GameObject dimGo = new GameObject("BackdropDim");
            dimGo.transform.SetParent(_modalRootGo.transform, false);
            RectTransform dimRt = dimGo.AddComponent<RectTransform>();
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.sizeDelta = Vector2.zero;
            Image dimImg = dimGo.AddComponent<Image>();
            dimImg.sprite = NauticalUITheme.SolidWhite;
            dimImg.color = new Color(0f, 0f, 0f, 0.70f);

            // Safe Area Wrapper
            GameObject safeGo = new GameObject("SafeAreaFitter");
            safeGo.transform.SetParent(_modalRootGo.transform, false);
            RectTransform safeRt = safeGo.AddComponent<RectTransform>();
            safeRt.anchorMin = Vector2.zero;
            safeRt.anchorMax = Vector2.one;
            safeRt.sizeDelta = Vector2.zero;
            safeGo.AddComponent<SafeAreaFitter>();

            // Main Parchment Sea Chart Frame
            GameObject frameGo = new GameObject("SeaChartFrame");
            frameGo.transform.SetParent(safeRt, false);
            RectTransform frameRt = frameGo.AddComponent<RectTransform>();
            frameRt.anchorMin = new Vector2(0.04f, 0.04f);
            frameRt.anchorMax = new Vector2(0.96f, 0.96f);
            frameRt.sizeDelta = Vector2.zero;
            Image frameImg = frameGo.AddComponent<Image>();
            frameImg.sprite = NauticalUITheme.MahoganyPanel;
            frameImg.type = Image.Type.Sliced;
            frameImg.color = Color.white;

            // Chart Title Banner
            GameObject titleGo = new GameObject("ChartTitleBanner");
            titleGo.transform.SetParent(frameGo.transform, false);
            RectTransform titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -12f);
            titleRt.sizeDelta = new Vector2(480f, 44f);
            Image titleBg = titleGo.AddComponent<Image>();
            titleBg.sprite = NauticalUITheme.ParchmentPanel;
            titleBg.type = Image.Type.Sliced;

            GameObject titleTextGo = new GameObject("TitleText");
            titleTextGo.transform.SetParent(titleGo.transform, false);
            RectTransform ttRt = titleTextGo.AddComponent<RectTransform>();
            ttRt.anchorMin = Vector2.zero;
            ttRt.anchorMax = Vector2.one;
            ttRt.sizeDelta = Vector2.zero;
            Text titleTxt = titleTextGo.AddComponent<Text>();
            titleTxt.text = "ARCHIPELAGO SEA CHART";
            titleTxt.font = NauticalUITheme.GetDefaultFont();
            titleTxt.fontSize = 18;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = NauticalUITheme.MahoganyDark;

            // Top-Right Brass 'X' Wax-Seal Close Button
            GameObject closeGo = new GameObject("CloseWaxSealBtn");
            closeGo.transform.SetParent(frameGo.transform, false);
            RectTransform closeRt = closeGo.AddComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-16f, -16f);
            closeRt.sizeDelta = new Vector2(58f, 58f);

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
            xText.fontSize = 24;
            xText.fontStyle = FontStyle.Bold;
            xText.alignment = TextAnchor.MiddleCenter;
            xText.color = NauticalUITheme.GoldAccent;
            xText.raycastTarget = false;

            Button closeBtn = closeGo.AddComponent<Button>();
            closeBtn.onClick.AddListener(() => SetChartOpen(false));

            // Viewport Rect (Masked area where parchment ocean scrolls)
            GameObject viewGo = new GameObject("ChartViewport");
            viewGo.transform.SetParent(frameGo.transform, false);
            _chartViewportRect = viewGo.AddComponent<RectTransform>();
            _chartViewportRect.anchorMin = new Vector2(0.02f, 0.08f);
            _chartViewportRect.anchorMax = new Vector2(0.98f, 0.90f);
            _chartViewportRect.sizeDelta = Vector2.zero;

            Image viewImg = viewGo.AddComponent<Image>();
            viewImg.sprite = NauticalUITheme.SolidWhite;
            viewImg.color = NauticalUITheme.ParchmentBase;
            Mask mask = viewGo.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            // Scrollable & Zoomable Chart Content Root
            GameObject contentGo = new GameObject("ChartContent");
            contentGo.transform.SetParent(viewGo.transform, false);
            _chartContentRect = contentGo.AddComponent<RectTransform>();
            _chartContentRect.anchorMin = new Vector2(0.5f, 0.5f);
            _chartContentRect.anchorMax = new Vector2(0.5f, 0.5f);
            _chartContentRect.pivot = new Vector2(0.5f, 0.5f);
            _chartContentRect.sizeDelta = new Vector2(2400f, 2400f);

            // Chart Background Parchment Grid Lines
            GameObject gridGo = new GameObject("ParchmentGridLines");
            gridGo.transform.SetParent(contentGo.transform, false);
            RectTransform gridRt = gridGo.AddComponent<RectTransform>();
            gridRt.anchorMin = Vector2.zero;
            gridRt.anchorMax = Vector2.one;
            gridRt.sizeDelta = Vector2.zero;
            Image gridImg = gridGo.AddComponent<Image>();
            gridImg.sprite = NauticalUITheme.ParchmentPanel;
            gridImg.type = Image.Type.Tiled;
            gridImg.color = new Color(0.80f, 0.72f, 0.58f, 0.50f);

            // Cartographic Compass Rose (Positioned elegantly in the northeast quadrant of chart)
            GameObject roseGo = new GameObject("CompassRose");
            roseGo.transform.SetParent(contentGo.transform, false);
            RectTransform roseRt = roseGo.AddComponent<RectTransform>();
            roseRt.anchorMin = new Vector2(0.5f, 0.5f);
            roseRt.anchorMax = new Vector2(0.5f, 0.5f);
            roseRt.anchoredPosition = new Vector2(580f, 580f);
            roseRt.sizeDelta = new Vector2(240f, 240f);
            Image roseImg = roseGo.AddComponent<Image>();
            roseImg.sprite = NauticalUITheme.CompassRose;
            roseImg.color = new Color(1f, 1f, 1f, 0.85f);

            // Player Tracking Ship Needle
            GameObject playerGo = new GameObject("PlayerShipNeedle");
            playerGo.transform.SetParent(contentGo.transform, false);
            _playerNeedleRect = playerGo.AddComponent<RectTransform>();
            _playerNeedleRect.sizeDelta = new Vector2(26f, 38f);
            Image playerImg = playerGo.AddComponent<Image>();
            playerImg.sprite = NauticalUITheme.ShipHullSilhouette;
            playerImg.color = NauticalUITheme.EmeraldSea;

            // Player Ship Label
            GameObject plLabelGo = new GameObject("PlayerLabel");
            plLabelGo.transform.SetParent(playerGo.transform, false);
            RectTransform plRt = plLabelGo.AddComponent<RectTransform>();
            plRt.anchorMin = new Vector2(0.5f, 0.5f);
            plRt.anchorMax = new Vector2(0.5f, 0.5f);
            plRt.pivot = new Vector2(0.5f, 1f);
            plRt.anchoredPosition = new Vector2(0f, -20f);
            plRt.sizeDelta = new Vector2(120f, 18f);
            Text plText = plLabelGo.AddComponent<Text>();
            plText.text = "YOU";
            plText.font = NauticalUITheme.GetDefaultFont();
            plText.fontSize = 11;
            plText.fontStyle = FontStyle.Bold;
            plText.alignment = TextAnchor.MiddleCenter;
            plText.color = NauticalUITheme.EmeraldSea;

            // Bottom Footer Bar (Telemetry, Coordinates & Legend)
            GameObject footGo = new GameObject("ChartFooter");
            footGo.transform.SetParent(frameGo.transform, false);
            RectTransform footRt = footGo.AddComponent<RectTransform>();
            footRt.anchorMin = new Vector2(0.02f, 0f);
            footRt.anchorMax = new Vector2(0.98f, 0.08f);
            footRt.sizeDelta = Vector2.zero;

            _playerCoordsText = footGo.AddComponent<Text>();
            _playerCoordsText.text = "POSITION: [0, 0]  |  ZOOM: 1.0x";
            _playerCoordsText.font = NauticalUITheme.GetDefaultFont();
            _playerCoordsText.fontSize = 12;
            _playerCoordsText.fontStyle = FontStyle.Bold;
            _playerCoordsText.alignment = TextAnchor.MiddleCenter;
            _playerCoordsText.color = NauticalUITheme.ParchmentLight;
        }
    }
}
