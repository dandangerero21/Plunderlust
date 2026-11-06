using UnityEngine;
using System.Collections;

namespace Plunderlust.Diagnostics
{
    /// <summary>
    /// Displays and logs screen metrics on startup to verify full-screen resolution on Android.
    /// </summary>
    public class ScreenDebugLogger : MonoBehaviour
    {
        private string _info = "";
        private float _expireTime;

        private void Awake()
        {
            _expireTime = Time.realtimeSinceStartup + 6f;
            RefreshMetrics("Awake");
            StartCoroutine(PollDelayed());
        }

        private IEnumerator PollDelayed()
        {
            yield return null;
            RefreshMetrics("Frame1");
            yield return new WaitForSeconds(1f);
            RefreshMetrics("1s");
        }

        private void RefreshMetrics(string phase)
        {
            _info = $"[{phase}] Screen: {Screen.width}x{Screen.height} (dpi={Screen.dpi:F0}) | Display: {Display.main.systemWidth}x{Display.main.systemHeight} | Cutout: {Screen.cutouts.Length}";
            UnityEngine.Debug.LogWarning($"[SCREEN_DEBUG] {_info}");
        }

        private void OnGUI()
        {
            if (Time.realtimeSinceStartup < _expireTime && !string.IsNullOrEmpty(_info))
            {
                GUI.color = Color.yellow;
                GUI.skin.label.fontSize = 26;
                GUI.Label(new Rect(30, 20, 1400, 80), _info);
            }
        }
    }
}
