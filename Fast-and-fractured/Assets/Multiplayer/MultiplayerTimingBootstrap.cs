using FishNet;
using FishNet.Managing;
using FishNet.Managing.Timing;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Reflection;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Configures FishNet's TimeManager tick rate at runtime, so no scene edits are needed
    /// (the MainMenu NetworkManager has no TimeManager component serialized and would otherwise
    /// run the FishNet default of 30 ticks/s).
    ///
    /// Tick rate 50 == the project's fixed timestep (0.02s): the networked simulation ticks at
    /// the same cadence as singleplayer physics, so per-tick forces (state machine, dash,
    /// deceleration) integrate like they do in singleplayer.
    ///
    /// PhysicsMode.Unity (reverted 2026-09-16 back from a same-day PhysicsMode.TimeManager
    /// experiment; see git history for that attempt's changes to NetworkedVehicle.Reconcile).
    ///
    /// TimeManager mode was tried because FishNet's own Rigidbody prediction demo
    /// (Assets/FishNet/Demos/Prediction/Rigidbody) reconciles with a plain, unconditional
    /// PredictionRigidbody.Reconcile() - no blending, no history - which only looks smooth because
    /// TimeManager mode calls TimeManager.SimulatePhysics() once per REPLAYED tick, so the body
    /// teleports to the server's tick and then the same sequence of forces that already ran gets
    /// re-applied WITH a real physics step between each one, converging the trajectory back to
    /// nearly what was already being shown.
    ///
    /// CONFIRMED LIVE (2026-09-16) exactly the catch-up spiral this class previously only warned
    /// about in a comment: with TimeManager mode active, driving produced a WheelCollider spinning
    /// at 120,000-136,000 RPM and climbing - motor torque re-applied and re-simulated on every
    /// replayed tick, converting into wheel rotation instead of ground force once the tire's slip
    /// curve saturates, not vehicle motion - while Time.fixedTime fell 12+ seconds behind
    /// Time.realtimeSinceStartup and kept falling further behind. Every replayed tick that resimulates
    /// the whole PhysicsScene costs real time; a client that has fallen behind replays a bigger
    /// backlog, which costs more time, which falls further behind - unbounded on this project's
    /// maps (WheelCollider tire physics plus construction-site colliders). FishNet bounds the
    /// incoming STATE queue (PredictionManager.RemoveExcessiveStates) but not how many ticks one
    /// reconcile's replay loop itself will run, so nothing stopped this from compounding.
    ///
    /// Back on PhysicsMode.Unity, replayed ticks queue forces onto Unity's own independent
    /// FixedUpdate schedule instead of re-simulating, so a hard, unconditional reconcile does not
    /// converge and needs a soft correction in NetworkedVehicle.Reconcile - see that method's notes
    /// for the current approach and for the more elaborate ones already tried and abandoned.
    /// </summary>
    public static class MultiplayerTimingBootstrap
    {
        public const ushort TICK_RATE = 50;
        private const ushort PREDICTED_SPAWN_RESERVED_IDS = 64;
        private static readonly FieldInfo AllowPredictedSpawningField = typeof(FishNet.Managing.Server.ServerManager)
            .GetField("_allowPredictedSpawning", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ReservedObjectIdsField = typeof(FishNet.Managing.Server.ServerManager)
            .GetField("_reservedObjectIds", BindingFlags.NonPublic | BindingFlags.Instance);

        private static NetworkManager _configured;

        // Domain reload is disabled in this project's Enter Play Mode options: statics survive
        // between play sessions, so both the state and the sceneLoaded subscription must be
        // explicitly reset or they accumulate across runs.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _configured = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryConfigure();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryConfigure();

        private static void TryConfigure()
        {
            var nm = InstanceFinder.NetworkManager;
            if (nm == null || nm == _configured)
                return;

            _configured = nm;

            if (nm.IsServerStarted || nm.IsClientStarted)
            {
                // Changing tick rate mid-connection desyncs timing; only safe while offline.
                Debug.LogWarning("[MultiplayerTimingBootstrap] NetworkManager discovered with an active connection; tick rate left as-is.");
                return;
            }

            nm.TimeManager.SetTickRate(TICK_RATE);
            nm.TimeManager.SetPhysicsMode(PhysicsMode.Unity);
            if (nm.GetComponent<MultiplayerInputDeviceGuard>() == null)
                nm.gameObject.AddComponent<MultiplayerInputDeviceGuard>();
            // FishNet exposes these Pro settings in the inspector but not through a public API.
            // Configure the existing menu NetworkManager at runtime so no single-player scene or
            // prefab has to be modified.
            AllowPredictedSpawningField?.SetValue(nm.ServerManager, true);
            ReservedObjectIdsField?.SetValue(nm.ServerManager, PREDICTED_SPAWN_RESERVED_IDS);
            MpCanary.Mark($"TimingBootstrap: TickRate={TICK_RATE} on '{nm.name}' (PhysicsMode={nm.TimeManager.PhysicsMode})");
            Debug.Log($"[MultiplayerTimingBootstrap] TickRate={TICK_RATE}, predicted spawning enabled, reservedIds={PREDICTED_SPAWN_RESERVED_IDS} on '{nm.name}'.");
        }
    }
}
