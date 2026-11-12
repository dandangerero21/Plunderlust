using UnityEngine;

namespace Plunderlust.World
{
    /// <summary>
    /// 3D Luminous Horizon Waypoint Beacon.
    /// Casts an atmospheric light column rising into the sky at the target location
    /// (e.g. Bounty Target, Port Haven, Smuggler Cove) visible across the sea.
    /// Operates with zero heap allocations at runtime.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class WaypointBeacon : MonoBehaviour
    {
        private static WaypointBeacon _instance;
        public static WaypointBeacon Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<WaypointBeacon>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("WaypointBeacon");
                        _instance = go.AddComponent<WaypointBeacon>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        public bool HasActiveWaypoint => _hasActiveWaypoint;
        public Vector3 WaypointPosition => _waypointPosition;
        public string WaypointLabel => _waypointLabel;

        private bool _hasActiveWaypoint = false;
        private Vector3 _waypointPosition = Vector3.zero;
        private string _waypointLabel = "";

        private GameObject _beaconVisualGo;
        private Transform _playerTransform;
        private Material _beaconMaterial;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            BuildBeaconVisual();
        }

        private void Start()
        {
            FindPlayer();
        }

        private void FindPlayer()
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null) _playerTransform = playerGo.transform;
        }

        private void BuildBeaconVisual()
        {
            _beaconVisualGo = new GameObject("3D_Horizon_Waypoint_Beacon");
            _beaconVisualGo.transform.SetParent(transform, true);

            // 1. Central Luminous Cylinder Light Column
            GameObject columnGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            columnGo.name = "Light_Column";
            columnGo.transform.SetParent(_beaconVisualGo.transform, false);
            columnGo.transform.localScale = new Vector3(4.0f, 90.0f, 4.0f);
            columnGo.transform.localPosition = new Vector3(0f, 90.0f, 0f);

            // Remove physical collider
            Collider col = columnGo.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Translucent glowing golden-cyan nautical beam material
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                            Shader.Find("Universal Render Pipeline/Simple Lit") ??
                            Shader.Find("Universal Render Pipeline/Unlit") ??
                            Shader.Find("Standard") ??
                            Shader.Find("Mobile/Diffuse");

            if (shader != null)
            {
                _beaconMaterial = new Material(shader);
                _beaconMaterial.name = "M_WaypointBeaconGlow";

                Color glowColor = new Color(0.95f, 0.78f, 0.25f, 0.45f);
                if (_beaconMaterial.HasProperty("_BaseColor")) _beaconMaterial.SetColor("_BaseColor", glowColor);
                _beaconMaterial.color = glowColor;

                // Transparent blending mode in URP
                if (_beaconMaterial.HasProperty("_Surface")) _beaconMaterial.SetFloat("_Surface", 1.0f); // Transparent
                if (_beaconMaterial.HasProperty("_Blend")) _beaconMaterial.SetFloat("_Blend", 0.0f); // Alpha blend
                if (_beaconMaterial.HasProperty("_Cull")) _beaconMaterial.SetFloat("_Cull", 0.0f); // Double sided

                MeshRenderer mr = columnGo.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = _beaconMaterial;
            }

            // 2. High-Altitude Floating Compass Pip
            GameObject ringGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ringGo.name = "Altitude_Beacon_Pip";
            ringGo.transform.SetParent(_beaconVisualGo.transform, false);
            ringGo.transform.localScale = new Vector3(14.0f, 14.0f, 14.0f);
            ringGo.transform.localPosition = new Vector3(0f, 180.0f, 0f);

            Collider ringCol = ringGo.GetComponent<Collider>();
            if (ringCol != null) Destroy(ringCol);

            MeshRenderer ringMr = ringGo.GetComponent<MeshRenderer>();
            if (ringMr != null && _beaconMaterial != null) ringMr.sharedMaterial = _beaconMaterial;

            _beaconVisualGo.SetActive(false);
        }

        public void SetWaypoint(Vector3 worldPos, string label)
        {
            _waypointPosition = worldPos;
            _waypointPosition.y = 0f;
            _waypointLabel = label;
            _hasActiveWaypoint = true;

            if (_beaconVisualGo != null)
            {
                _beaconVisualGo.transform.position = _waypointPosition;
                _beaconVisualGo.SetActive(true);
            }

            Debug.Log($"<color=yellow>[Waypoint Set]</color> {label} at {_waypointPosition}");
        }

        public void ClearWaypoint()
        {
            _hasActiveWaypoint = false;
            _waypointLabel = "";

            if (_beaconVisualGo != null)
            {
                _beaconVisualGo.SetActive(false);
            }
        }

        public float GetDistanceToPlayer()
        {
            if (_playerTransform == null) FindPlayer();
            if (_playerTransform == null || !_hasActiveWaypoint) return 0f;

            Vector3 diff = _waypointPosition - _playerTransform.position;
            diff.y = 0f;
            return diff.magnitude;
        }

        private void Update()
        {
            if (!_hasActiveWaypoint || _beaconVisualGo == null) return;

            // Gentle pulsating alpha and slow horizon rotation
            float pulse = 0.35f + Mathf.Sin(Time.time * 2.5f) * 0.15f;
            if (_beaconMaterial != null)
            {
                Color c = new Color(0.95f, 0.78f, 0.25f, pulse);
                if (_beaconMaterial.HasProperty("_BaseColor")) _beaconMaterial.SetColor("_BaseColor", c);
                _beaconMaterial.color = c;
            }

            _beaconVisualGo.transform.Rotate(0f, 25.0f * Time.deltaTime, 0f);

            // Auto-clear if player arrives within 22m of a non-combat port waypoint
            if (_playerTransform != null)
            {
                float dist = Vector3.Distance(_playerTransform.position, _waypointPosition);
                if (dist <= 22.0f && !_waypointLabel.StartsWith("BOUNTY", System.StringComparison.OrdinalIgnoreCase))
                {
                    ClearWaypoint();
                }
            }
        }
    }
}
