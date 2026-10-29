using System.IO;
using FishNet;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Testing
{
    /// <summary>
    /// Dumps NetworkedVehicleDiagnostics.Report() periodically to &lt;project&gt;/../VehicleDiagnosticsAutoRun.log, only
    /// on a non-host client (the one that reconciles its own car).
    /// </summary>
    public static class VehicleDiagnosticsAutoRunner
    {
        private const float ReportIntervalSeconds = 5f;
        private const float RetryIntervalSeconds = 1f;

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
                // A host is also a server: only a real client reconciles.
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
                    _nextRetryTime = Time.unscaledTime + RetryIntervalSeconds;

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
