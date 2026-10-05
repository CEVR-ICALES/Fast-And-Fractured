using System;
using System.IO;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Crash/freeze forensics for multiplayer sessions. Editor console logs are useless when the
    /// main thread hard-locks (nothing flushes), so this writes flushed one-liners to
    /// &lt;project&gt;/mp_canary.log — per project folder, so host editor and ParrelSync clone get
    /// separate files. A ~1Hz heartbeat plus stage markers means the LAST line in the file after
    /// a freeze tells us exactly which stage the main thread died in.
    ///
    /// Cost: one small file append per second plus one per stage marker — negligible.
    /// </summary>
    public static class MpCanary
    {
        private static string _path;
        private static bool _failed;

        private static string PathToLog
        {
            get
            {
                if (_path == null)
                {
                    // dataPath = <project>/Assets → write next to the project folder root.
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
            Mark($"=== play session start (unity {Application.unityVersion}) ===");
            var go = new GameObject("[MpCanary]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<HeartbeatRunner>();
        }

        // Several builds launched from one folder share the file: a write can collide with another
        // process's, so only give up after many failures in a row, and tag each line with the process.
        private const int MAX_CONSECUTIVE_FAILURES = 50;
        private static int _consecutiveFailures;
        private static readonly string ProcessTag = Application.isEditor
            ? string.Empty : $"p={System.Diagnostics.Process.GetCurrentProcess().Id} ";

        public static void Mark(string message)
        {
            if (_failed) return;
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
                _nextBeatRealtime = Time.realtimeSinceStartup + 1f;
                Mark($"heartbeat dt={Time.deltaTime * 1000f:F0}ms scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            }
        }
    }
}
