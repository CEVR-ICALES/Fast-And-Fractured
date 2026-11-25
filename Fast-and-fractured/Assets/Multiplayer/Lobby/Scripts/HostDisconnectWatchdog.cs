using Assets.SimpleLocalization.Scripts;
using FishNet;
using FishNet.Transporting;
using UnityEngine;
using Enums;
using FastAndFractured;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Sends the player back to the multiplayer hub when the client loses the host unexpectedly during a match,
    /// unless host or room migration takes over.
    /// </summary>
    public class HostDisconnectWatchdog : MonoBehaviour
    {
        private const float SCREEN_TRANSITION_SECONDS = 0.5f;

        private static HostDisconnectWatchdog _instance;
        private static bool _intentionalLeave;
        private static bool _inMatch;
        private static bool _connected;

        private bool _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[HostDisconnectWatchdog]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<HostDisconnectWatchdog>();
        }

        /// <summary>Set before a deliberate teardown so the next client stop is not reported; resets after one stop.</summary>
        public static void NotifyIntentionalLeave()
        {
            _intentionalLeave = true;
        }

        /// <summary>Called by the lobby manager when the match transitions to active play.</summary>
        public static void NotifyMatchStarted()
        {
            _inMatch = true;
        }

        /// <summary>Called by the level controller manager when the match ends normally.</summary>
        public static void NotifyMatchEnded()
        {
            _inMatch = false;
        }

        // A join that never got through is not a lost host: the join screen reports it and the player stays there.
        public static bool ShouldReport(bool wasIntentional, bool wasConnected) => !wasIntentional && wasConnected;

        private void Update()
        {
            // The NetworkManager may not exist until a scene with one loads.
            if (_hooked) return;
            var nm = InstanceFinder.NetworkManager;
            if (nm == null) return;
            nm.ClientManager.OnClientConnectionState += OnClientConnectionState;
            _connected = nm.ClientManager.Started;
            _hooked = true;
        }

        private void OnDestroy()
        {
            if (!_hooked) return;
            var nm = InstanceFinder.NetworkManager;
            if (nm != null)
                nm.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                _connected = true;
                // A leave announced before this connection existed is stale: it would swallow the next real host loss.
                _intentionalLeave = false;
            }
            if (args.ConnectionState != LocalConnectionState.Stopped) return;

            bool wasConnected = _connected;
            _connected = false;
            bool wasIntentional = _intentionalLeave;
            // A player waiting in the room while the others play is not in that match.
            bool wasInMatch = _inMatch &&
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex != LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX;
            _intentionalLeave = false;
            _inMatch = false;

            if (!ShouldReport(wasIntentional, wasConnected))
                return;

            // A migration is reconnecting: its failed attempts stop the client too.
            if (HostMigration.HostMigrationNetwork.Migrating || HostMigration.RoomMigration.Migrating)
                return;

            // Host migration (off by default): the match continues on another player instead.
            if (wasInMatch && HostMigration.HostMigrationNetwork.TryHandleHostLoss())
                return;

            // Room migration: the room carries on under another player.
            if (HostMigration.RoomMigration.TryHandleHostLoss())
                return;

            Debug.LogWarning("[HostDisconnectWatchdog] Client connection stopped unexpectedly" +
                             (wasInMatch ? " during active match." : " (was in lobby).") +
                             " Returning to multiplayer hub.");

            ConnectionLostNotifier.Show(wasInMatch
                ? LocalizationManager.Localize("MP.HostDisconnected")
                : LocalizationManager.Localize("MP.ConnectionLost"));

            // From a map the main menu scene must load first; TransitionBetweenScreens only works inside it.
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool inMenuScene = activeScene.buildIndex == LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX;

            if (inMenuScene && MainMenuManager.HasInstance)
            {
                MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.MULTIPLAYER_HUB, SCREEN_TRANSITION_SECONDS);
            }
            else
            {
                MultiplayerSceneTeardown.LoadMainMenu();
            }
        }
    }
}
