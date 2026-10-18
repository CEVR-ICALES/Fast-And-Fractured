#if UNITY_EDITOR || DEVELOPMENT_BUILD
// Test tooling (driven from the editor through MCP): never in a release build.
using System.Collections;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Testing
{
    /// <summary>
    /// Test helper (MCP): real screen captures, overlay UI included, every few seconds across scene loads.
    /// <code>ScreenCaptureSeries.Start("Logs/Captures/end", 3f, 10);</code>
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
#endif
