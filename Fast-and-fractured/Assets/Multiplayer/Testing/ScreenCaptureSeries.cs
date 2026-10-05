using System.Collections;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Testing
{
    /// <summary>
    /// Test helper (MCP): saves real screen captures - overlay UI included, unlike camera
    /// screenshots - every few seconds, surviving scene loads.
    ///   ScreenCaptureSeries.Start("Logs/Captures/end", 3f, 10);
    /// </summary>
    public sealed class ScreenCaptureSeries : MonoBehaviour
    {
        public static string Start(string pathPrefix, float interval, int count)
        {
            var go = new GameObject("[ScreenCaptureSeries]");
            DontDestroyOnLoad(go);
            go.AddComponent<ScreenCaptureSeries>().StartCoroutine(Run(go, pathPrefix, interval, count));
            return $"capturing {count} frames every {interval}s to {pathPrefix}_N.png";
        }

        private static IEnumerator Run(GameObject owner, string prefix, float interval, int count)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(prefix)));
            for (int i = 0; i < count; i++)
            {
                yield return new WaitForSecondsRealtime(interval);
                ScreenCapture.CaptureScreenshot($"{prefix}_{i}.png");
            }
            Destroy(owner);
        }
    }
}
