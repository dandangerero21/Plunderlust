using System.Collections.Generic;
using Plunderlust.Cargo;
using Plunderlust.Combat;
using Plunderlust.Core.Data;
using Plunderlust.Factions;
using Plunderlust.Loot;
using Plunderlust.Ocean;
using Plunderlust.Ship;
using Plunderlust.Subsystems;
using UnityEngine;

namespace Plunderlust.AI
{
    /// <summary>
    /// Phase 4 Naval Artificial Intelligence & Fleet Traffic Coordinator.
    /// Manages ambient faction patrols on trade lanes between Port Royal Haven and Smuggler Cove,
    /// and dynamically dispatches Imperial Navy Hunter Squadrons when player reputation drops to Hostile/Hunted.
    /// Employs diverse Kenney ship models (ship-small, ship-medium, ship-pirate-small) scaled to realistic naval proportions.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class NavalAIManager : MonoBehaviour
    {
        public static NavalAIManager Instance { get; private set; }

        [Header("Ambient Fleet Settings")]
        [SerializeField] private bool _spawnAmbientFleetOnStart = true;
        [SerializeField] private int _maxActiveAIShips = 5;

        // Deep-water navigable sea routes (kept >55m away from all island shores)
        private static readonly Vector3[] TradeLaneAlpha = new Vector3[]
        {
            new Vector3(95f, 0f, 30f),     // Far East of Port Royal in clear sea
            new Vector3(70f, 0f, 95f),     // North-East open shipping channel
            new Vector3(0f, 0f, 115f),     // North deep sea
            new Vector3(-85f, 0f, 75f),    // North-West open channel
            new Vector3(-85f, 0f, -25f),   // West deep waters
            new Vector3(15f, 0f, -50f)     // South shipping basin
        };

        private static readonly Vector3[] NavyPatrolRoute = new Vector3[]
        {
            new Vector3(90f, 0f, -15f),    // South-East open waters
            new Vector3(85f, 0f, 85f),     // East deep channel
            new Vector3(20f, 0f, 135f),    // North maritime sector
            new Vector3(-70f, 0f, 125f),   // North-West patrol
            new Vector3(0f, 0f, 45f)       // Central open channel (between Port Royal and Smuggler Cove)
        };

        private static readonly Vector3[] CorsairRaiderRoute = new Vector3[]
        {
            new Vector3(-95f, 0f, 40f),    // Far West of Smuggler's Cove in open sea
            new Vector3(-110f, 0f, 110f),  // Open waters towards Tortuga
            new Vector3(-55f, 0f, 135f),   // North deep passage
            new Vector3(-15f, 0f, 75f),    // Central North water
            new Vector3(-70f, 0f, -20f)    // South-West open sea
        };

        private readonly List<AIShipBrain> _activeAIShips = new List<AIShipBrain>();
        private float _spawnCooldownTimer = 0f;

        public IReadOnlyList<AIShipBrain> ActiveAIShips => _activeAIShips;

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
            if (FactionManager.Instance != null)
            {
                FactionManager.Instance.OnHunterSquadronDispatched += HandleHunterSquadronDispatched;
            }

            if (_spawnAmbientFleetOnStart)
            {
                SpawnAmbientFleet();
            }
        }

        private void OnDestroy()
        {
            if (FactionManager.Instance != null)
            {
                FactionManager.Instance.OnHunterSquadronDispatched -= HandleHunterSquadronDispatched;
            }
        }

        private void Update()
        {
            _spawnCooldownTimer += Time.deltaTime;

            // Prune dead or destroyed ships from active registry
            for (int i = _activeAIShips.Count - 1; i >= 0; i--)
            {
                if (_activeAIShips[i] == null)
                {
                    _activeAIShips.RemoveAt(i);
                }
            }

            // Continuous ambient fleet replenishment
            if (_activeAIShips.Count < _maxActiveAIShips && _spawnCooldownTimer >= 20.0f)
            {
                _spawnCooldownTimer = 0f;
                SpawnDynamicPatrolShip();
            }
        }

        public ShipTier GetPlayerShipTier()
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                ShipSubsystemController sub = playerGo.GetComponent<ShipSubsystemController>();
                if (sub != null && sub.HullData != null)
                {
                    return sub.HullData.Tier;
                }
            }
            return ShipTier.Tier1_LeakyRowboat;
        }

        public ShipTier PickNPCTierBasedOnPlayer()
        {
            ShipTier playerTier = GetPlayerShipTier();
            float roll = UnityEngine.Random.value;

            if (playerTier <= ShipTier.Tier1_LeakyRowboat)
            {
                // Player is in a small rowboat: small boats spawn most frequently,
                // while medium and large ships remain on the horizon
                if (roll < 0.65f) return ShipTier.Tier2_CoastalCutter;
                if (roll < 0.90f) return ShipTier.Tier3_PirateBrigantine;
                return ShipTier.Tier4_RoyalGalleon;
            }
            else if (playerTier == ShipTier.Tier2_CoastalCutter)
            {
                // Player has upgraded to Coastal Cutter: balanced encounter mix
                if (roll < 0.35f) return ShipTier.Tier2_CoastalCutter;
                if (roll < 0.85f) return ShipTier.Tier3_PirateBrigantine;
                return ShipTier.Tier4_RoyalGalleon;
            }
            else
            {
                // Player commands a heavy Brigantine or Galleon: larger armada vessels dominate
                if (roll < 0.20f) return ShipTier.Tier2_CoastalCutter;
                if (roll < 0.65f) return ShipTier.Tier3_PirateBrigantine;
                return ShipTier.Tier4_RoyalGalleon;
            }
        }

        // ponytail: authentic pirate and naval ship name pools
        private static readonly string[] s_NavyShipNames = new string[]
        {
            "HMS Dauntless", "HMS Vanguard", "HMS Sovereign", "HMS Defiance",
            "HMS Resolute", "HMS Thunderer", "HMS Royal Oak", "HMS Avenger",
            "HMS Dreadnought", "HMS Indomitable", "HMS Iron Duke", "HMS Victory"
        };

        private static readonly string[] s_MerchantShipNames = new string[]
        {
            "The Golden Hind", "Maiden's Fortune", "Silken Trader", "Cinnamon Breeze",
            "Fortune's Favor", "Island Trader", "Gilded Sovereign", "Silver Horizon",
            "Prosperity", "Grand Leviathan", "Spice Runner", "The Royal Bounty"
        };

        private static readonly string[] s_CorsairShipNames = new string[]
        {
            "Crimson Raider", "Black Dagger", "Reaper's Wake", "Sea Scourge",
            "Bloody Cutlass", "Bone Collector", "Devil's Maw", "Salty Siren",
            "Ghost Wind", "Widowmaker", "Black Tide", "Storm Crow"
        };

        private static readonly string[] s_SmugglerShipNames = new string[]
        {
            "Rum Runner", "Night Stalker", "Moonlit Drift", "Blind Pelican",
            "Black Gull", "Silent Cutlass", "Drunken Barnacle", "Cutpurse"
        };

        public static string GeneratePirateShipName(FactionType faction)
        {
            string[] pool = faction switch
            {
                FactionType.ImperialNavy => s_NavyShipNames,
                FactionType.MerchantGuild => s_MerchantShipNames,
                FactionType.CorsairBrethren => s_CorsairShipNames,
                _ => s_SmugglerShipNames
            };
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }

        public void SpawnAmbientFleet()
        {
            // 1. Merchant Trader (Open East Sea channel)
            ShipTier t1 = PickNPCTierBasedOnPlayer();
            SpawnAIShipTier(GeneratePirateShipName(FactionType.MerchantGuild), FactionType.MerchantGuild, new Vector3(95f, 0.5f, 30f), TradeLaneAlpha, t1);

            // 2. Imperial Navy Patrol (North-East deep water channel)
            ShipTier t2 = PickNPCTierBasedOnPlayer();
            SpawnAIShipTier(GeneratePirateShipName(FactionType.ImperialNavy), FactionType.ImperialNavy, new Vector3(85f, 0.5f, 85f), NavyPatrolRoute, t2);

            // 3. Corsair Raider (Far West deep waters)
            ShipTier t3 = PickNPCTierBasedOnPlayer();
            SpawnAIShipTier(GeneratePirateShipName(FactionType.CorsairBrethren), FactionType.CorsairBrethren, new Vector3(-95f, 0.5f, 40f), CorsairRaiderRoute, t3);
        }

        private void SpawnDynamicPatrolShip()
        {
            ShipTier tier = PickNPCTierBasedOnPlayer();
            FactionType faction = (FactionType)UnityEngine.Random.Range(0, 4);
            Vector3[] route = TradeLaneAlpha;
            Vector3 spawnPos = new Vector3(70f, 0.5f, 95f);

            switch (faction)
            {
                case FactionType.ImperialNavy:
                    route = NavyPatrolRoute;
                    spawnPos = new Vector3(90f, 0.5f, -15f);
                    break;
                case FactionType.CorsairBrethren:
                    route = CorsairRaiderRoute;
                    spawnPos = new Vector3(-95f, 0.5f, 40f);
                    break;
                default:
                    route = TradeLaneAlpha;
                    spawnPos = new Vector3(70f, 0.5f, 95f);
                    break;
            }

            string shipName = GeneratePirateShipName(faction);
            SpawnAIShipTier(shipName, faction, spawnPos, route, tier);
        }

        /// <summary>
        /// Ensures candidate spawn positions are in clear, deep navigable waters,
        /// avoiding all islands, rocks, docks, and coastal terrain.
        /// </summary>
        public static Vector3 FindSafeSpawnPoint(Vector3 targetPos, float minIslandClearance = 55.0f)
        {
            Vector3 safePos = targetPos;
            safePos.y = 0.5f;

            // 1. Check distance from all known island centers in scene
            var havens = Object.FindObjectsByType<Plunderlust.Port.PortIslandHaven>(FindObjectsInactive.Exclude);
            var coves = Object.FindObjectsByType<Plunderlust.Port.SmugglerCoveOutpost>(FindObjectsInactive.Exclude);
            var ports = Object.FindObjectsByType<Plunderlust.World.ProceduralPortLandmark>(FindObjectsInactive.Exclude);
            var atolls = Object.FindObjectsByType<Plunderlust.World.TreasureIslandLandmark>(FindObjectsInactive.Exclude);

            var islandCenters = new List<Vector3>();
            if (havens != null)
            {
                for (int i = 0; i < havens.Length; i++)
                {
                    if (havens[i] != null) islandCenters.Add(havens[i].transform.position);
                }
            }
            if (coves != null)
            {
                for (int i = 0; i < coves.Length; i++)
                {
                    if (coves[i] != null) islandCenters.Add(coves[i].transform.position);
                }
            }
            if (ports != null)
            {
                for (int i = 0; i < ports.Length; i++)
                {
                    if (ports[i] != null) islandCenters.Add(ports[i].transform.position);
                }
            }
            if (atolls != null)
            {
                for (int i = 0; i < atolls.Length; i++)
                {
                    if (atolls[i] != null) islandCenters.Add(atolls[i].transform.position);
                }
            }

            // Fallback protections for key historical ports
            if (islandCenters.Count == 0)
            {
                islandCenters.Add(new Vector3(45f, 0f, 25f));     // Port Royal
                islandCenters.Add(new Vector3(-45f, 0f, 30f));    // Smuggler Cove
                islandCenters.Add(new Vector3(-120f, 0f, 180f));  // Tortuga
            }

            for (int i = 0; i < islandCenters.Count; i++)
            {
                Vector3 center = islandCenters[i];
                Vector3 diff = safePos - center;
                diff.y = 0f;
                float dist = diff.magnitude;

                if (dist < minIslandClearance)
                {
                    Vector3 pushDir = dist > 1.0f ? diff.normalized : Vector3.forward;
                    safePos = center + (pushDir * (minIslandClearance + 15.0f));
                    safePos.y = 0.5f;
                }
            }

            // 2. Physics overlap check: ensure no solid terrain/rock colliders within 16m
            Collider[] hits = Physics.OverlapSphere(safePos, 16.0f);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i];
                if (col.isTrigger) continue;
                if (col.GetComponentInParent<ShipLocomotionController>() != null) continue; // ignore other ships

                Vector3 away = safePos - col.bounds.center;
                away.y = 0f;
                if (away.sqrMagnitude < 0.1f) away = Vector3.right;
                safePos += away.normalized * 25.0f;
                safePos.y = 0.5f;
            }

            return safePos;
        }

        public AIShipBrain SpawnAIShipTier(string shipName, FactionType faction, Vector3 worldPos, Vector3[] waypoints, ShipTier tier)
        {
            worldPos = FindSafeSpawnPoint(worldPos);
            string modelPath;
            string profilePath;
            float scale;
            float mass;
            Vector3 colliderSize;
            Vector3 colCenter;
            float probeFwd;
            float probeLat;

            switch (tier)
            {
                case ShipTier.Tier3_PirateBrigantine:
                    modelPath = faction == FactionType.CorsairBrethren
                        ? "Assets/kenney_pirate-kit/Models/FBX format/ship-pirate-medium.fbx"
                        : "Assets/kenney_pirate-kit/Models/FBX format/ship-medium.fbx";
                    profilePath = "Assets/Settings/ShipData/ShipHull_Tier3_PirateBrigantine.asset";
                    scale = 0.85f;
                    mass = 1400f;
                    colliderSize = new Vector3(3.0f, 2.6f, 8.8f);
                    colCenter = new Vector3(0f, 1.2f, 0f);
                    probeFwd = 3.8f;
                    probeLat = 1.4f;
                    break;

                case ShipTier.Tier4_RoyalGalleon:
                    modelPath = faction == FactionType.CorsairBrethren
                        ? "Assets/kenney_pirate-kit/Models/FBX format/ship-pirate-large.fbx"
                        : "Assets/kenney_pirate-kit/Models/FBX format/ship-large.fbx";
                    profilePath = "Assets/Settings/ShipData/ShipHull_Tier4_RoyalGalleon.asset";
                    scale = 1.15f;
                    mass = 2800f;
                    colliderSize = new Vector3(4.0f, 3.2f, 13.5f);
                    colCenter = new Vector3(0f, 1.6f, 0f);
                    probeFwd = 5.8f;
                    probeLat = 2.0f;
                    break;

                default: // Tier 2 Coastal Cutter (Sloop)
                    modelPath = faction == FactionType.CorsairBrethren
                        ? "Assets/kenney_pirate-kit/Models/FBX format/ship-pirate-small.fbx"
                        : "Assets/kenney_pirate-kit/Models/FBX format/ship-small.fbx";
                    profilePath = "Assets/Settings/ShipData/ShipHull_Tier2_CoastalCutter.asset";
                    scale = 0.55f; // Sleek single-mast cutter/sloop: exactly 2x rowboat
                    mass = 650f;
                    colliderSize = new Vector3(2.0f, 2.0f, 4.8f);
                    colCenter = new Vector3(0f, 0.9f, 0f);
                    probeFwd = 2.1f;
                    probeLat = 0.95f;
                    break;
            }

            return SpawnAIShipInternal(shipName, faction, worldPos, waypoints, modelPath, profilePath, scale, mass, colliderSize, colCenter, probeFwd, probeLat);
        }

        public AIShipBrain SpawnAIShip(
            string shipName,
            FactionType faction,
            Vector3 worldPos,
            Vector3[] waypoints,
            string modelPath = null,
            float scale = 0.55f,
            float mass = 650f,
            Vector3? colliderSize = null)
        {
            Vector3 cSize = colliderSize ?? new Vector3(2.0f, 2.0f, 4.8f);
            Vector3 center = new Vector3(0f, cSize.y * 0.45f, 0f);
            float fwd = cSize.z * 0.44f;
            float lat = cSize.x * 0.44f;
            string profile = "Assets/Settings/ShipData/ShipHull_Tier2_CoastalCutter.asset";
            return SpawnAIShipInternal(shipName, faction, worldPos, waypoints, modelPath, profile, scale, mass, cSize, center, fwd, lat);
        }

        private AIShipBrain SpawnAIShipInternal(
            string shipName,
            FactionType faction,
            Vector3 worldPos,
            Vector3[] waypoints,
            string modelPath,
            string profilePath,
            float scale,
            float mass,
            Vector3 colliderSize,
            Vector3 colliderCenter,
            float probeFwd,
            float probeLat)
        {
            if (_activeAIShips.Count >= _maxActiveAIShips) return null;

            GameObject shipGo = new GameObject(shipName);
            shipGo.transform.position = worldPos;
            shipGo.transform.rotation = Quaternion.identity;

            // Rigidbody
            Rigidbody rb = shipGo.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = 0.85f;
            rb.angularDamping = 2.0f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            if (string.IsNullOrEmpty(modelPath))
            {
                modelPath = faction == FactionType.CorsairBrethren
                    ? "Assets/kenney_pirate-kit/Models/FBX format/ship-pirate-small.fbx"
                    : "Assets/kenney_pirate-kit/Models/FBX format/ship-small.fbx";
            }

            // Model Prefab with proportional scaling
            string modelName = System.IO.Path.GetFileNameWithoutExtension(modelPath);
            GameObject modelPrefab = Resources.Load<GameObject>("Models/" + modelName);
#if UNITY_EDITOR
            if (modelPrefab == null)
            {
                modelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            }
#endif
            if (modelPrefab != null)
            {
                GameObject modelInstance = Instantiate(modelPrefab, shipGo.transform);
                modelInstance.name = "ShipModel";
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one * scale;

                Material paletteMat = Resources.Load<Material>("M_KenneyPiratePalette");
#if UNITY_EDITOR
                if (paletteMat == null)
                {
                    paletteMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_KenneyPiratePalette.mat");
                }
#endif
                if (paletteMat != null)
                {
                    Renderer[] rends = modelInstance.GetComponentsInChildren<Renderer>();
                    foreach (var r in rends) r.sharedMaterial = paletteMat;
                }
            }

            // Box Collider tailored to hull size
            BoxCollider col = shipGo.AddComponent<BoxCollider>();
            col.center = colliderCenter;
            col.size = colliderSize;

            // BuoyancyBody with 4 Probes proportional to scale
            BuoyancyBody buoyancy = shipGo.AddComponent<BuoyancyBody>();
            GameObject probesRoot = new GameObject("BuoyancyProbes");
            probesRoot.transform.SetParent(shipGo.transform, false);

            CreateProbe(probesRoot.transform, "Probe_Bow", new Vector3(0f, 0f, probeFwd));
            CreateProbe(probesRoot.transform, "Probe_Stern", new Vector3(0f, 0f, -probeFwd));
            CreateProbe(probesRoot.transform, "Probe_Port", new Vector3(-probeLat, 0f, 0f));
            CreateProbe(probesRoot.transform, "Probe_Starboard", new Vector3(probeLat, 0f, 0f));

            string profFileName = !string.IsNullOrEmpty(profilePath) ? System.IO.Path.GetFileNameWithoutExtension(profilePath) : "ShipHull_Tier2_CoastalCutter";
            ShipHullDataSO hullProfile = Resources.Load<ShipHullDataSO>("ShipData/" + profFileName);
#if UNITY_EDITOR
            if (hullProfile == null && !string.IsNullOrEmpty(profilePath))
            {
                hullProfile = UnityEditor.AssetDatabase.LoadAssetAtPath<ShipHullDataSO>(profilePath);
            }
            if (hullProfile == null)
            {
                hullProfile = UnityEditor.AssetDatabase.LoadAssetAtPath<ShipHullDataSO>("Assets/Settings/ShipData/ShipHull_Tier2_CoastalCutter.asset");
            }
#endif

            // Locomotion, Subsystems, Combat, Cargo
            ShipLocomotionController locomotion = shipGo.AddComponent<ShipLocomotionController>();
            if (hullProfile != null) locomotion.SetHullData(hullProfile);

            ShipSubsystemController subsystems = shipGo.AddComponent<ShipSubsystemController>();
            if (hullProfile != null) subsystems.SetHullData(hullProfile);

            NavalCombatController combat = shipGo.AddComponent<NavalCombatController>();
            if (hullProfile != null) combat.SetHullData(hullProfile);

            ShipCargoHold cargoHold = shipGo.AddComponent<ShipCargoHold>();
            if (hullProfile != null) cargoHold.SetHullData(hullProfile);

            shipGo.AddComponent<ShipWorldHealthBar>();
            shipGo.AddComponent<LootableWreck>();

            // Faction Pennant & AI Brain
            FactionPennantVisual pennant = shipGo.AddComponent<FactionPennantVisual>();
            pennant.SetFaction(faction);

            AIShipBrain brain = shipGo.AddComponent<AIShipBrain>();
            brain.InitializeAI(faction, waypoints);

            _activeAIShips.Add(brain);
            Debug.Log($"<color=cyan>[Fleet Spawner]</color> Spawned {faction} {hullProfile?.ShipClassName ?? "Vessel"} {shipName} at {worldPos}");
            return brain;
        }

        private void HandleHunterSquadronDispatched(FactionType hunterFaction)
        {
            if (_spawnCooldownTimer < 15.0f) return;
            _spawnCooldownTimer = 0f;

            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo == null) return;

            // Spawn 2-Masted Navy Brigantine Hunter on the horizon
            Vector3 spawnDir = (playerGo.transform.forward + (playerGo.transform.right * 0.4f)).normalized;
            Vector3 spawnPos = playerGo.transform.position + (spawnDir * 80.0f);
            spawnPos.y = 0.5f;

            AIShipBrain hunter = SpawnAIShip(
                "HMS Vanguard",
                hunterFaction,
                spawnPos,
                null,
                "Assets/kenney_pirate-kit/Models/FBX format/ship-medium.fbx",
                1.0f,
                1600f,
                new Vector3(3.5f, 3.2f, 9.8f)
            );

            if (hunter != null)
            {
                hunter.SetCombatTarget(playerGo.transform);
                Debug.LogWarning("<color=red><b>[HUNTER INTERCEPT!]</b></color> Heavy Imperial Navy Brigantine has mobilized to claim your bounty!");
            }
        }

        private static Transform CreateProbe(Transform parent, string name, Vector3 localPos)
        {
            GameObject probeGo = new GameObject(name);
            probeGo.transform.SetParent(parent, false);
            probeGo.transform.localPosition = localPos;
            return probeGo.transform;
        }
    }
}
