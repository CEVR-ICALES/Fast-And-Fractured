using System.Collections.Generic;
using System.Text;
using FishNet;
using FishNet.Object;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Testing
{
    /// <summary>
    /// Measurement instrument for networked vehicle motion. Answers, with numbers instead of
    /// impressions, the two questions we could not answer before: "is it actually jittering?" and
    /// "is client-side prediction working?".
    ///
    /// Sampling runs on the UNITY FRAME (that is where jitter is seen), recording for each tracked
    /// car the rendered world position of its visual root. From that we derive:
    ///
    ///   * smoothness — the mean magnitude of the second derivative of position (acceleration of
    ///     the rendered transform). Constant-velocity motion has a second derivative near zero;
    ///     stutter shows up as large alternating deltas even when average speed is steady. This is
    ///     the number that corresponds to "it trembles".
    ///   * backwards — how often the rendered position moves opposite to its own average direction.
    ///     Interpolation hiccups and reconcile snaps show up here as visible "rubber-banding".
    ///   * prediction — for the owned car, the reconcile correction distance reported by FishNet:
    ///     how far the server said we were wrong each time it corrected us. Small and rare means
    ///     prediction is doing its job; large or constant means it is fighting the simulation.
    ///
    /// Usage (from the MCP console or a debug key):
    ///   NetworkedVehicleDiagnostics.StartSampling();      // begin
    ///   NetworkedVehicleDiagnostics.Report();             // read results at any time
    ///   NetworkedVehicleDiagnostics.StopSampling();
    ///
    /// Run it on BOTH peers at once: the owner's numbers describe prediction quality, the remote
    /// peer's numbers describe what other players actually see.
    /// </summary>
    public static class NetworkedVehicleDiagnostics
    {
        private sealed class Track
        {
            public string Name;
            public bool IsOwner;
            public Transform Visual;
            public Transform Body;
            public readonly List<Vector3> Positions = new();
            public Vector3 LastPos;
            public Vector3 LastDelta;
            public bool HasLast;
            public double SecondDerivativeSum;
            public int SecondDerivativeCount;
            public int BackwardsSteps;
            public int Steps;
            public float MaxStepDistance;
            public Vector3 NetTravel;
            public Rigidbody Body_Rigidbody;
            public float MaxBodyGap;
            public double BodyGapSum;
            public int BodyGapCount;
        }

        private static readonly Dictionary<int, Track> Tracks = new();
        private static bool _sampling;
        private static float _startTime;
        private static int _frames;

        // --- reconcile correction tracking (owned car only) ---
        // A client that owns a car both predicts locally and gets corrected by the server. Every
        // correction moves the body; if the corrections are large or constant they are seen as a
        // tremble that the HOST never shows, because a server takes no corrections. This measures
        // them directly, which is the difference between "prediction is working and nudging" and
        // "prediction is diverging and being yanked back 50 times a second".
        private static FishNet.Managing.Predicting.PredictionManager _predictionManager;
        private static Rigidbody _ownedBody;
        private static Vector3 _preReconcilePos;
        private static double _reconcileSum;
        private static float _reconcileMax;
        private static int _reconcileCount;
        private static bool _hooked;

        // Domain reload is disabled in this project; statics must be reset explicitly.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Tracks.Clear();
            _sampling = false;
            _frames = 0;
            UnhookReconcile();
            _reconcileSum = 0;
            _reconcileMax = 0;
            _reconcileCount = 0;
        }

        private static void HookReconcile(Rigidbody ownedBody)
        {
            UnhookReconcile();
            if (ownedBody == null) return;

            _predictionManager = InstanceFinder.PredictionManager;
            if (_predictionManager == null) return;

            _ownedBody = ownedBody;
            _predictionManager.OnPreReconcile += OnPreReconcile;
            _predictionManager.OnPostReconcile += OnPostReconcile;
            _hooked = true;
        }

        private static void UnhookReconcile()
        {
            if (!_hooked || _predictionManager == null) return;
            _predictionManager.OnPreReconcile -= OnPreReconcile;
            _predictionManager.OnPostReconcile -= OnPostReconcile;
            _hooked = false;
        }

        private static void OnPreReconcile(uint clientTick, uint serverTick)
        {
            if (_ownedBody != null) _preReconcilePos = _ownedBody.position;
        }

        private static void OnPostReconcile(uint clientTick, uint serverTick)
        {
            if (_ownedBody == null) return;
            float correction = Vector3.Distance(_preReconcilePos, _ownedBody.position);
            _reconcileSum += correction;
            _reconcileMax = Mathf.Max(_reconcileMax, correction);
            _reconcileCount++;
        }

        public static bool IsSampling => _sampling;

        /// <summary>Begins sampling every networked vehicle currently spawned.</summary>
        public static string StartSampling()
        {
            Tracks.Clear();
            _frames = 0;
            _startTime = Time.realtimeSinceStartup;

            foreach (NetworkedVehicle vehicle in Object.FindObjectsByType<NetworkedVehicle>(FindObjectsSortMode.None))
            {
                if (vehicle == null) continue;
                NetworkObject nob = vehicle.GetComponent<NetworkObject>();
                if (nob == null) continue;

                // Measure the VISUAL root when present: that is what the player sees, and it is what
                // the NetworkTickSmoother writes to. Falling back to the body still detects stutter.
                Transform visual = vehicle.transform.Find("Visuals") ?? vehicle.transform;

                Tracks[nob.ObjectId] = new Track
                {
                    Name = vehicle.gameObject.name,
                    IsOwner = nob.IsOwner,
                    Visual = visual,
                    Body = vehicle.transform,
                    Body_Rigidbody = vehicle.GetComponent<Rigidbody>(),
                    LastPos = visual.position,
                };
            }

            if (Tracks.Count == 0)
                return "No networked vehicles found — start a match first.";

            _reconcileSum = 0;
            _reconcileMax = 0;
            _reconcileCount = 0;
            foreach (Track t in Tracks.Values)
                if (t.IsOwner) { HookReconcile(t.Body_Rigidbody); break; }

            _sampling = true;
            DiagnosticsRunner.Ensure();
            return $"Sampling {Tracks.Count} vehicle(s). Drive, then call Report().";
        }

        public static string StopSampling()
        {
            _sampling = false;
            UnhookReconcile();
            return "Sampling stopped.";
        }

        internal static void SampleFrame()
        {
            if (!_sampling) return;
            _frames++;

            foreach (Track t in Tracks.Values)
            {
                if (t.Visual == null) continue;

                Vector3 pos = t.Visual.position;
                Vector3 delta = pos - t.LastPos;

                if (t.HasLast)
                {
                    // Second derivative of position: near zero for smooth motion at any speed.
                    Vector3 jerk = delta - t.LastDelta;
                    t.SecondDerivativeSum += jerk.magnitude;
                    t.SecondDerivativeCount++;

                    if (delta.sqrMagnitude > 1e-8f && t.LastDelta.sqrMagnitude > 1e-8f &&
                        Vector3.Dot(delta.normalized, t.LastDelta.normalized) < -0.5f)
                    {
                        t.BackwardsSteps++;
                    }
                }

                // Distance between the physics body and the transform that renders it. Anything but
                // ~zero means two writers are fighting over the same object: the body integrates to
                // one pose while something else (a reconcile write, a smoother, an interpolation
                // backend) puts the transform somewhere different, and the object alternates
                // between them every frame. This is the number that exposed the 140 m owner-car
                // teleport loop that read on screen as "the camera has gone crazy".
                if (t.Body_Rigidbody != null && t.Body != null)
                {
                    float gap = Vector3.Distance(t.Body_Rigidbody.position, t.Body.position);
                    t.BodyGapSum += gap;
                    t.BodyGapCount++;
                    t.MaxBodyGap = Mathf.Max(t.MaxBodyGap, gap);
                }

                if (t.Positions.Count < 4096) t.Positions.Add(pos);
                t.MaxStepDistance = Mathf.Max(t.MaxStepDistance, delta.magnitude);
                t.NetTravel += delta;
                t.LastDelta = delta;
                t.LastPos = pos;
                t.HasLast = true;
                t.Steps++;
            }
        }

        /// <summary>Human-readable results. Safe to call while still sampling.</summary>
        public static string Report()
        {
            if (Tracks.Count == 0)
                return "Nothing sampled. Call StartSampling() during a match.";

            float elapsed = Mathf.Max(0.0001f, Time.realtimeSinceStartup - _startTime);
            var sb = new StringBuilder();
            sb.AppendLine($"=== Vehicle motion over {elapsed:F1}s ({_frames} frames, {_frames / elapsed:F0} fps) ===");
            sb.AppendLine($"peer: server={InstanceFinder.IsServerStarted} client={InstanceFinder.IsClientStarted}");

            foreach (Track t in Tracks.Values)
            {
                float travelled = t.NetTravel.magnitude;
                double smoothness = t.SecondDerivativeCount > 0 ? t.SecondDerivativeSum / t.SecondDerivativeCount : 0;
                float backwardsPct = t.Steps > 0 ? (100f * t.BackwardsSteps / t.Steps) : 0f;

                sb.AppendLine();
                sb.AppendLine($"[{t.Name}] {(t.IsOwner ? "OWNED (prediction)" : "REMOTE (spectated)")}");
                sb.AppendLine($"   travelled       : {travelled:F1} m");
                sb.AppendLine($"   jitter index    : {smoothness * 1000f:F2}  (mean |Δ²pos| in mm; lower is smoother)");
                sb.AppendLine($"   direction flips : {t.BackwardsSteps} of {t.Steps} steps ({backwardsPct:F1}%)");
                sb.AppendLine($"   largest jump    : {t.MaxStepDistance:F3} m in one frame");

                double meanGap = t.BodyGapCount > 0 ? t.BodyGapSum / t.BodyGapCount : 0;
                string verdict = t.MaxBodyGap > 1f ? "  <-- TWO WRITERS FIGHTING" : "";
                sb.AppendLine($"   body/transform  : mean {meanGap:F3} m, max {t.MaxBodyGap:F3} m{verdict}");
                if (t.Body_Rigidbody != null)
                    sb.AppendLine($"   interpolation   : {t.Body_Rigidbody.interpolation}");
            }

            sb.AppendLine();
            sb.AppendLine("Reading it: a car driving smoothly in a straight line scores a low jitter index and");
            sb.AppendLine("near-zero direction flips. Flips above a few percent, or a largest jump far above");
            sb.AppendLine("normal per-frame travel, mean the rendered position is being yanked around.");
            sb.AppendLine();
            if (_reconcileCount > 0)
            {
                double meanCorrection = _reconcileSum / _reconcileCount;
                sb.AppendLine($"reconcile corrections on the OWNED car: {_reconcileCount} in {elapsed:F1}s " +
                              $"({_reconcileCount / elapsed:F0}/s)");
                sb.AppendLine($"   mean {meanCorrection * 100:F2} cm, largest {_reconcileMax * 100:F2} cm");
                sb.AppendLine("   This is how far the server moved the car each time it disagreed with the local");
                sb.AppendLine("   prediction. Small and occasional is prediction working. Large, or happening on");
                sb.AppendLine("   every tick while driving steadily, means the local simulation is diverging and");
                sb.AppendLine("   being yanked back — seen as a tremble ONLY on the client, never on the host.");
            }
            else
            {
                sb.AppendLine("reconcile corrections: none recorded (host/server peer, or no owned car).");
            }

            sb.AppendLine();
            sb.AppendLine("body/transform must stay near zero (healthy measured: 0.02 m). A large value means");
            sb.AppendLine("the physics body and the rendered transform disagree, so the car alternates between");
            sb.AppendLine("two poses every frame. Rigidbody interpolation on a client-OWNED car causes exactly");
            sb.AppendLine("that (it fights the reconcile write); interpolation must stay None on the vehicles.");
            return sb.ToString();
        }

        /// <summary>Snapshot of the prediction-relevant state of the local player's car.</summary>
        public static string PredictionState()
        {
            var sb = new StringBuilder();
            foreach (NetworkedVehicle vehicle in Object.FindObjectsByType<NetworkedVehicle>(FindObjectsSortMode.None))
            {
                NetworkObject nob = vehicle.GetComponent<NetworkObject>();
                if (nob == null) continue;

                var smoothers = vehicle.GetComponentsInChildren<FishNet.Component.Transforming.Beta.NetworkTickSmoother>(true);
                sb.AppendLine($"[{vehicle.gameObject.name}] owner={nob.IsOwner} spawned={nob.IsSpawned}");
                sb.AppendLine($"   graphicalObject : {(nob.GetGraphicalObject() == null ? "NULL" : nob.GetGraphicalObject().name)}");
                sb.AppendLine($"   PredictionSmoother: {(nob.PredictionSmoother == null ? "null" : "ACTIVE")}");
                sb.AppendLine($"   NetworkTickSmoothers: {smoothers.Length}");
                var rb = vehicle.GetComponent<Rigidbody>();
                if (rb != null)
                    sb.AppendLine($"   rb: speed={rb.linearVelocity.magnitude * 3.6f:F1} km/h kinematic={rb.isKinematic} interpolation={rb.interpolation}");
                sb.AppendLine($"   parent: {(vehicle.transform.parent == null ? "none" : vehicle.transform.parent.name)}");
            }
            if (InstanceFinder.TimeManager != null)
                sb.AppendLine($"tickRate={InstanceFinder.TimeManager.TickRate} physicsMode={InstanceFinder.TimeManager.PhysicsMode} rtt={InstanceFinder.TimeManager.RoundTripTime}ms");
            return sb.Length == 0 ? "no vehicles" : sb.ToString();
        }

        /// <summary>Frame pump. Created on demand so nothing runs unless diagnostics are requested.</summary>
        private sealed class DiagnosticsRunner : MonoBehaviour
        {
            private static DiagnosticsRunner _instance;

            internal static void Ensure()
            {
                if (_instance != null) return;
                var go = new GameObject("[VehicleDiagnostics]");
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<DiagnosticsRunner>();
            }

            private void LateUpdate() => SampleFrame();
        }
    }
}
