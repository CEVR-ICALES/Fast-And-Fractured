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
    /// This project deliberately uses PhysicsMode.Unity. FishNet's TimeManager mode re-simulates
    /// the entire map PhysicsScene for every replay tick; on these physics-heavy maps a single
    /// long frame creates a catch-up spiral which hard-locks both editors. Vehicles still use
    /// owner input prediction plus server reconcile, while projectiles and ability objects use
    /// deterministic per-tick simulation and FishNet Pro predicted spawning.
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
