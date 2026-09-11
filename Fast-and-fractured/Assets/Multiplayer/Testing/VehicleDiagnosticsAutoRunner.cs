using System.IO;
using FishNet;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Testing
{
    /// <summary>
    /// Self-installs NetworkedVehicleDiagnostics and periodically dumps its Report() to a log file,
    /// but ONLY on a non-host client - i.e. only on a machine that actually predicts and reconciles
    /// its own car over a real connection, which the host never does (server and client are the
    /// same process there, so its "owned" car has nothing to reconcile against).
    ///
    /// Written so this can be read after the fact from disk (like MpAutoRecorder), instead of
    /// requiring anyone to inject code into that peer's live process. Exists specifically to
    /// measure whether the "client's own car still trembles while the host's is smooth" report
    /// (2026-09-11) is reconciliation snapping the predicted rigidbody, since that is real per-tick
    /// motion and no render-side smoother can hide it - VehicleVisualSmoother only reproduces
    /// faithfully whatever the physics body actually did.
    ///
    /// Output: <project>/../VehicleDiagnosticsAutoRun.log
    ///
    /// To remove it, delete this file. It writes nothing on a host or a dedicated server.
    /// </summary>
    public static class VehicleDiagnosticsAutoRunner
    {
        private const float ReportIntervalSeconds = 5f;

        private static Runner _runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_runner != null) return;
            var go = new GameObject("[VehicleDiagnosticsAutoRunner]");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _runner = go.AddComponent<Runner>();
        }

        private static string LogPath =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "VehicleDiagnosticsAutoRun.log");

        private sealed class Runner : MonoBehaviour
        {
            private bool _sampling;
            private float _nextReportTime;
            private float _nextRetryTime;

            private void Update()
            {
                // Only a real (non-host) client reconciles its own predictions against the server;
                // a host's ServerManager and ClientManager are the same process, so IsServerStarted
                // is true there too and this correctly stays silent on it.
                bool isRemoteClientOnly = InstanceFinder.IsClientStarted && !InstanceFinder.IsServerStarted;
                if (!isRemoteClientOnly)
                {
                    if (_sampling)
                    {
                        NetworkedVehicleDiagnostics.StopSampling();
                        _sampling = false;
                    }
                    return;
                }

                if (!_sampling)
                {
                    if (Time.unscaledTime < _nextRetryTime) return;
                    _nextRetryTime = Time.unscaledTime + 1f;

                    string startResult = NetworkedVehicleDiagnostics.StartSampling();
                    if (!NetworkedVehicleDiagnostics.IsSampling) return; // no car spawned yet; retry

                    _sampling = true;
                    _nextReportTime = Time.unscaledTime + ReportIntervalSeconds;
                    Flush($"=== VehicleDiagnosticsAutoRunner ===\ntime: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{startResult}\n");
                    return;
                }

                if (Time.unscaledTime < _nextReportTime) return;
                _nextReportTime = Time.unscaledTime + ReportIntervalSeconds;
                Flush(NetworkedVehicleDiagnostics.Report());
            }

            private void Flush(string text)
            {
                try { File.AppendAllText(LogPath, text + "\n"); }
                catch { /* a diagnostic must never break the game */ }
            }

            private void OnDestroy()
            {
                if (_sampling) NetworkedVehicleDiagnostics.StopSampling();
            }
        }
    }
}
