using System;
using System.IO;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Freeze forensics: flushed one-liners to &lt;project&gt;/mp_canary.log, a ~1 Hz heartbeat plus stage markers, so
    /// the last line after a hang shows the stage. Editor and development builds only.
    /// </summary>
    public static class MpCanary
    {
        private static readonly bool Enabled = Application.isEditor || Debug.isDebugBuild;
        private static string _path;
        private static bool _failed;

        private static string PathToLog
        {
            get
            {
                if (_path == null)
                {
                    _path = System.IO.Path.Combine(Application.dataPath, "..", "mp_canary.log");
                }
                return _path;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _path = null;
            _failed = false;
            _consecutiveFailures = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Enabled) return;
            Mark($"=== play session start (unity {Application.unityVersion}) ===");
            var go = new GameObject("[MpCanary]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<HeartbeatRunner>();
        }

        // Builds launched from one folder share the file: tolerate collisions and tag each line with the process.
        private const int MAX_CONSECUTIVE_FAILURES = 50;
        private const float HEARTBEAT_SECONDS = 1f;
        private static int _consecutiveFailures;
        private static readonly string ProcessTag = Application.isEditor
            ? string.Empty : $"p={System.Diagnostics.Process.GetCurrentProcess().Id} ";

        public static void Mark(string message)
        {
            if (_failed || !Enabled) return;
            try
            {
                File.AppendAllText(PathToLog,
                    $"[{DateTime.Now:HH:mm:ss.fff}] {ProcessTag}f={Time.frameCount} {message}\n");
                _consecutiveFailures = 0;
            }
            catch
            {
                // Never let forensics take the game down (or spam retries).
                if (++_consecutiveFailures >= MAX_CONSECUTIVE_FAILURES) _failed = true;
            }
        }

        private sealed class HeartbeatRunner : MonoBehaviour
        {
            private float _nextBeatRealtime;

            private void Update()
            {
                if (Time.realtimeSinceStartup < _nextBeatRealtime) return;
                _nextBeatRealtime = Time.realtimeSinceStartup + HEARTBEAT_SECONDS;
                Mark($"heartbeat dt={Time.deltaTime * 1000f:F0}ms scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            }
        }
    }
}
