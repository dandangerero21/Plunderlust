using Plunderlust.AI;
using Plunderlust.Cargo;
using Plunderlust.Core.Data;
using Plunderlust.Crew;
using Plunderlust.Factions;
using Plunderlust.Ocean;
using Plunderlust.Port;
using Plunderlust.Ship;
using Plunderlust.Subsystems;
using Plunderlust.UI;
using Plunderlust.Wind;
using Plunderlust.World;
using UnityEngine;

namespace Plunderlust.Combat
{
    /// <summary>
    /// Runtime combat arena coordinator.
    /// Ensures all required managers (Ocean, Wind, ProjectilePool, HUD) are present,
    /// verifies the player's flagship setup, spawns combat targets in front of the camera,
    /// and handles automatic respawning when sunken ships founder.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class CombatArenaBootstrap : MonoBehaviour
    {
        private Transform _playerTransform;

        private void Awake()
        {
            // ponytail: lock target frame rate to 60fps and disable vSync fighting
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            PurgeLegacySceneLandmarks();
            EnsureCoreManagers();
            EnsureCombatHUD();
            EnsurePortTavernView();
        }

        private void Start()
        {
            EnsurePlayerShip();
            EnsureInWorldLocations();

            bool resumeSaved = PlayerPrefs.GetInt("Plunderlust_ResumeSavedGame", 1) == 1;
            if (resumeSaved && Plunderlust.Core.Persistence.ShipSaveManager.Instance != null && Plunderlust.Core.Persistence.ShipSaveManager.Instance.HasSaveFile())
            {
                var save = Plunderlust.Core.Persistence.ShipSaveManager.Instance.LoadSaveData();
                if (save != null && _playerTransform != null)
                {
                    Plunderlust.Core.Persistence.ShipSaveManager.Instance.ApplySaveToPlayer(_playerTransform.gameObject, save);
                    Debug.Log($"[CombatArenaBootstrap] Resumed saved voyage for '{save.shipCustomName}' (Tier: {save.tier}, Gold: {save.currentGold}).");
                }
            }
            else
            {
                string christenedName = PlayerPrefs.GetString("Plunderlust_NewVoyageName", "");
                if (!string.IsNullOrEmpty(christenedName) && _playerTransform != null)
                {
                    var identity = _playerTransform.GetComponent<ShipIdentitySystem>();
                    if (identity != null) identity.ChristenShip(christenedName);
                }
            }
        }

        private void EnsureCoreManagers()
        {
            if (FindAnyObjectByType<OceanWaveManager>() == null)
            {
                GameObject oceanGo = new GameObject("OceanWaveManager");
                oceanGo.AddComponent<OceanWaveManager>();
            }

            if (FindAnyObjectByType<WindManager>() == null)
            {
                GameObject windGo = new GameObject("WindManager");
                windGo.AddComponent<WindManager>();
            }

            if (ProjectilePool.Instance == null)
            {
                GameObject poolGo = new GameObject("ProjectilePool");
                poolGo.AddComponent<ProjectilePool>();
            }

            if (FactionManager.Instance == null)
            {
                GameObject factionGo = new GameObject("FactionManager");
                factionGo.AddComponent<FactionManager>();
            }

            if (NavalAIManager.Instance == null)
            {
                GameObject aiGo = new GameObject("NavalAIManager");
                aiGo.AddComponent<NavalAIManager>();
            }

            // Phase 5: World Generation, Floating Origin & Treasure Managers
            if (FindAnyObjectByType<Plunderlust.World.WorldChunkManager>() == null)
            {
                GameObject chunkGo = new GameObject("WorldChunkManager");
                chunkGo.AddComponent<Plunderlust.World.WorldChunkManager>();
            }

            if (FindAnyObjectByType<Plunderlust.World.FloatingOrigin>() == null)
            {
                GameObject originGo = new GameObject("FloatingOrigin");
                originGo.AddComponent<Plunderlust.World.FloatingOrigin>();
            }

            if (FindAnyObjectByType<Plunderlust.World.TreasureManager>() == null)
            {
                GameObject treasureGo = new GameObject("TreasureManager");
                treasureGo.AddComponent<Plunderlust.World.TreasureManager>();
            }

            if (FindAnyObjectByType<Plunderlust.World.MapBoundaryBarrier>() == null)
            {
                GameObject barrierGo = new GameObject("MapBoundaryBarrier");
                barrierGo.AddComponent<Plunderlust.World.MapBoundaryBarrier>();
            }

            if (FindAnyObjectByType<Plunderlust.World.DayNightController>() == null)
            {
                GameObject dayNightGo = new GameObject("DayNightController");
                dayNightGo.AddComponent<Plunderlust.World.DayNightController>();
            }

            if (FindAnyObjectByType<Plunderlust.World.EnvironmentVisualsManager>() == null)
            {
                GameObject envGo = new GameObject("EnvironmentVisualsManager");
                envGo.AddComponent<Plunderlust.World.EnvironmentVisualsManager>();
            }

            if (FindAnyObjectByType<Plunderlust.World.TreasureExcavationDirector>() == null)
            {
                GameObject directorGo = new GameObject("TreasureExcavationDirector");
                directorGo.AddComponent<Plunderlust.World.TreasureExcavationDirector>();
            }

            if (FindAnyObjectByType<Plunderlust.World.ProceduralWildlifeManager>() == null)
            {
                GameObject wildGo = new GameObject("ProceduralWildlifeManager");
                wildGo.AddComponent<Plunderlust.World.ProceduralWildlifeManager>();
            }

            if (FindAnyObjectByType<Plunderlust.Shipyard.ShipyardManager>() == null)
            {
                GameObject yardGo = new GameObject("ShipyardManager");
                yardGo.AddComponent<Plunderlust.Shipyard.ShipyardManager>();
            }

            if (FindAnyObjectByType<Plunderlust.Core.Persistence.ShipSaveManager>() == null)
            {
                GameObject saveGo = new GameObject("ShipSaveManager");
                saveGo.AddComponent<Plunderlust.Core.Persistence.ShipSaveManager>();
            }

            if (FindAnyObjectByType<Plunderlust.Port.PortMarketView>() == null)
            {
                GameObject marketViewGo = new GameObject("PortMarketView");
                marketViewGo.AddComponent<Plunderlust.Port.PortMarketView>();
            }

            if (FindAnyObjectByType<Plunderlust.Shipyard.ShipyardView>() == null)
            {
                GameObject yardViewGo = new GameObject("ShipyardView");
                yardViewGo.AddComponent<Plunderlust.Shipyard.ShipyardView>();
            }

            if (FindAnyObjectByType<Plunderlust.Audio.NauticalAudioManager>() == null)
            {
                GameObject audioGo = new GameObject("NauticalAudioManager");
                audioGo.AddComponent<Plunderlust.Audio.NauticalAudioManager>();
            }
            Plunderlust.Audio.NauticalAudioManager.Instance?.PlayVoyageMusic();

            if (FindAnyObjectByType<Plunderlust.World.OceanFloraAndFaunaDirector>() == null)
            {
                GameObject oceanDirGo = new GameObject("OceanFloraAndFaunaDirector");
                oceanDirGo.AddComponent<Plunderlust.World.OceanFloraAndFaunaDirector>();
            }
        }

        private void EnsureCombatHUD()
        {
            if (FindAnyObjectByType<PirateCombatHUD>() == null)
            {
                GameObject hudGo = new GameObject("PirateCombatHUD");
                hudGo.AddComponent<PirateCombatHUD>();
            }

            // Phase 1 Overhaul: Production UGUI Paper-Doll, Mobile Input Bridge, and Interactive Sea Chart
            if (FindAnyObjectByType<ShipPaperDollUI>() == null)
            {
                GameObject dollGo = new GameObject("ShipPaperDollUI");
                dollGo.AddComponent<ShipPaperDollUI>();
            }

            if (FindAnyObjectByType<MobileInputBridge>() == null)
            {
                GameObject inputGo = new GameObject("MobileInputBridge");
                inputGo.AddComponent<MobileInputBridge>();
            }

            if (FindAnyObjectByType<SeaChartMapUI>() == null)
            {
                GameObject chartGo = new GameObject("SeaChartMapUI");
                chartGo.AddComponent<SeaChartMapUI>();
            }

            if (FindAnyObjectByType<NavalMinimapAndChart>() == null)
            {
                GameObject mapGo = new GameObject("NavalMinimapAndChart");
                mapGo.AddComponent<NavalMinimapAndChart>();
            }

            if (FindAnyObjectByType<ShipInventoryModalUI>() == null)
            {
                GameObject invGo = new GameObject("ShipInventoryModalUI");
                invGo.AddComponent<ShipInventoryModalUI>();
            }

            if (FindAnyObjectByType<PauseMenuUI>() == null)
            {
                GameObject pauseGo = new GameObject("PauseMenuUI");
                pauseGo.AddComponent<PauseMenuUI>();
            }

            if (FindAnyObjectByType<GameOverUI>() == null)
            {
                GameObject gameOverGo = new GameObject("GameOverUI");
                gameOverGo.AddComponent<GameOverUI>();
            }

            if (FindAnyObjectByType<TutorialManagerUI>() == null)
            {
                GameObject tutGo = new GameObject("TutorialManagerUI");
                tutGo.AddComponent<TutorialManagerUI>();
            }
        }

        private void EnsurePortTavernView()
        {
            if (FindAnyObjectByType<PortTavernView>() == null)
            {
                GameObject viewGo = new GameObject("PortTavernView");
                viewGo.AddComponent<PortTavernView>();
            }
        }

        private void EnsurePlayerShip()
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo == null)
            {
                ShipLocomotionController existingLoco = FindAnyObjectByType<ShipLocomotionController>();
                if (existingLoco != null)
                {
                    playerGo = existingLoco.gameObject;
                    playerGo.tag = "Player";
                }
            }

            if (playerGo != null)
            {
                _playerTransform = playerGo.transform;

                ShipHullDataSO rowboatProfile = Resources.Load<ShipHullDataSO>("ShipData/ShipHull_Tier1_LeakyRowboat");
#if UNITY_EDITOR
                if (rowboatProfile == null)
                {
                    rowboatProfile = UnityEditor.AssetDatabase.LoadAssetAtPath<ShipHullDataSO>("Assets/Settings/ShipData/ShipHull_Tier1_LeakyRowboat.asset");
                }
#endif

                // Ensure required components are present on player
                if (playerGo.GetComponent<ShipSubsystemController>() == null)
                {
                    var sub = playerGo.AddComponent<ShipSubsystemController>();
                    if (rowboatProfile != null) sub.SetHullData(rowboatProfile);
                }
                if (playerGo.GetComponent<NavalCombatController>() == null)
                {
                    var combat = playerGo.AddComponent<NavalCombatController>();
                    if (rowboatProfile != null) combat.SetHullData(rowboatProfile);
                }
                if (playerGo.GetComponent<ShipWorldHealthBar>() == null)
                {
                    playerGo.AddComponent<ShipWorldHealthBar>();
                }
                if (playerGo.GetComponent<ShipCargoHold>() == null)
                {
                    var hold = playerGo.AddComponent<ShipCargoHold>();
                    if (rowboatProfile != null) hold.SetHullData(rowboatProfile);
                }
                if (playerGo.GetComponent<CrewManager>() == null)
                {
                    var crew = playerGo.AddComponent<CrewManager>();
                    if (rowboatProfile != null) crew.SetHullData(rowboatProfile);
                }
                if (playerGo.GetComponent<OfficerVisualFXController>() == null)
                {
                    playerGo.AddComponent<OfficerVisualFXController>();
                }
                if (playerGo.GetComponent<ShipCrewVisuals>() == null)
                {
                    playerGo.AddComponent<ShipCrewVisuals>();
                }
                if (playerGo.GetComponent<FactionPennantVisual>() == null)
                {
                    var pennant = playerGo.AddComponent<FactionPennantVisual>();
                    pennant.SetFaction(FactionType.CorsairBrethren);
                }
                if (playerGo.GetComponent<BoatPaddleAnimator>() == null)
                {
                    var paddleAnim = playerGo.AddComponent<BoatPaddleAnimator>();
                    paddleAnim.InitializePaddles();
                }
                if (playerGo.GetComponent<EmergencyRepairsController>() == null)
                {
                    playerGo.AddComponent<EmergencyRepairsController>();
                }
                if (playerGo.GetComponent<ShipIdentitySystem>() == null)
                {
                    playerGo.AddComponent<ShipIdentitySystem>();
                }
            }
        }

        private void PurgeLegacySceneLandmarks()
        {
            // Destroy legacy static ungrounded placeholder islands and test dummy ships from SampleScene.unity
            // Preserves fully populated viewport-baked landmarks that already contain modular 3D models
            GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                GameObject go = rootObjects[i];
                if (go == null) continue;
                if (go.name == "PortHaven_Island" || go.name == "SmugglerCove_Outpost" || go.name == "PortHaven_DrydockSlipway"
                    || go.name == "TargetVessel_Cutter" || go.name.StartsWith("TargetVessel"))
                {
                    // If the object is already a populated 3D landmark with models, preserve it for viewport & runtime!
                    if (go.transform.childCount > 2)
                    {
                        continue;
                    }
                    Debug.Log($"<color=cyan>[CombatArenaBootstrap]</color> Purging legacy static placeholder '{go.name}' in favor of rich 3D landmarks.");
                    DestroyImmediate(go);
                }
            }
        }

        private void EnsureInWorldLocations()
        {
            // If WorldChunkManager is present, it generates all ports and atolls in procedural chunks
            if (FindAnyObjectByType<Plunderlust.World.WorldChunkManager>() != null)
            {
                return;
            }

            // Standalone Fallback: Only spawn if WorldChunkManager is absent and landmark isn't already baked
            var port = GameObject.Find("PortHaven_Island");
            if (port == null || port.transform.childCount <= 2)
            {
                SpawnPortHavenLandmark(new Vector3(45f, 0f, 25f));
            }

            var tortuga = GameObject.Find("Tortuga_CorsairHaven");
            if (tortuga == null || tortuga.transform.childCount <= 2)
            {
                SpawnTortugaHavenLandmark(new Vector3(-120f, 0f, 180f));
            }

            var cove = GameObject.Find("SmugglerCove_Outpost");
            if (cove == null || cove.transform.childCount <= 2)
            {
                SpawnSmugglerCoveLandmark(new Vector3(-45f, 0f, 30f));
            }
        }

        private void SpawnTortugaHavenLandmark(Vector3 worldPos)
        {
            FactionPortBuilder.BuildPort(null, worldPos, Quaternion.identity, PortArchetype.CorsairHavenFreeport, "Tortuga_CorsairHaven");
        }

        private void SpawnDrydockSlipwayLandmark(Vector3 worldPos)
        {
            // FactionPortBuilder automatically constructs shipyards and drydocks within all ports
        }

        private void SpawnPortHavenLandmark(Vector3 worldPos)
        {
            FactionPortBuilder.BuildPort(null, worldPos, Quaternion.identity, PortArchetype.MerchantGuildHub, "PortHaven_Island");
        }

        private void SpawnSmugglerCoveLandmark(Vector3 worldPos)
        {
            FactionPortBuilder.BuildPort(null, worldPos, Quaternion.identity, PortArchetype.CorsairHavenFreeport, "SmugglerCove_Outpost");
        }

        private static void SpawnModelChild(Transform parent, string path, Vector3 localPos, Quaternion localRot, Vector3 localScale)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            GameObject prefab = Resources.Load<GameObject>("Models/" + fileName);
#if UNITY_EDITOR
            if (prefab == null)
            {
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
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
                    Renderer[] r = inst.GetComponentsInChildren<Renderer>();
                    foreach (var rend in r) rend.sharedMaterial = paletteMat;
                }

                // Add solid physical MeshColliders so ships cannot pass through rocks/docks
                MeshFilter[] mfs = inst.GetComponentsInChildren<MeshFilter>();
                foreach (var mf in mfs)
                {
                    if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                    {
                        mf.gameObject.AddComponent<MeshCollider>();
                    }
                }
            }
        }
    }
}
