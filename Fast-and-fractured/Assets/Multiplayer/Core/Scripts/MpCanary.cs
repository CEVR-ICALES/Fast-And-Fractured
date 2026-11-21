using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Freeze forensics: one-liners to &lt;project&gt;/mp_canary.log, a ~1 Hz heartbeat plus stage markers, so
    /// the last line after a hang shows the stage. Editor and development builds only.
    /// </summary>
    public static class MpCanary
    {
        public static readonly bool Enabled = Application.isEditor || Debug.isDebugBuild;
        private static string _path;
        private static bool _failed;

        // Lines are written by a background thread: a write per mark cost several file opens per frame.
        private static readonly ConcurrentQueue<string> Pending = new ConcurrentQueue<string>();
        private static readonly AutoResetEvent Signal = new AutoResetEvent(false);
        private static Thread _writer;
        private const int WRITER_IDLE_MILLISECONDS = 250;

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
            Application.quitting -= Flush;
            Application.quitting += Flush;
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
                if (_path == null) _path = Path.Combine(Application.dataPath, "..", "mp_canary.log");
                Pending.Enqueue(FormatLine(DateTime.Now, ProcessTag, Time.frameCount, message));
                EnsureWriter();
                Signal.Set();
            }
            catch
            {
                // Never let forensics take the game down (or spam retries).
                if (++_consecutiveFailures >= MAX_CONSECUTIVE_FAILURES) _failed = true;
            }
        }

        internal static string FormatLine(DateTime now, string processTag, int frame, string message) =>
            $"[{now:HH:mm:ss.fff}] {processTag}f={frame} {message}\n";

        internal static string Drain(ConcurrentQueue<string> queue)
        {
            if (queue.IsEmpty) return string.Empty;
            var block = new StringBuilder();
            while (queue.TryDequeue(out string line)) block.Append(line);
            return block.ToString();
        }

        private static void EnsureWriter()
        {
            if (_writer != null && _writer.IsAlive) return;
            _writer = new Thread(WriteLoop) { IsBackground = true, Name = "MpCanary" };
            _writer.Start();
        }

        private static void WriteLoop()
        {
            while (true)
            {
                Signal.WaitOne(WRITER_IDLE_MILLISECONDS);
                Flush();
            }
        }

        private static void Flush()
        {
            string path = _path;
            if (path == null) return;
            string block = Drain(Pending);
            if (block.Length == 0) return;
            try
            {
                File.AppendAllText(path, block);
                _consecutiveFailures = 0;
            }
            catch
            {
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
