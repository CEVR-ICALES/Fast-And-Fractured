using FishNet;
using FishNet.Managing;
using FishNet.Managing.Timing;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Reflection;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Sets FishNet's tick rate at runtime to 50, the project's fixed timestep, so per-tick forces integrate as in
    /// singleplayer. PhysicsMode stays Unity: TimeManager mode re-simulates the whole scene per replayed tick and
    /// spiralled into an unbounded catch-up on these maps.
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

        // Domain reload is off: reset the statics and the sceneLoaded subscription.
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
            // These Pro settings have no public API: set them on the menu NetworkManager at runtime.
            AllowPredictedSpawningField?.SetValue(nm.ServerManager, true);
            ReservedObjectIdsField?.SetValue(nm.ServerManager, PREDICTED_SPAWN_RESERVED_IDS);
            MpCanary.Mark($"TimingBootstrap: TickRate={TICK_RATE} on '{nm.name}' (PhysicsMode={nm.TimeManager.PhysicsMode})");
            Debug.Log($"[MultiplayerTimingBootstrap] TickRate={TICK_RATE}, predicted spawning enabled, reservedIds={PREDICTED_SPAWN_RESERVED_IDS} on '{nm.name}'.");
        }
    }
}
