using UnityEngine;

namespace Plunderlust.UI
{
    /// <summary>
    /// Safe Area Fitter for mobile devices (iOS notch, Dynamic Island, Android camera cutouts,
    /// rounded corners). Reads Screen.safeArea and clamps RectTransform anchors dynamically.
    /// Operates with zero per-frame heap allocations.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public class SafeAreaFitter : MonoBehaviour
    {
        [Header("Constraint Flags")]
        [SerializeField] private bool _conformHorizontal = true;
        [SerializeField] private bool _conformVertical = true;

        private RectTransform _rectTransform;
        private Rect _lastSafeArea = new Rect(0, 0, 0, 0);
        private Vector2Int _lastScreenSize = new Vector2Int(0, 0);
        private ScreenOrientation _lastOrientation = ScreenOrientation.AutoRotation;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void OnEnable()
        {
            ApplySafeArea();
        }

        private void Update()
        {
            if (HasScreenChanged())
            {
                ApplySafeArea();
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (HasScreenChanged())
            {
                ApplySafeArea();
            }
        }

        private bool HasScreenChanged()
        {
            bool safeAreaChanged = _lastSafeArea != Screen.safeArea;
            bool sizeChanged = _lastScreenSize.x != Screen.width || _lastScreenSize.y != Screen.height;
            bool orientChanged = _lastOrientation != Screen.orientation;
            return safeAreaChanged || sizeChanged || orientChanged;
        }

        public void ApplySafeArea()
        {
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null) return;

            Rect safeArea = Screen.safeArea;
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;

            if (screenWidth <= 0 || screenHeight <= 0) return;

            _lastSafeArea = safeArea;
            _lastScreenSize = new Vector2Int(screenWidth, screenHeight);
            _lastOrientation = Screen.orientation;

            Vector2 minAnchor = safeArea.position;
            Vector2 maxAnchor = safeArea.position + safeArea.size;

            minAnchor.x /= screenWidth;
            minAnchor.y /= screenHeight;
            maxAnchor.x /= screenWidth;
            maxAnchor.y /= screenHeight;

            // Guard against Android inverted landscape safe area bug
            if (screenWidth > screenHeight && safeArea.width < safeArea.height)
            {
                minAnchor.x = 0f;
                maxAnchor.x = 1f;
            }

            if (!_conformHorizontal)
            {
                minAnchor.x = 0f;
                maxAnchor.x = 1f;
            }
            if (!_conformVertical)
            {
                minAnchor.y = 0f;
                maxAnchor.y = 1f;
            }

            // Clamp between 0 and 1
            minAnchor.x = Mathf.Clamp01(minAnchor.x);
            minAnchor.y = Mathf.Clamp01(minAnchor.y);
            maxAnchor.x = Mathf.Clamp01(maxAnchor.x);
            maxAnchor.y = Mathf.Clamp01(maxAnchor.y);

            _rectTransform.anchorMin = minAnchor;
            _rectTransform.anchorMax = maxAnchor;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }
    }
}
