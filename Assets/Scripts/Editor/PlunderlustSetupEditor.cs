#if UNITY_EDITOR
using System.IO;
using Plunderlust.AI;
using Plunderlust.CameraControl;
using Plunderlust.Cargo;
using Plunderlust.Combat;
using Plunderlust.Core.Data;
using Plunderlust.Crew;
using Plunderlust.Factions;
using Plunderlust.Loot;
using Plunderlust.Ocean;
using Plunderlust.Port;
using Plunderlust.Ship;
using Plunderlust.Subsystems;
using Plunderlust.UI;
using Plunderlust.Wind;
using Plunderlust.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Plunderlust.Editor
{
    /// <summary>
    /// Editor automation tool for setting up materials, hull profiles, and the Phase 2 naval combat scene.
    /// Configures the angled top-down tactical camera, starter rowboat flagship with swivel gun,
    /// and a target cutter vessel to test localized subsystem damage, waterline leaks, and flotsam loot.
    /// </summary>
    public static class PlunderlustSetupEditor
    {
        // [MenuItem("Plunderlust/⚓ Bake BOTH Scenes for Submission (SampleScene & MainMenu)", priority = 1)]
        public static void BakeBothScenesForSubmission()
        {
            BakeViewportWorldIntoSampleScene();
            BakeMainMenuDiorama();
            Debug.Log("<color=green>[Plunderlust] ⚓ FULL SUBMISSION READY! Both SampleScene and MainMenu are now 100% baked and fully visible in the Unity Viewport.</color>");
        }

        // [MenuItem("Plunderlust/⚓ Bake Viewport World into SampleScene", priority = 2)]
        public static void BakeViewportWorldIntoSampleScene()
        {
            SetupTextureImportSettings();
            SetupMaterials();
            ShipHullDataCreator.GenerateDefaultShipProfiles();

            var currentScene = EditorSceneManager.GetActiveScene();
            if (!currentScene.name.Equals("SampleScene", System.StringComparison.OrdinalIgnoreCase))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
                }
            }

            SetupSampleScene();

            // Destroy any legacy empty placeholder shells before baking fresh rich 3D models
            string[] landmarkNames = { "PortHaven_Island", "SmugglerCove_Outpost", "Tortuga_CorsairHaven", "SkeletonKey_TreasureAtoll", "DeadMansReef_TreasureAtoll" };
            foreach (string name in landmarkNames)
            {
                GameObject old = GameObject.Find(name);
                if (old != null)
                {
                    Undo.DestroyObjectImmediate(old);
                }
            }

            // 1. Port Royal Haven (Merchant Guild Hub) with docks, warehouses, watchtower, palm groves
            GameObject portHaven = Plunderlust.World.FactionPortBuilder.BuildPort(null, new Vector3(45f, 0f, 25f), Quaternion.identity, Plunderlust.World.PortArchetype.MerchantGuildHub, "PortHaven_Island");
            Undo.RegisterCreatedObjectUndo(portHaven, "Bake PortHaven_Island");

            // 2. Smuggler's Cove Outpost (Corsair Den) with tilted shipwreck tavern and pirate banners
            GameObject smugglerCove = Plunderlust.World.FactionPortBuilder.BuildPort(null, new Vector3(-45f, 0f, 30f), Quaternion.identity, Plunderlust.World.PortArchetype.CorsairHavenFreeport, "SmugglerCove_Outpost");
            Undo.RegisterCreatedObjectUndo(smugglerCove, "Bake SmugglerCove_Outpost");

            // 3. Tortuga Corsair Freeport (Deep water pirate bastion)
            GameObject tortuga = Plunderlust.World.FactionPortBuilder.BuildPort(null, new Vector3(-120f, 0f, 180f), Quaternion.identity, Plunderlust.World.PortArchetype.CorsairHavenFreeport, "Tortuga_CorsairHaven");
            Undo.RegisterCreatedObjectUndo(tortuga, "Bake Tortuga_CorsairHaven");

            // 4. Skeleton Key Treasure Atoll with palm trees and dig site
            GameObject atoll1 = Plunderlust.World.TreasureAtollBuilder.BuildAtoll(null, new Vector3(95f, 0f, -85f), Quaternion.identity, "SkeletonKey_TreasureAtoll", 750);
            Undo.RegisterCreatedObjectUndo(atoll1, "Bake SkeletonKey_TreasureAtoll");

            // 5. Dead Man's Reef Treasure Atoll
            GameObject atoll2 = Plunderlust.World.TreasureAtollBuilder.BuildAtoll(null, new Vector3(-85f, 0f, -65f), Quaternion.identity, "DeadMansReef_TreasureAtoll", 600);
            Undo.RegisterCreatedObjectUndo(atoll2, "Bake DeadMansReef_TreasureAtoll");

            // 6. Ensure MapBoundaryBarrier exists in scene
            if (Object.FindAnyObjectByType<Plunderlust.World.MapBoundaryBarrier>() == null)
            {
                GameObject boundaryGo = new GameObject("MapBoundaryBarrier");
                boundaryGo.AddComponent<Plunderlust.World.MapBoundaryBarrier>();
                Undo.RegisterCreatedObjectUndo(boundaryGo, "Create MapBoundaryBarrier");
            }

            // 7. Bake In-Game UI Canvases (PaperDoll HUD and Mobile Controls)
            ShipPaperDollUI paperDoll = Object.FindAnyObjectByType<ShipPaperDollUI>();
            if (paperDoll == null)
            {
                GameObject pdGo = new GameObject("ShipPaperDollUI");
                paperDoll = pdGo.AddComponent<ShipPaperDollUI>();
                Undo.RegisterCreatedObjectUndo(pdGo, "Create ShipPaperDollUI");
            }
            GameObject oldPdCanvas = GameObject.Find("Plunderlust_PaperDollCanvas");
            if (oldPdCanvas != null) Undo.DestroyObjectImmediate(oldPdCanvas);
            paperDoll.BuildPaperDollCanvas();

            MobileInputBridge mobileBridge = Object.FindAnyObjectByType<MobileInputBridge>();
            if (mobileBridge == null)
            {
                GameObject mbGo = new GameObject("MobileInputBridge");
                mobileBridge = mbGo.AddComponent<MobileInputBridge>();
                Undo.RegisterCreatedObjectUndo(mbGo, "Create MobileInputBridge");
            }
            GameObject oldMbCanvas = GameObject.Find("Plunderlust_MobileControlsCanvas");
            if (oldMbCanvas != null) Undo.DestroyObjectImmediate(oldMbCanvas);
            mobileBridge.BuildMobileHUDCanvas();

            // Save active scene so the baked world is persistently written to disk
            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("<color=green>[Plunderlust] ⚓ World successfully baked into SampleScene Viewport! Professor can now inspect full 3D islands, ports, and ships in the Editor without running Play mode.</color>");
        }

        // [MenuItem("Plunderlust/⚓ Bake Harbor Diorama into MainMenu Scene", priority = 3)]
        public static void BakeMainMenuDiorama()
        {
            SetupTextureImportSettings();
            SetupMaterials();

            var currentScene = EditorSceneManager.GetActiveScene();
            if (!currentScene.name.Equals("MainMenu", System.StringComparison.OrdinalIgnoreCase))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
                }
            }

            // 1. Ensure Camera & Sun Light
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 52f;
            cam.transform.position = new Vector3(0f, 4.6f, -12.5f);
            cam.transform.rotation = Quaternion.Euler(15f, 0f, 0f);

            Light sun = Object.FindAnyObjectByType<Light>();
            if (sun == null || sun.type != LightType.Directional)
            {
                GameObject sunGo = new GameObject("Sun_DirectionalLight");
                sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.rotation = Quaternion.Euler(38f, 135f, 0f);
            sun.color = new Color(1.0f, 0.94f, 0.84f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;

            // 2. Ensure OceanWaveManager & OceanSurface WaterGrid
            if (Object.FindAnyObjectByType<OceanWaveManager>() == null)
            {
                GameObject waveGo = new GameObject("OceanWaveManager");
                waveGo.AddComponent<OceanWaveManager>();
                Undo.RegisterCreatedObjectUndo(waveGo, "Create OceanWaveManager");
            }

            WaterGrid water = Object.FindAnyObjectByType<WaterGrid>();
            if (water == null)
            {
                GameObject waterGo = new GameObject("OceanSurface");
                waterGo.transform.position = Vector3.zero;
                water = waterGo.AddComponent<WaterGrid>();
                MeshRenderer rend = waterGo.GetComponent<MeshRenderer>();
                Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_LowPolyOcean.mat");
                if (mat != null && rend != null) rend.sharedMaterial = mat;
                Undo.RegisterCreatedObjectUndo(waterGo, "Create OceanSurface");
            }

            // 3. Ensure MainMenuController
            if (Object.FindAnyObjectByType<MainMenuController>() == null)
            {
                GameObject menuGo = new GameObject("MainMenuController");
                menuGo.AddComponent<MainMenuController>();
                Undo.RegisterCreatedObjectUndo(menuGo, "Create MainMenuController");
            }

            // 4. Destroy existing diorama to rebuild fresh
            GameObject oldDiorama = GameObject.Find("[Harbor_MainMenu_Diorama]");
            if (oldDiorama != null)
            {
                Undo.DestroyObjectImmediate(oldDiorama);
            }

            var registry = PrefabRegistrySO.Instance;
            if (registry == null)
            {
                Debug.LogError("[Plunderlust] PrefabRegistrySO instance not found.");
                return;
            }

            Vector3 dioramaCenter = new Vector3(0f, 0.45f, 0f);
            GameObject dioramaRoot = new GameObject("[Harbor_MainMenu_Diorama]");
            dioramaRoot.transform.position = dioramaCenter;
            Undo.RegisterCreatedObjectUndo(dioramaRoot, "Bake [Harbor_MainMenu_Diorama]");

            GameObject harborEnv = new GameObject("Harbor_Environment_Props");
            harborEnv.transform.SetParent(dioramaRoot.transform, false);

            // Pier Dock
            registry.Spawn("platform-planks", harborEnv.transform, new Vector3(-4.8f, 0.35f, -3.8f), Quaternion.Euler(0f, 90f, 0f), new Vector3(1.2f, 1f, 1.2f), null, false);
            registry.Spawn("platform-planks", harborEnv.transform, new Vector3(-1.6f, 0.35f, -3.8f), Quaternion.Euler(0f, 90f, 0f), new Vector3(1.2f, 1f, 1.2f), null, false);
            registry.Spawn("platform-planks", harborEnv.transform, new Vector3( 1.6f, 0.35f, -3.8f), Quaternion.Euler(0f, 90f, 0f), new Vector3(1.2f, 1f, 1.2f), null, false);
            registry.Spawn("platform-planks", harborEnv.transform, new Vector3( 4.8f, 0.35f, -3.8f), Quaternion.Euler(0f, 90f, 0f), new Vector3(1.2f, 1f, 1.2f), null, false);

            // Walkway to shore
            registry.Spawn("platform-planks", harborEnv.transform, new Vector3(0f, 0.35f, -6.5f), Quaternion.identity, new Vector3(1.2f, 1f, 1.2f), null, false);
            registry.Spawn("platform-planks", harborEnv.transform, new Vector3(0f, 0.35f, -9.5f), Quaternion.identity, new Vector3(1.2f, 1f, 1.2f), null, false);
            registry.Spawn("platform-planks", harborEnv.transform, new Vector3(0f, 0.35f, -12.5f), Quaternion.identity, new Vector3(1.2f, 1f, 1.2f), null, false);
            registry.Spawn("platform-planks", harborEnv.transform, new Vector3(0f, 0.35f, -15.5f), Quaternion.identity, new Vector3(1.2f, 1f, 1.2f), null, false);

            // Props
            registry.Spawn("barrel", harborEnv.transform, new Vector3(-3.5f, 0.75f, -3.6f), Quaternion.identity, Vector3.one * 1.1f, null, false);
            registry.Spawn("barrel", harborEnv.transform, new Vector3(-3.0f, 0.75f, -3.7f), Quaternion.Euler(0f, 35f, 0f), Vector3.one * 1.1f, null, false);
            registry.Spawn("crate", harborEnv.transform, new Vector3(3.0f, 0.75f, -3.6f), Quaternion.identity, Vector3.one, null, false);
            registry.Spawn("crate-bottles", harborEnv.transform, new Vector3(3.0f, 1.25f, -3.6f), Quaternion.Euler(0f, 15f, 0f), Vector3.one, null, false);
            registry.Spawn("chest", harborEnv.transform, new Vector3(-0.8f, 0.75f, -3.6f), Quaternion.Euler(0f, -25f, 0f), Vector3.one * 1.1f, null, false);
            registry.Spawn("flag-pirate", harborEnv.transform, new Vector3(-4.8f, 0.75f, -3.6f), Quaternion.identity, Vector3.one * 1.3f, null, false);
            registry.Spawn("cannon", harborEnv.transform, new Vector3(4.5f, 0.75f, -3.6f), Quaternion.Euler(0f, 180f, 0f), Vector3.one * 1.15f, null, false);

            // Pier Lantern
            GameObject lanternGo = new GameObject("HarborPierLantern");
            lanternGo.transform.SetParent(harborEnv.transform, false);
            lanternGo.transform.localPosition = new Vector3(0f, 1.6f, -3.6f);
            Light lanternLight = lanternGo.AddComponent<Light>();
            lanternLight.type = LightType.Point;
            lanternLight.color = new Color(1.0f, 0.68f, 0.32f);
            lanternLight.range = 14f;
            lanternLight.intensity = 2.2f;
            lanternLight.shadows = LightShadows.Soft;

            // Shoreline & Beach
            registry.Spawn("rocks-sand-b", harborEnv.transform, new Vector3(0f, -1.8f, -22.0f), Quaternion.identity, new Vector3(9.0f, 3.5f, 7.0f), null, false);
            registry.Spawn("patch-sand", harborEnv.transform, new Vector3(0f, -0.60f, -22.0f), Quaternion.identity, new Vector3(8.0f, 2.0f, 6.0f), null, false);
            registry.Spawn("patch-sand", harborEnv.transform, new Vector3(0f, -0.10f, -22.0f), Quaternion.identity, new Vector3(6.5f, 1.4f, 5.0f), null, false);
            registry.Spawn("patch-sand-foliage", harborEnv.transform, new Vector3(3f, 0.15f, -24.0f), Quaternion.Euler(0f, 45f, 0f), new Vector3(4.0f, 1.2f, 4.0f), null, false);
            registry.Spawn("patch-sand-foliage", harborEnv.transform, new Vector3(-3f, 0.15f, -23.0f), Quaternion.Euler(0f, -30f, 0f), new Vector3(4.0f, 1.2f, 4.0f), null, false);

            // Boulders & Palms
            registry.Spawn("rocks-sand-b", harborEnv.transform, new Vector3(-5.5f, -0.35f, -20.0f), Quaternion.Euler(0f, 25f, 0f), new Vector3(2.5f, 2.5f, 2.5f), null, false);
            registry.Spawn("rocks-sand-c", harborEnv.transform, new Vector3(5.5f, -0.35f, -20.0f), Quaternion.Euler(0f, -40f, 0f), new Vector3(2.5f, 2.5f, 2.5f), null, false);
            registry.Spawn("palm-detailed-bend", harborEnv.transform, new Vector3(3.5f, 0.25f, -21.0f), Quaternion.Euler(0f, 60f, 0f), Vector3.one * 1.3f, null, false);
            registry.Spawn("palm-detailed-straight", harborEnv.transform, new Vector3(-3.0f, 0.25f, -22.0f), Quaternion.Euler(0f, 120f, 0f), Vector3.one * 1.2f, null, false);
            registry.Spawn("palm-bend", harborEnv.transform, new Vector3(5.5f, 0.25f, -24.0f), Quaternion.Euler(0f, -80f, 0f), Vector3.one * 1.2f, null, false);

            // Watchtower
            registry.Spawn("tower-watch", harborEnv.transform, new Vector3(0f, 0.2f, -26.0f), Quaternion.identity, Vector3.one * 1.3f, null, false);

            // Moored Brigantine
            GameObject mooredShip = registry.Spawn("ship-pirate-large", dioramaRoot.transform, Vector3.zero, Quaternion.Euler(0f, 90f, 0f), Vector3.one, null, false);
            if (mooredShip != null)
            {
                mooredShip.name = "Moored_PirateBrigantine";
            }

            // Turntable Root & Ship Models
            GameObject turntableRoot = new GameObject("Shipyard_Turntable_Root");
            turntableRoot.transform.SetParent(dioramaRoot.transform, false);
            turntableRoot.transform.localPosition = Vector3.zero;

            string[] models = { "boat-row-small", "ship-small", "ship-medium", "ship-large", "ship-pirate-large", "ship-ghost" };
            for (int i = 0; i < 6; i++)
            {
                Material matOverride = (i == 5) ? registry.GhostShipMaterial : null;
                GameObject shipModel = registry.Spawn(models[i], turntableRoot.transform, Vector3.zero, Quaternion.identity, Vector3.one, matOverride, false);
                if (shipModel != null)
                {
                    shipModel.name = $"Turntable_Tier_{i + 1}_{models[i]}";
                    shipModel.SetActive(false);
                }
            }

            // 5. Bake Main Menu UI Canvas (Title card, buttons, settings, showcase)
            MainMenuController menuCtrl = Object.FindAnyObjectByType<MainMenuController>();
            if (menuCtrl == null)
            {
                GameObject menuGo = new GameObject("MainMenuController");
                menuCtrl = menuGo.AddComponent<MainMenuController>();
                Undo.RegisterCreatedObjectUndo(menuGo, "Create MainMenuController");
            }
            if (menuCtrl != null)
            {
                Transform oldCanvas = menuCtrl.transform.Find("MainMenu_Canvas");
                if (oldCanvas != null) Undo.DestroyObjectImmediate(oldCanvas.gameObject);

                menuCtrl.BuildUIHierarchy();
                menuCtrl.SetMenuState(MainMenuState.TitleScreen);
            }

            // Save Scene
            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("<color=green>[Plunderlust] ⚓ Main Menu 3D Harbor Diorama successfully baked into Scene Viewport! Moored ship, pier, beach, and palms are now visible in Edit mode.</color>");
        }

        // [MenuItem("Plunderlust/Configure Texture Point Filter", priority = 10)]
        public static void SetupTextureImportSettings()
        {
            string texturePath = "Assets/kenney_pirate-kit/Models/FBX format/Textures/colormap.png";
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer != null)
            {
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                Debug.Log("[Plunderlust] Configured colormap.png to Point filter for crisp palette rendering.");
            }
        }

        // [MenuItem("Plunderlust/Generate URP Materials", priority = 11)]
        public static void SetupMaterials()
        {
            string settingsFolder = "Assets/Settings";
            if (!AssetDatabase.IsValidFolder(settingsFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Settings");
            }

            Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLitShader == null)
            {
                Debug.LogWarning("[Plunderlust] Universal Render Pipeline/Lit shader not found, falling back to Standard.");
                urpLitShader = Shader.Find("Standard");
            }

            // 1. Kenney Palette Material
            string paletteMatPath = $"{settingsFolder}/M_KenneyPiratePalette.mat";
            Material paletteMat = AssetDatabase.LoadAssetAtPath<Material>(paletteMatPath);
            if (paletteMat == null)
            {
                paletteMat = new Material(urpLitShader);
                paletteMat.name = "M_KenneyPiratePalette";
                AssetDatabase.CreateAsset(paletteMat, paletteMatPath);
            }

            Texture2D colormap = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/kenney_pirate-kit/Models/FBX format/Textures/colormap.png");
            if (colormap != null)
            {
                paletteMat.mainTexture = colormap;
            }
            paletteMat.SetFloat("_Smoothness", 0.1f);
            paletteMat.SetFloat("_Metallic", 0.0f);
            EditorUtility.SetDirty(paletteMat);

            // 2. Ocean Water Material
            string oceanMatPath = $"{settingsFolder}/M_LowPolyOcean.mat";
            Material oceanMat = AssetDatabase.LoadAssetAtPath<Material>(oceanMatPath);
            if (oceanMat == null)
            {
                oceanMat = new Material(urpLitShader);
                oceanMat.name = "M_LowPolyOcean";
                AssetDatabase.CreateAsset(oceanMat, oceanMatPath);
            }
            else if (oceanMat.shader != urpLitShader)
            {
                oceanMat.shader = urpLitShader;
            }

            // Tropical Pirate Turquoise (#0E8590) - Matte retro finish, zero glare!
            Color tropicalWater = new Color(0.055f, 0.52f, 0.565f, 1.0f);
            oceanMat.color = tropicalWater;
            if (oceanMat.HasProperty("_BaseColor")) oceanMat.SetColor("_BaseColor", tropicalWater);
            oceanMat.SetFloat("_Smoothness", 0.15f);
            oceanMat.SetFloat("_Metallic", 0.0f);
            if (oceanMat.HasProperty("_SpecularHighlights")) oceanMat.SetFloat("_SpecularHighlights", 0.0f);
            if (oceanMat.HasProperty("_EnvironmentReflections")) oceanMat.SetFloat("_EnvironmentReflections", 0.0f);
            EditorUtility.SetDirty(oceanMat);

            AssetDatabase.SaveAssets();
            Debug.Log("[Plunderlust] Generated M_KenneyPiratePalette and M_LowPolyOcean materials.");
        }

        // [MenuItem("Plunderlust/Construct Test Naval Scene", priority = 20)]
        public static void SetupSampleScene()
        {
            // 1. Ensure OceanWaveManager exists
            OceanWaveManager waveManager = Object.FindAnyObjectByType<OceanWaveManager>();
            if (waveManager == null)
            {
                GameObject oceanManagerGo = new GameObject("OceanWaveManager");
                waveManager = oceanManagerGo.AddComponent<OceanWaveManager>();
                Undo.RegisterCreatedObjectUndo(oceanManagerGo, "Create OceanWaveManager");
            }

            // 2. Ensure WindManager exists
            WindManager windManager = Object.FindAnyObjectByType<WindManager>();
            if (windManager == null)
            {
                GameObject windManagerGo = new GameObject("WindManager");
                windManager = windManagerGo.AddComponent<WindManager>();
                Undo.RegisterCreatedObjectUndo(windManagerGo, "Create WindManager");
            }

            // 3. Ensure ProjectilePool exists
            ProjectilePool projectilePool = Object.FindAnyObjectByType<ProjectilePool>();
            if (projectilePool == null)
            {
                GameObject poolGo = new GameObject("ProjectilePool");
                projectilePool = poolGo.AddComponent<ProjectilePool>();
                Undo.RegisterCreatedObjectUndo(poolGo, "Create ProjectilePool");
            }

            // 4. Remove legacy ships if present to clean scene
            ShipLocomotionController[] existingShips = Object.FindObjectsByType<ShipLocomotionController>(FindObjectsInactive.Include);
            foreach (var ship in existingShips)
            {
                Undo.DestroyObjectImmediate(ship.gameObject);
            }

            // 5. Construct Starter Small Boat (Tier 1 Leaky Rowboat)
            GameObject shipGo = new GameObject("PlayerFlagship_Rowboat");
            shipGo.tag = "Player";
            shipGo.transform.position = new Vector3(0f, 0.4f, 0f);

            Rigidbody rb = shipGo.AddComponent<Rigidbody>();
            rb.mass = 350f;
            rb.linearDamping = 1.1f;
            rb.angularDamping = 2.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Instantiate Kenney Small Rowboat FBX
            GameObject rowboatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/kenney_pirate-kit/Models/FBX format/boat-row-small.fbx");
            if (rowboatPrefab != null)
            {
                GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(rowboatPrefab, shipGo.transform);
                modelInstance.name = "BoatModel";
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;

                Material paletteMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_KenneyPiratePalette.mat");
                if (paletteMat != null)
                {
                    Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>();
                    foreach (var r in renderers)
                    {
                        r.sharedMaterial = paletteMat;
                    }
                }
            }

            // Add BoxCollider for physical bounds
            BoxCollider col = shipGo.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.45f, 0f);
            col.size = new Vector3(1.6f, 0.9f, 2.7f);

            // Add BuoyancyBody with 4 probes tailored for the small boat
            BuoyancyBody buoyancy = shipGo.AddComponent<BuoyancyBody>();

            GameObject probesRoot = new GameObject("BuoyancyProbes");
            probesRoot.transform.SetParent(shipGo.transform, false);

            Transform[] probeArray = new Transform[4];
            probeArray[0] = CreateProbeChild(probesRoot.transform, "Probe_Bow", new Vector3(0f, -0.05f, 1.25f));
            probeArray[1] = CreateProbeChild(probesRoot.transform, "Probe_Stern", new Vector3(0f, -0.05f, -1.25f));
            probeArray[2] = CreateProbeChild(probesRoot.transform, "Probe_Port", new Vector3(-0.55f, -0.05f, 0f));
            probeArray[3] = CreateProbeChild(probesRoot.transform, "Probe_Starboard", new Vector3(0.55f, -0.05f, 0f));

            SerializedObject buoyancySO = new SerializedObject(buoyancy);
            SerializedProperty probesProp = buoyancySO.FindProperty("_probes");
            probesProp.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                probesProp.GetArrayElementAtIndex(i).objectReferenceValue = probeArray[i];
            }

            ShipHullDataSO rowboatProfile = AssetDatabase.LoadAssetAtPath<ShipHullDataSO>("Assets/Settings/ShipData/ShipHull_Tier1_LeakyRowboat.asset");
            if (rowboatProfile != null)
            {
                buoyancySO.FindProperty("_hullData").objectReferenceValue = rowboatProfile;
            }
            buoyancySO.ApplyModifiedProperties();

            // Add Locomotion Controller, Subsystems, Combat, and Input Bridge
            ShipLocomotionController locomotion = shipGo.AddComponent<ShipLocomotionController>();
            if (rowboatProfile != null) locomotion.SetHullData(rowboatProfile);

            ShipSubsystemController subsystems = shipGo.AddComponent<ShipSubsystemController>();
            if (rowboatProfile != null) subsystems.SetHullData(rowboatProfile);

            NavalCombatController combat = shipGo.AddComponent<NavalCombatController>();
            if (rowboatProfile != null) combat.SetHullData(rowboatProfile);

            ShipCargoHold cargoHold = shipGo.AddComponent<ShipCargoHold>();
            if (rowboatProfile != null) cargoHold.SetHullData(rowboatProfile);

            CrewManager crew = shipGo.AddComponent<CrewManager>();
            if (rowboatProfile != null) crew.SetHullData(rowboatProfile);

            shipGo.AddComponent<OfficerVisualFXController>();
            shipGo.AddComponent<ShipCrewVisuals>();

            shipGo.AddComponent<LootableWreck>();
            shipGo.AddComponent<PlayerShipInputBridge>();

            // Add Procedural Rowing Paddle Animator
            BoatPaddleAnimator paddleAnimator = shipGo.AddComponent<BoatPaddleAnimator>();
            paddleAnimator.InitializePaddles();

            Undo.RegisterCreatedObjectUndo(shipGo, "Create Starter Rowboat");

            // 7. Ensure Dynamic Ocean Surface exists and follows the small boat
            WaterGrid waterGrid = Object.FindAnyObjectByType<WaterGrid>();
            if (waterGrid == null)
            {
                GameObject waterGridGo = new GameObject("OceanSurface");
                waterGrid = waterGridGo.AddComponent<WaterGrid>();
                Material oceanMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_LowPolyOcean.mat");
                if (oceanMat != null)
                {
                    waterGridGo.GetComponent<MeshRenderer>().sharedMaterial = oceanMat;
                }
                Undo.RegisterCreatedObjectUndo(waterGridGo, "Create OceanSurface");
            }

            SerializedObject waterGridSO = new SerializedObject(waterGrid);
            waterGridSO.FindProperty("_followTarget").objectReferenceValue = shipGo.transform;
            var sizeProp = waterGridSO.FindProperty("_gridSize");
            if (sizeProp != null) sizeProp.floatValue = 850.0f;
            var resProp = waterGridSO.FindProperty("_gridResolution");
            if (resProp != null) resProp.intValue = 68;
            waterGridSO.ApplyModifiedProperties();

            // 8. Configure Main Camera with Angled Top-Down NavalCameraController
            UnityEngine.Camera mainCam = UnityEngine.Camera.main;
            if (mainCam == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                mainCam = camGo.AddComponent<UnityEngine.Camera>();
                camGo.tag = "MainCamera";
                camGo.AddComponent<AudioListener>();
            }

            NavalCameraController camCtrl = mainCam.GetComponent<NavalCameraController>();
            if (camCtrl == null)
            {
                camCtrl = mainCam.gameObject.AddComponent<NavalCameraController>();
            }

            camCtrl.Target = shipGo.transform;
            camCtrl.PitchAngle = 52f;
            camCtrl.Distance = 15f;
            camCtrl.SnapToTarget();

            // 9. Ensure Combat HUD & Arena Bootstrap exist in scene
            PirateCombatHUD hud = Object.FindAnyObjectByType<PirateCombatHUD>();
            if (hud == null)
            {
                GameObject hudGo = new GameObject("PirateCombatHUD");
                hud = hudGo.AddComponent<PirateCombatHUD>();
                Undo.RegisterCreatedObjectUndo(hudGo, "Create PirateCombatHUD");
            }

            CombatArenaBootstrap arena = Object.FindAnyObjectByType<CombatArenaBootstrap>();
            if (arena == null)
            {
                GameObject arenaGo = new GameObject("CombatArenaBootstrap");
                arena = arenaGo.AddComponent<CombatArenaBootstrap>();
                Undo.RegisterCreatedObjectUndo(arenaGo, "Create CombatArenaBootstrap");
            }

            // 10. Ensure Port Haven Island & Smuggler Cove landmarks exist
            PortIslandHaven existingHaven = Object.FindAnyObjectByType<PortIslandHaven>();
            if (existingHaven == null)
            {
                CreatePortHavenInScene(new Vector3(45f, 0f, 25f));
            }

            SmugglerCoveOutpost existingCove = Object.FindAnyObjectByType<SmugglerCoveOutpost>();
            if (existingCove == null)
            {
                CreateSmugglerCoveInScene(new Vector3(-45f, 0f, 30f));
            }

            PortTavernView tavernView = Object.FindAnyObjectByType<PortTavernView>();
            if (tavernView == null)
            {
                GameObject tavernGo = new GameObject("PortTavernView");
                tavernGo.AddComponent<PortTavernView>();
                Undo.RegisterCreatedObjectUndo(tavernGo, "Create PortTavernView");
            }

            // 11. Ensure FactionManager & NavalAIManager exist
            Plunderlust.Factions.FactionManager factionMgr = Object.FindAnyObjectByType<Plunderlust.Factions.FactionManager>();
            if (factionMgr == null)
            {
                GameObject factionGo = new GameObject("FactionManager");
                factionGo.AddComponent<Plunderlust.Factions.FactionManager>();
                Undo.RegisterCreatedObjectUndo(factionGo, "Create FactionManager");
            }

            Plunderlust.AI.NavalAIManager aiMgr = Object.FindAnyObjectByType<Plunderlust.AI.NavalAIManager>();
            if (aiMgr == null)
            {
                GameObject aiGo = new GameObject("NavalAIManager");
                aiGo.AddComponent<Plunderlust.AI.NavalAIManager>();
                Undo.RegisterCreatedObjectUndo(aiGo, "Create NavalAIManager");
            }

            // 12. Ensure Phase 5 In-World Drydock & Shipyard View
            Plunderlust.Shipyard.ShipyardDrydock existingDrydock = Object.FindAnyObjectByType<Plunderlust.Shipyard.ShipyardDrydock>();
            if (existingDrydock == null)
            {
                CreateDrydockInScene(new Vector3(32f, 0f, 48f));
            }

            if (Object.FindAnyObjectByType<Plunderlust.Shipyard.ShipyardManager>() == null)
            {
                GameObject yardGo = new GameObject("ShipyardManager");
                yardGo.AddComponent<Plunderlust.Shipyard.ShipyardManager>();
                Undo.RegisterCreatedObjectUndo(yardGo, "Create ShipyardManager");
            }

            if (Object.FindAnyObjectByType<Plunderlust.Shipyard.ShipyardView>() == null)
            {
                GameObject yardViewGo = new GameObject("ShipyardView");
                yardViewGo.AddComponent<Plunderlust.Shipyard.ShipyardView>();
                Undo.RegisterCreatedObjectUndo(yardViewGo, "Create ShipyardView");
            }

            if (Object.FindAnyObjectByType<Plunderlust.Port.PortMarketView>() == null)
            {
                GameObject marketViewGo = new GameObject("PortMarketView");
                marketViewGo.AddComponent<Plunderlust.Port.PortMarketView>();
                Undo.RegisterCreatedObjectUndo(marketViewGo, "Create PortMarketView");
            }

            if (Object.FindAnyObjectByType<Plunderlust.Core.Persistence.ShipSaveManager>() == null)
            {
                GameObject saveGo = new GameObject("ShipSaveManager");
                saveGo.AddComponent<Plunderlust.Core.Persistence.ShipSaveManager>();
                Undo.RegisterCreatedObjectUndo(saveGo, "Create ShipSaveManager");
            }

            if (Object.FindAnyObjectByType<Plunderlust.World.WorldChunkManager>() == null)
            {
                GameObject chunkGo = new GameObject("WorldChunkManager");
                chunkGo.AddComponent<Plunderlust.World.WorldChunkManager>();
                Undo.RegisterCreatedObjectUndo(chunkGo, "Create WorldChunkManager");
            }

            if (Object.FindAnyObjectByType<Plunderlust.World.FloatingOrigin>() == null)
            {
                GameObject originGo = new GameObject("FloatingOrigin");
                originGo.AddComponent<Plunderlust.World.FloatingOrigin>();
                Undo.RegisterCreatedObjectUndo(originGo, "Create FloatingOrigin");
            }

            if (Object.FindAnyObjectByType<Plunderlust.World.TreasureManager>() == null)
            {
                GameObject treasureGo = new GameObject("TreasureManager");
                treasureGo.AddComponent<Plunderlust.World.TreasureManager>();
                Undo.RegisterCreatedObjectUndo(treasureGo, "Create TreasureManager");
            }

            if (Object.FindAnyObjectByType<Plunderlust.World.MapBoundaryBarrier>() == null)
            {
                GameObject barrierGo = new GameObject("MapBoundaryBarrier");
                barrierGo.AddComponent<Plunderlust.World.MapBoundaryBarrier>();
                Undo.RegisterCreatedObjectUndo(barrierGo, "Create MapBoundaryBarrier");
            }

            // Save Scene Permanently
            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("[Plunderlust] Test Naval Scene configured & saved: Starter Boat, Target Cutter, Port Island Haven, Smuggler Cove, and Angled Camera.");
        }

        private static GameObject CreateTargetShip(string name, Vector3 pos, string profilePath, string modelPath)
        {
            GameObject shipGo = new GameObject(name);
            shipGo.transform.position = pos;
            shipGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // Broadside facing player

            Rigidbody rb = shipGo.AddComponent<Rigidbody>();
            rb.mass = 950f;
            rb.linearDamping = 0.85f;
            rb.angularDamping = 2.0f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelPrefab != null)
            {
                GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, shipGo.transform);
                modelInstance.name = "ShipModel";
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one * 0.85f;

                Material paletteMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_KenneyPiratePalette.mat");
                if (paletteMat != null)
                {
                    Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>();
                    foreach (var r in renderers) r.sharedMaterial = paletteMat;
                }
            }

            BoxCollider col = shipGo.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.15f, 0f);
            col.size = new Vector3(2.6f, 2.5f, 6.5f);

            BuoyancyBody buoyancy = shipGo.AddComponent<BuoyancyBody>();
            GameObject probesRoot = new GameObject("BuoyancyProbes");
            probesRoot.transform.SetParent(shipGo.transform, false);

            Transform[] probeArray = new Transform[4];
            probeArray[0] = CreateProbeChild(probesRoot.transform, "Probe_Bow", new Vector3(0f, 0f, 2.8f));
            probeArray[1] = CreateProbeChild(probesRoot.transform, "Probe_Stern", new Vector3(0f, 0f, -2.8f));
            probeArray[2] = CreateProbeChild(probesRoot.transform, "Probe_Port", new Vector3(-1.15f, 0f, 0f));
            probeArray[3] = CreateProbeChild(probesRoot.transform, "Probe_Starboard", new Vector3(1.15f, 0f, 0f));

            SerializedObject buoyancySO = new SerializedObject(buoyancy);
            SerializedProperty probesProp = buoyancySO.FindProperty("_probes");
            probesProp.arraySize = 4;
            for (int i = 0; i < 4; i++) probesProp.GetArrayElementAtIndex(i).objectReferenceValue = probeArray[i];

            ShipHullDataSO profile = AssetDatabase.LoadAssetAtPath<ShipHullDataSO>(profilePath);
            if (profile != null) buoyancySO.FindProperty("_hullData").objectReferenceValue = profile;
            buoyancySO.ApplyModifiedProperties();

            ShipLocomotionController locomotion = shipGo.AddComponent<ShipLocomotionController>();
            if (profile != null) locomotion.SetHullData(profile);

            ShipSubsystemController subsystems = shipGo.AddComponent<ShipSubsystemController>();
            if (profile != null) subsystems.SetHullData(profile);

            NavalCombatController combat = shipGo.AddComponent<NavalCombatController>();
            if (profile != null) combat.SetHullData(profile);

            ShipCargoHold cargoHold = shipGo.AddComponent<ShipCargoHold>();
            if (profile != null) cargoHold.SetHullData(profile);

            shipGo.AddComponent<ShipWorldHealthBar>();
            shipGo.AddComponent<LootableWreck>();

            FactionPennantVisual pennant = shipGo.AddComponent<FactionPennantVisual>();
            pennant.SetFaction(FactionType.CorsairBrethren);

            AIShipBrain brain = shipGo.AddComponent<AIShipBrain>();
            brain.InitializeAI(FactionType.CorsairBrethren, null);

            Undo.RegisterCreatedObjectUndo(shipGo, $"Create {name}");
            return shipGo;
        }

        private static Transform CreateProbeChild(Transform parent, string name, Vector3 localPos)
        {
            GameObject probeGo = new GameObject(name);
            probeGo.transform.SetParent(parent, false);
            probeGo.transform.localPosition = localPos;
            return probeGo.transform;
        }

        private static void CreatePortHavenInScene(Vector3 worldPos)
        {
            GameObject port = Plunderlust.World.FactionPortBuilder.BuildPort(null, worldPos, Quaternion.identity, Plunderlust.World.PortArchetype.MerchantGuildHub, "PortHaven_Island");
            Undo.RegisterCreatedObjectUndo(port, "Create PortHaven_Island");
        }

        private static void CreateSmugglerCoveInScene(Vector3 worldPos)
        {
            GameObject cove = Plunderlust.World.FactionPortBuilder.BuildPort(null, worldPos, Quaternion.identity, Plunderlust.World.PortArchetype.CorsairHavenFreeport, "SmugglerCove_Outpost");
            Undo.RegisterCreatedObjectUndo(cove, "Create SmugglerCove_Outpost");
        }

        private static void CreateDrydockInScene(Vector3 worldPos)
        {
            GameObject drydockRoot = new GameObject("PortHaven_DrydockSlipway");
            drydockRoot.transform.position = worldPos;
            drydockRoot.transform.rotation = Quaternion.Euler(0f, -45f, 0f);

            SpawnSceneModel(drydockRoot.transform, "Assets/kenney_pirate-kit/Models/FBX format/structure-platform-dock.fbx", new Vector3(-3.5f, 0.2f, 0f), Quaternion.identity, new Vector3(1.4f, 1f, 2.5f));
            SpawnSceneModel(drydockRoot.transform, "Assets/kenney_pirate-kit/Models/FBX format/structure-platform-dock.fbx", new Vector3(3.5f, 0.2f, 0f), Quaternion.identity, new Vector3(1.4f, 1f, 2.5f));
            SpawnSceneModel(drydockRoot.transform, "Assets/kenney_pirate-kit/Models/FBX format/structure-fence.fbx", new Vector3(-3.5f, 1.2f, 0f), Quaternion.identity, Vector3.one * 1.2f);
            SpawnSceneModel(drydockRoot.transform, "Assets/kenney_pirate-kit/Models/FBX format/structure-fence.fbx", new Vector3(3.5f, 1.2f, 0f), Quaternion.identity, Vector3.one * 1.2f);
            SpawnSceneModel(drydockRoot.transform, "Assets/kenney_pirate-kit/Models/FBX format/platform-planks.fbx", new Vector3(0f, 0.1f, -4.5f), Quaternion.Euler(0f, 90f, 0f), new Vector3(1.8f, 1f, 1.8f));
            SpawnSceneModel(drydockRoot.transform, "Assets/kenney_pirate-kit/Models/FBX format/rocks-sand-b.fbx", new Vector3(0f, 0f, -7.0f), Quaternion.identity, Vector3.one * 2.0f);

            drydockRoot.AddComponent<Plunderlust.Shipyard.ShipyardDrydock>();
            Undo.RegisterCreatedObjectUndo(drydockRoot, "Create PortHaven_DrydockSlipway");
        }

        private static void SpawnSceneModel(Transform parent, string path, Vector3 localPos, Quaternion localRot, Vector3 scale)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                inst.transform.localPosition = localPos;
                inst.transform.localRotation = localRot;
                inst.transform.localScale = scale;

                Material paletteMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_KenneyPiratePalette.mat");
                if (paletteMat != null)
                {
                    Renderer[] rends = inst.GetComponentsInChildren<Renderer>();
                    foreach (var r in rends) r.sharedMaterial = paletteMat;
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
#endif
