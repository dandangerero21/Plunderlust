#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Plunderlust.UI;
using Plunderlust.Audio;
using Plunderlust.Ocean;

namespace Plunderlust.Editor
{
    /// <summary>
    /// Automated Scene Builder for the Dedicated 3D Live Main Menu Scene.
    /// Creates Assets/Scenes/MainMenu.unity, configures the camera, lighting, audio,
    /// and diorama coordinator, and registers the scene as Scene 0 in EditorBuildSettings.
    /// </summary>
    [InitializeOnLoad]
    public static class MainMenuSceneBuilder
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string GameplayScenePath = "Assets/Scenes/SampleScene.unity";

        static MainMenuSceneBuilder()
        {
            EditorApplication.delayCall += CheckAndBuildSceneOnLoad;
        }

        private static void CheckAndBuildSceneOnLoad()
        {
            if (!System.IO.File.Exists(MainMenuScenePath))
            {
                BuildMainMenuScene();
            }
            else
            {
                EnsureBuildSettingsConfigured();
            }
        }

        // [MenuItem("Plunderlust/Build Dedicated Main Menu Scene")]
        public static void BuildMainMenuScene()
        {
            Debug.Log("[MainMenuSceneBuilder] Constructing dedicated MainMenu.unity scene...");

            // 1. Create Clean New Scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Camera Setup
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(85f, 5.2f, 81.5f);
            camGo.transform.rotation = Quaternion.Euler(18f, 180f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 50f;
            camGo.AddComponent<AudioListener>();

            // 3. Directional Sunlight Setup
            GameObject sunGo = new GameObject("Sun_DirectionalLight");
            sunGo.transform.rotation = Quaternion.Euler(38f, 135f, 0f);
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.94f, 0.84f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;

            // 4. Managers & MainMenuController Setup
            GameObject oceanGo = new GameObject("OceanWaveManager");
            oceanGo.AddComponent<OceanWaveManager>();

            GameObject audioGo = new GameObject("NauticalAudioManager");
            audioGo.AddComponent<NauticalAudioManager>();

            GameObject menuGo = new GameObject("MainMenuController");
            menuGo.AddComponent<MainMenuController>();

            // 5. EventSystem Setup
            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            // 6. Ensure Scenes directory exists
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            // 6. Save Scene
            bool saved = EditorSceneManager.SaveScene(scene, MainMenuScenePath);
            Debug.Log($"[MainMenuSceneBuilder] Scene saved to '{MainMenuScenePath}': {saved}");

            // 7. Register in Build Settings
            EnsureBuildSettingsConfigured();

            AssetDatabase.Refresh();
        }

        public static void EnsureBuildSettingsConfigured()
        {
            List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>();

            if (System.IO.File.Exists(MainMenuScenePath))
            {
                buildScenes.Add(new EditorBuildSettingsScene(MainMenuScenePath, true));
            }

            if (System.IO.File.Exists(GameplayScenePath))
            {
                buildScenes.Add(new EditorBuildSettingsScene(GameplayScenePath, true));
            }

            EditorBuildSettings.scenes = buildScenes.ToArray();
            Debug.Log($"[MainMenuSceneBuilder] Build settings verified: 0={MainMenuScenePath}, 1={GameplayScenePath}");
        }
    }
}
#endif
