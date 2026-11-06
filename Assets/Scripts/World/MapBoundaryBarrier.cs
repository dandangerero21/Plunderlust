using System.Collections.Generic;
using Plunderlust.Ship;
using Plunderlust.UI;
using UnityEngine;

namespace Plunderlust.World
{
    /// <summary>
    /// Phase 5 Map Boundary Barrier & Restorative Ocean Current System.
    /// Encloses the 3x3 archipelago in a visible perimeter of barrier reefs, sea stacks,
    /// and warning spires, backed by solid physical edge colliders and restorative ocean currents
    /// that gently turn stray vessels back into the playable naval theater.
    /// </summary>
    public class MapBoundaryBarrier : MonoBehaviour
    {
        public static MapBoundaryBarrier Instance { get; private set; }

        [Header("Archipelago World Bounds")]
        [Tooltip("Half-extent of the playable map in meters (covers -halfSize to +halfSize).")]
        [SerializeField] private float _halfSize = 550.0f;
        [Tooltip("Distance from boundary edge where warning alerts and inward currents activate.")]
        [SerializeField] private float _currentZoneWidth = 45.0f;
        [Tooltip("Acceleration power of inward ocean currents pushing stray ships back into the map.")]
        [SerializeField] private float _restorativeCurrentPower = 18.0f;
        [Tooltip("Spacing in meters between decorative barrier reef rocks along the perimeter.")]
        [SerializeField] private float _barrierReefSpacing = 48.0f;

        private readonly List<Rigidbody> _trackedShips = new List<Rigidbody>();
        private Transform _playerTransform;
        private float _lastPlayerAlertTime = -10f;

        public float HalfSize => _halfSize;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BuildPerimeterColliders();
            SpawnPerimeterReefVisuals();
        }

        private void Start()
        {
            FindPlayer();
            RefreshTrackedShips();
        }

        private void FindPlayer()
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                _playerTransform = playerGo.transform;
            }
        }

        public void RefreshTrackedShips()
        {
            _trackedShips.Clear();
            ShipLocomotionController[] locos = FindObjectsByType<ShipLocomotionController>(FindObjectsInactive.Exclude);
            foreach (var loco in locos)
            {
                Rigidbody rb = loco.GetComponent<Rigidbody>();
                if (rb != null && !_trackedShips.Contains(rb))
                {
                    _trackedShips.Add(rb);
                }
            }
        }

        private void BuildPerimeterColliders()
        {
            GameObject boundaryGo = new GameObject("BoundaryColliders");
            boundaryGo.transform.SetParent(transform, false);

            float wallThickness = 20.0f;
            float wallHeight = 40.0f;
            float totalSpan = (_halfSize * 2.0f) + (wallThickness * 2.0f);
            float wallY = wallHeight * 0.4f;

            // North Wall (+Z)
            CreateWallBox(boundaryGo.transform, "Wall_North", new Vector3(0f, wallY, _halfSize + (wallThickness * 0.5f)), new Vector3(totalSpan, wallHeight, wallThickness));
            // South Wall (-Z)
            CreateWallBox(boundaryGo.transform, "Wall_South", new Vector3(0f, wallY, -_halfSize - (wallThickness * 0.5f)), new Vector3(totalSpan, wallHeight, wallThickness));
            // East Wall (+X)
            CreateWallBox(boundaryGo.transform, "Wall_East", new Vector3(_halfSize + (wallThickness * 0.5f), wallY, 0f), new Vector3(wallThickness, wallHeight, totalSpan));
            // West Wall (-X)
            CreateWallBox(boundaryGo.transform, "Wall_West", new Vector3(-_halfSize - (wallThickness * 0.5f), wallY, 0f), new Vector3(wallThickness, wallHeight, totalSpan));
        }

        private static void CreateWallBox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            GameObject wallGo = new GameObject(name);
            wallGo.transform.SetParent(parent, false);
            wallGo.transform.position = center;
            BoxCollider col = wallGo.AddComponent<BoxCollider>();
            col.size = size;
        }

        private void SpawnPerimeterReefVisuals()
        {
            GameObject visualRoot = new GameObject("PerimeterBarrierVisuals");
            visualRoot.transform.SetParent(transform, false);

            // 1. Four Corner Sea Spires / Watchtowers
            SpawnCornerBeacon(visualRoot.transform, new Vector3(_halfSize - 10f, 0f, _halfSize - 10f), 45f);
            SpawnCornerBeacon(visualRoot.transform, new Vector3(-_halfSize + 10f, 0f, _halfSize - 10f), 135f);
            SpawnCornerBeacon(visualRoot.transform, new Vector3(-_halfSize + 10f, 0f, -_halfSize + 10f), 225f);
            SpawnCornerBeacon(visualRoot.transform, new Vector3(_halfSize - 10f, 0f, -_halfSize + 10f), 315f);

            // 2. Perimeter Barrier Reef Rock Clusters
            int steps = Mathf.Max(6, Mathf.RoundToInt((_halfSize * 2.0f) / _barrierReefSpacing));
            float stepSize = (_halfSize * 2.0f) / steps;

            string[] rockModels = { "rocks-a", "rocks-b", "rocks-sand-a", "rocks-sand-b" };

            for (int i = 1; i < steps; i++)
            {
                float offset = -_halfSize + (i * stepSize);
                string modelA = rockModels[i % rockModels.Length];
                string modelB = rockModels[(i + 1) % rockModels.Length];

                // North edge (+Z)
                SpawnBarrierRock(visualRoot.transform, modelA, new Vector3(offset, 0f, _halfSize), i * 37f);
                // South edge (-Z)
                SpawnBarrierRock(visualRoot.transform, modelB, new Vector3(offset, 0f, -_halfSize), i * 53f);
                // East edge (+X)
                SpawnBarrierRock(visualRoot.transform, modelB, new Vector3(_halfSize, 0f, offset), i * 71f);
                // West edge (-X)
                SpawnBarrierRock(visualRoot.transform, modelA, new Vector3(-_halfSize, 0f, offset), i * 89f);
            }
        }

        private void SpawnCornerBeacon(Transform parent, Vector3 pos, float yaw)
        {
            GameObject spireGo = new GameObject("BarrierCornerBeacon");
            spireGo.transform.SetParent(parent, false);
            spireGo.transform.position = pos;
            spireGo.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            SpawnModel(spireGo.transform, "rocks-sand-a", Vector3.zero, Quaternion.identity, new Vector3(3.2f, 3.8f, 3.2f));
            SpawnModel(spireGo.transform, "tower-watch", new Vector3(0f, 1.8f, 0f), Quaternion.identity, Vector3.one * 1.3f);
        }

        private void SpawnBarrierRock(Transform parent, string modelName, Vector3 pos, float yaw)
        {
            GameObject rockGo = new GameObject($"Barrier_{modelName}");
            rockGo.transform.SetParent(parent, false);
            rockGo.transform.position = pos;
            rockGo.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            SpawnModel(rockGo.transform, modelName, Vector3.zero, Quaternion.identity, new Vector3(2.6f, 2.8f, 2.6f));
        }

        private static GameObject SpawnModel(Transform parent, string modelName, Vector3 localPos, Quaternion localRot, Vector3 localScale)
        {
            GameObject prefab = Resources.Load<GameObject>("Models/" + modelName) ?? Resources.Load<GameObject>(modelName);
#if UNITY_EDITOR
            if (prefab == null)
            {
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/kenney_pirate-kit/Models/FBX format/{modelName}.fbx");
            }
#endif
            if (prefab != null)
            {
                GameObject inst = Instantiate(prefab, parent);
                inst.transform.localPosition = localPos;
                inst.transform.localRotation = localRot;
                inst.transform.localScale = localScale;

                Material paletteMat = Resources.Load<Material>("M_KenneyPiratePalette");
#if UNITY_EDITOR
                if (paletteMat == null)
                {
                    paletteMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_KenneyPiratePalette.mat");
                }
#endif
                if (paletteMat != null)
                {
                    Renderer[] rends = inst.GetComponentsInChildren<Renderer>();
                    foreach (var r in rends) r.sharedMaterial = paletteMat;
                }
                MeshFilter[] mfs = inst.GetComponentsInChildren<MeshFilter>();
                foreach (var mf in mfs)
                {
                    if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                    {
                        mf.gameObject.AddComponent<MeshCollider>();
                    }
                }
                return inst;
            }
            return null;
        }

        private void FixedUpdate()
        {
            float triggerBoundary = _halfSize - _currentZoneWidth;

            for (int i = _trackedShips.Count - 1; i >= 0; i--)
            {
                Rigidbody rb = _trackedShips[i];
                if (rb == null)
                {
                    _trackedShips.RemoveAt(i);
                    continue;
                }

                Vector3 pos = rb.position;
                float absX = Mathf.Abs(pos.x);
                float absZ = Mathf.Abs(pos.z);

                if (absX > triggerBoundary || absZ > triggerBoundary)
                {
                    // Calculate restorative inward vector towards map center (0, 0)
                    Vector3 toCenter = new Vector3(-pos.x, 0f, -pos.z);
                    if (toCenter.sqrMagnitude > 0.001f)
                    {
                        Vector3 inwardDir = toCenter.normalized;

                        // Progressive current force scaling as ship approaches outer edge
                        float excessX = Mathf.Max(0f, absX - triggerBoundary);
                        float excessZ = Mathf.Max(0f, absZ - triggerBoundary);
                        float excessRatio = Mathf.Clamp01(Mathf.Max(excessX, excessZ) / _currentZoneWidth);

                        float appliedForce = _restorativeCurrentPower * (0.6f + (excessRatio * 0.8f));
                        rb.AddForce(inwardDir * appliedForce, ForceMode.Acceleration);

                        // Gentle steering torque aligning ship towards center
                        Vector3 forward = Vector3.ProjectOnPlane(rb.transform.forward, Vector3.up).normalized;
                        Vector3 turnCross = Vector3.Cross(forward, inwardDir);
                        rb.AddTorque(Vector3.up * (turnCross.y * 12.0f), ForceMode.Acceleration);

                        // Alert player if within warning zone
                        if (_playerTransform != null && rb.transform == _playerTransform)
                        {
                            if (Time.time - _lastPlayerAlertTime > 4.5f)
                            {
                                _lastPlayerAlertTime = Time.time;
                                if (PirateCombatHUD.Instance != null)
                                {
                                    PirateCombatHUD.Instance.ShowBanner("⚠️ PERILOUS REEFS: Outer boundary reached! Ocean currents turning ship about.", new Color(1f, 0.45f, 0.25f), 3.0f);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
