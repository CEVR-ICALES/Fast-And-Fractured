using System.Collections.Generic;
using System.Reflection;
using FishNet;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utilities;
using Utilities.Managers.PauseSystem;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// In multiplayer, opening the pause menu must not freeze the local simulation: the server
    /// keeps simulating, so pausing rigidbodies/inputs/timers locally only desyncs this peer
    /// (and a paused host would fight every client). PauseManager already leaves Time.timeScale
    /// alone, but it still notifies every registered IPausable (physics, input, timer and
    /// animation pausables all freeze their targets).
    ///
    /// PauseManager invokes onGamePaused/onGameResumed BEFORE iterating its pausable list, so this
    /// gate subscribes to those events and, while a network session is active, empties the private
    /// list right before the loop runs — the pause menu still opens, nothing gameplay-side stops.
    /// Singleplayer sessions (no active connection) are untouched.
    /// </summary>
    public static class MultiplayerPauseGate
    {
        private static FieldInfo _pausablesField;
        private static PauseManager _hooked;

        // Domain reload is disabled in Enter Play Mode options; statics survive between plays,
        // so both state and the sceneLoaded subscription must be reset or they accumulate.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsForDomainReloadDisabled()
        {
            _hooked = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            _pausablesField = typeof(PauseManager).GetField("_pausableComponents",
                BindingFlags.NonPublic | BindingFlags.Instance);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryHook();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryHook();

        private static void TryHook()
        {
            var pm = PauseManager.ExistingInstance;
            if (pm == null || pm == _hooked)
                return;

            _hooked = pm;
            pm.onGamePaused.AddListener(ClearPausablesIfOnline);
            pm.onGameResumed.AddListener(ClearPausablesIfOnline);
        }

        private static void ClearPausablesIfOnline()
        {
            if (InstanceFinder.NetworkManager == null)
                return;
            if (!InstanceFinder.IsServerStarted && !InstanceFinder.IsClientStarted)
                return;

            if (_pausablesField?.GetValue(_hooked) is List<IPausable> list && list.Count > 0)
            {
                Debug.Log($"[MultiplayerPauseGate] Network session active: suppressing {list.Count} IPausable pause/resume notifications.");
                list.Clear();
            }
        }
    }
}
