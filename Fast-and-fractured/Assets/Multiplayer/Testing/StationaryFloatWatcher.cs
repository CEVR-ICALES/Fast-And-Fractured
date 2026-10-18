using System.IO;
using FishNet;
using FishNet.Object;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Testing
{
    /// <summary>
    /// Diagnostic for a client's own car floating while stopped: logs a snapshot to
    /// &lt;project&gt;/../StationaryFloatWatcher.log when its Visuals diverge from the body at rest.
    /// </summary>
    public static class StationaryFloatWatcher
    {
        private const float FloatThreshold = 0.15f;
        private const float StationarySpeedThreshold = 0.3f;
        private const int MaxEvents = 20;
        private const float ScanIntervalSeconds = 0.25f;
        private const float GroundRayDistance = 50f;

        private static Runner _runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_runner != null) return;
            var go = new GameObject("[StationaryFloatWatcher]");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _runner = go.AddComponent<Runner>();
        }

        private static string LogPath =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "StationaryFloatWatcher.log");

        private sealed class Runner : MonoBehaviour
        {
            private int _events;
            private float _nextScanTime;

            private void Update()
            {
                bool isRemoteClientOnly = InstanceFinder.IsClientStarted && !InstanceFinder.IsServerStarted;
                if (!isRemoteClientOnly || _events >= MaxEvents) return;

                if (Time.unscaledTime < _nextScanTime) return;
                _nextScanTime = Time.unscaledTime + ScanIntervalSeconds;

                System.Type nvType = System.Type.GetType("FastAndFractured.Multiplayer.NetworkedVehicle, Assembly-CSharp");
                if (nvType == null) return;

                foreach (var mb in FindObjectsByType(nvType, FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    var vehicle = (MonoBehaviour)mb;
                    var nob = vehicle.GetComponent<NetworkObject>();
                    if (nob == null || !nob.IsOwner) continue; // only the OWNED car reproduces this

                    var rb = vehicle.GetComponent<Rigidbody>();
                    if (rb == null) continue;
                    if (rb.linearVelocity.magnitude > StationarySpeedThreshold) continue;

                    Transform visuals = vehicle.transform.Find("Visuals");
                    if (visuals == null) continue;

                    float verticalGap = visuals.position.y - rb.position.y;
                    if (Mathf.Abs(verticalGap) < FloatThreshold) continue;

                    Dump(vehicle, rb, visuals, verticalGap);
                }
            }

            private void Dump(MonoBehaviour vehicle, Rigidbody rb, Transform visuals, float verticalGap)
            {
                _events++;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"=== StationaryFloatWatcher event #{_events} at {System.DateTime.Now:HH:mm:ss} (frame {Time.frameCount}) ===");
                sb.AppendLine($"vehicle: {vehicle.gameObject.name}");
                sb.AppendLine($"rb.position        : {rb.position}  (speed {rb.linearVelocity.magnitude:F3})");
                sb.AppendLine($"visuals.position   : {visuals.position}");
                sb.AppendLine($"vertical gap (visuals.y - rb.y): {verticalGap:F4} m");

                bool grounded = Physics.Raycast(rb.position, Vector3.down, out RaycastHit hit, GroundRayDistance);
                sb.AppendLine($"ground under rb.position: {(grounded ? $"{Vector3.Distance(rb.position, hit.point):F3} m below, hit '{hit.collider.name}'" : "no hit within 50m")}");

                System.Type smootherType = System.Type.GetType("FastAndFractured.Multiplayer.VehicleVisualSmoother, Assembly-CSharp");
                var smoother = smootherType != null ? vehicle.GetComponent(smootherType) : null;
                if (smoother == null)
                {
                    sb.AppendLine("VehicleVisualSmoother: NOT PRESENT on this vehicle");
                }
                else
                {
                    var samplesField = smootherType.GetField("_samples", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    var samples = samplesField?.GetValue(smoother) as System.Collections.ICollection;
                    sb.AppendLine($"VehicleVisualSmoother: enabled={((Behaviour)smoother).enabled} sampleCount={(samples == null ? "?" : samples.Count.ToString())}");

                    if (samples != null && samples.Count > 0)
                    {
                        var sampleType = smootherType.GetNestedType("Sample", System.Reflection.BindingFlags.NonPublic);
                        var stepField = sampleType.GetField("Step");
                        var posField = sampleType.GetField("Position");
                        int i = 0;
                        foreach (var s in samples)
                        {
                            long step = (long)stepField.GetValue(s);
                            Vector3 pos = (Vector3)posField.GetValue(s);
                            sb.AppendLine($"   sample[{i}] step={step} pos={pos}");
                            i++;
                        }
                    }
                }

                try { File.AppendAllText(LogPath, sb.ToString() + "\n"); }
                catch { /* a diagnostic must never break the game */ }
            }
        }
    }
}
