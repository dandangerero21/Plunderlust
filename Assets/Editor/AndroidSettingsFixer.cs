#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.IO;

namespace Plunderlust.Editor
{
    /// <summary>
    /// Forces correct Android PlayerSettings directly in Editor memory.
    /// ponytail: minimal one-shot script to uncheck resizeableActivity, lock landscape,
    /// and switch Graphics API to OpenGLES3 to fix Qualcomm Adreno Vulkan half-screen bug.
    /// </summary>
    [InitializeOnLoad]
    public static class AndroidSettingsFixer
    {
        static AndroidSettingsFixer()
        {
            EditorApplication.delayCall += ApplyFixes;
        }

        // [MenuItem("Plunderlust/Apply Android Fullscreen Fixes")]
        public static void ApplyFixes()
        {
            // 1. Switch to OpenGLES3 (CRITICAL: Fixes Qualcomm Adreno 6xx Vulkan GMEM half-screen bug!)
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new GraphicsDeviceType[] { GraphicsDeviceType.OpenGLES3 });

            // 2. DISABLE resizeableActivity (prevents OPPO/ColorOS from forcing 1:1 split-screen/freeform)
            PlayerSettings.Android.resizeableActivity = false;

            // 3. Lock orientation to Landscape
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // 4. Render outside safe area (fills behind punch-hole camera)
            PlayerSettings.Android.renderOutsideSafeArea = true;

            // 5. Max aspect ratio 2.4 (21.6:9 modern wide phones)
            PlayerSettings.Android.maxAspectRatio = 2.4f;

            AssetDatabase.SaveAssets();
            Debug.Log("[AndroidSettingsFixer] Applied: Graphics API = OpenGLES3, resizeableActivity = false, LandscapeLeft locked.");
        }
    }
}
#endif
