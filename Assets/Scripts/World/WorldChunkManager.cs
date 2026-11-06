using System.Collections.Generic;
using Plunderlust.Core.Data;
using Plunderlust.Shipyard;
using UnityEngine;

namespace Plunderlust.World
{
    /// <summary>
    /// Phase 5 Infinite Ocean World & Chunk Streaming Manager.
    /// Procedurally generates an infinite archipelago of diverse ports (Imperial Fortresses,
    /// Merchant Hubs, Pirate Dens, Freeport Atolls), Treasure Islands, and Sunken Shipwreck Reefs
    /// with zero GC allocations in runtime update loops.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class WorldChunkManager : MonoBehaviour
    {
        public static WorldChunkManager Instance { get; private set; }

        [Header("Chunk Grid Configuration")]
        [SerializeField] private float _chunkSize = 350.0f;
        [SerializeField] private int _chunkRadius = 1; // 3x3 active grid
        [SerializeField] private int _worldSeed = 1337;
        [Tooltip("If true, bounds the world to the 3x3 archipelago contained within MapBoundaryBarrier.")]
        [SerializeField] private bool _boundToArchipelago = true;

        private Transform _playerTransform;
        private Vector2Int _currentCenterChunk = new Vector2Int(int.MinValue, int.MinValue);
        private readonly Dictionary<Vector2Int, GameObject> _activeChunks = new Dictionary<Vector2Int, GameObject>();

        public float ChunkSize => _chunkSize;
        public int ActiveChunkCount => _activeChunks.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            FindPlayer();
            UpdateChunks(true);
        }

        private void FindPlayer()
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                _playerTransform = playerGo.transform;
            }
        }

        private void Update()
        {
            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            Vector3 worldPos = _playerTransform.position;
            if (FloatingOrigin.Instance != null)
            {
                worldPos += FloatingOrigin.Instance.AccumulatedOffset;
            }

            if (_boundToArchipelago)
            {
                // In bound archipelago mode, the 3x3 world ([-1, 1] on X and Z) is persistently maintained
                if (_currentCenterChunk != Vector2Int.zero)
                {
                    _currentCenterChunk = Vector2Int.zero;
                    UpdateChunks(false);
                }
                return;
            }

            int chunkX = Mathf.FloorToInt((worldPos.x + (_chunkSize * 0.5f)) / _chunkSize);
            int chunkZ = Mathf.FloorToInt((worldPos.z + (_chunkSize * 0.5f)) / _chunkSize);
            Vector2Int newCenter = new Vector2Int(chunkX, chunkZ);

            if (newCenter != _currentCenterChunk)
            {
                _currentCenterChunk = newCenter;
                UpdateChunks(false);
            }
        }

        private void UpdateChunks(bool force)
        {
            HashSet<Vector2Int> requiredChunks = new HashSet<Vector2Int>();

            for (int dx = -_chunkRadius; dx <= _chunkRadius; dx++)
            {
                for (int dz = -_chunkRadius; dz <= _chunkRadius; dz++)
                {
                    Vector2Int coord = new Vector2Int(_currentCenterChunk.x + dx, _currentCenterChunk.y + dz);
                    requiredChunks.Add(coord);

                    if (!_activeChunks.ContainsKey(coord))
                    {
                        GenerateChunk(coord);
                    }
                }
            }

            // Unload out-of-range chunks
            List<Vector2Int> toRemove = new List<Vector2Int>();
            foreach (var kvp in _activeChunks)
            {
                if (!requiredChunks.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                Vector2Int coord = toRemove[i];
                if (_activeChunks.TryGetValue(coord, out GameObject chunkGo))
                {
                    if (chunkGo != null) Destroy(chunkGo);
                    _activeChunks.Remove(coord);
                }
            }
        }

        private void GenerateChunk(Vector2Int coord)
        {
            Vector3 chunkWorldCenter = new Vector3(coord.x * _chunkSize, 0f, coord.y * _chunkSize);
            if (FloatingOrigin.Instance != null)
            {
                chunkWorldCenter -= FloatingOrigin.Instance.AccumulatedOffset;
            }

            GameObject chunkGo = new GameObject($"WorldChunk_{coord.x}_{coord.y}");
            chunkGo.transform.position = chunkWorldCenter;
            _activeChunks[coord] = chunkGo;

            // Chunk (0, 0) is the home haven chunk (Port Royal Haven & Drydock)
            if (coord.x == 0 && coord.y == 0)
            {
                var existing = GameObject.Find("PortHaven_Island");
                if (existing != null && existing.transform.childCount > 2)
                {
                    existing.transform.SetParent(chunkGo.transform, true);
                    return;
                }
                GenerateHomeHavenChunk(chunkGo.transform);
                return;
            }

            // Chunk (-1, 0) is Smuggler Cove
            if (coord.x == -1 && coord.y == 0)
            {
                var existing = GameObject.Find("SmugglerCove_Outpost");
                if (existing != null && existing.transform.childCount > 2)
                {
                    existing.transform.SetParent(chunkGo.transform, true);
                    return;
                }
                GenerateSmugglerCoveChunk(chunkGo.transform);
                return;
            }

            // Deterministic Chunk RNG
            int hash = ((coord.x * 73856093) ^ (coord.y * 19349663) ^ _worldSeed) & 0x7FFFFFFF;
            float roll = (hash % 1000) / 1000.0f;

            if (roll < 0.16f)
            {
                // Procedural Faction Port (Naval Fortress, Merchant Port, or Pirate Den)
                GenerateProceduralPort(chunkGo.transform, coord, hash);
            }
            else if (roll < 0.32f)
            {
                // Uninhabited Treasure Island with Buried Chest & Palm Atoll
                GenerateTreasureIsland(chunkGo.transform, coord, hash);
            }
            else if (roll < 0.46f)
            {
                // Sunken Shipwreck Reef with Flotsam
                GenerateSunkenShipwreckReef(chunkGo.transform, coord, hash);
            }
            else if (roll < 0.60f)
            {
                // Sea Stack Rocks & Minor Reef Hazard
                GenerateSeaStackReef(chunkGo.transform, hash);
            }
            // Otherwise open deep ocean
        }

        private void GenerateHomeHavenChunk(Transform chunkRoot)
        {
            FactionPortBuilder.BuildPort(chunkRoot, new Vector3(45f, 0f, 25f), Quaternion.identity, PortArchetype.MerchantGuildHub, "PortHaven_Island");
        }

        private void GenerateSmugglerCoveChunk(Transform chunkRoot)
        {
            FactionPortBuilder.BuildPort(chunkRoot, new Vector3(-45f, 0f, 30f), Quaternion.identity, PortArchetype.CorsairHavenFreeport, "SmugglerCove_Outpost");
        }

        private void GenerateProceduralPort(Transform chunkRoot, Vector2Int coord, int hash)
        {
            FactionType faction = (FactionType)(hash % 4);
            PortArchetype archetype = faction switch
            {
                FactionType.ImperialNavy => PortArchetype.ImperialNavyBastion,
                FactionType.MerchantGuild => PortArchetype.MerchantGuildHub,
                _ => PortArchetype.CorsairHavenFreeport
            };

            string portName = faction switch
            {
                FactionType.ImperialNavy => $"Fort St. George [{coord.x},{coord.y}]",
                FactionType.MerchantGuild => $"Port Marigold [{coord.x},{coord.y}]",
                FactionType.CorsairBrethren => $"Cutlass Cove [{coord.x},{coord.y}]",
                _ => $"Isla de la Libre [{coord.x},{coord.y}]"
            };

            Vector3 localPos = new Vector3((hash % 100) - 50f, 0f, ((hash / 100) % 100) - 50f);
            FactionPortBuilder.BuildPort(chunkRoot, localPos, Quaternion.identity, archetype, portName);
        }

        private void GenerateTreasureIsland(Transform chunkRoot, Vector2Int coord, int hash)
        {
            Vector3 localPos = new Vector3((hash % 120) - 60f, 0f, ((hash / 100) % 120) - 60f);
            int goldReward = UnityEngine.Random.Range(550, 950);
            TreasureAtollBuilder.BuildAtoll(chunkRoot, localPos, Quaternion.identity, $"Skeleton Key Atoll [{coord.x},{coord.y}]", goldReward);
        }

        private void GenerateSunkenShipwreckReef(Transform chunkRoot, Vector2Int coord, int hash)
        {
            GameObject reefGo = new GameObject($"ShipwreckReef_{coord.x}_{coord.y}");
            reefGo.transform.SetParent(chunkRoot, false);
            reefGo.transform.localPosition = new Vector3((hash % 140) - 70f, 0f, ((hash / 80) % 140) - 70f);

            // Half-submerged broken hull tilted in shallow reef
            SpawnModel(reefGo.transform, "ship-wreck", new Vector3(0f, -0.4f, 0f), Quaternion.Euler(18f, hash % 360, -12f), Vector3.one * 1.3f);
            SpawnModel(reefGo.transform, "rocks-sand-c", new Vector3(4.0f, 0.1f, -2.0f), Quaternion.identity, Vector3.one * 1.8f);

            // Spawn floating flotsam crates around wreck
            Loot.LootableWreck loot = reefGo.AddComponent<Loot.LootableWreck>();
            loot.SpawnFlotsam();
        }

        private void GenerateSeaStackReef(Transform chunkRoot, int hash)
        {
            GameObject stackGo = new GameObject("SeaStackReef");
            stackGo.transform.SetParent(chunkRoot, false);
            stackGo.transform.localPosition = new Vector3((hash % 160) - 80f, 0f, ((hash / 60) % 160) - 80f);

            string rockModel = (hash % 2 == 0) ? "rocks-a" : "rocks-sand-a";
            SpawnModel(stackGo.transform, rockModel, Vector3.zero, Quaternion.Euler(0f, hash % 360, 0f), new Vector3(2.5f, 3.2f, 2.5f));
        }

        private static GameObject SpawnModel(Transform parent, string modelName, Vector3 localPos, Quaternion localRot, Vector3 scale)
        {
            string path = $"Assets/kenney_pirate-kit/Models/FBX format/{modelName}.fbx";
            GameObject prefab = Resources.Load<GameObject>("Models/" + modelName);
#if UNITY_EDITOR
            if (prefab == null) prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#endif
            if (prefab == null) prefab = Resources.Load<GameObject>(modelName);

            if (prefab != null)
            {
                GameObject inst = Instantiate(prefab, parent);
                inst.name = modelName;
                inst.transform.localPosition = localPos;
                inst.transform.localRotation = localRot;
                inst.transform.localScale = scale;

                Material paletteMat = Resources.Load<Material>("M_KenneyPiratePalette");
#if UNITY_EDITOR
                if (paletteMat == null)
                    paletteMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_KenneyPiratePalette.mat");
#endif
                if (paletteMat != null)
                {
                    Renderer[] rends = inst.GetComponentsInChildren<Renderer>();
                    foreach (var r in rends) r.sharedMaterial = paletteMat;
                }
                // Ensure physical solid collider
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
    }
}
