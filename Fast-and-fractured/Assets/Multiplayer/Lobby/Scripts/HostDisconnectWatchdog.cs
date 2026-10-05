using FishNet;
using FishNet.Transporting;
using UnityEngine;
using Enums;
using FastAndFractured;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// "Soft" host-disconnect handler. Detects when the client loses connection to the
    /// server unexpectedly (host crashed, host clicked-out, network drop) and returns
    /// the player to the multiplayer hub with a clear notification.
    ///
    /// This is the placeholder until FishNet ships real host migration (Pro roadmap)
    /// or we integrate a third-party solution. It does NOT preserve match state; the
    /// game session ends. Goal is to replace the current failure mode (frozen player
    /// + premature WinLoseScreen) with a clean "Host disconnected" return-to-lobby.
    ///
    /// Wiring:
    /// - Bootstrapped at startup via [RuntimeInitializeOnLoadMethod] — single
    ///   DontDestroyOnLoad instance.
    /// - Subscribes to ClientManager.OnClientConnectionState.
    /// - Match-active state is set by NetworkedLobbyManager.OnGameStarting and cleared
    ///   by NetworkedLevelControllerManager.EndMatchOnServer (via EndGameRpc) or by
    ///   NotifyIntentionalLeave when the local player triggers a clean leave.
    /// - On Stopped while _inMatch == true and intentional==false: transition to the
    ///   multiplayer hub.
    /// </summary>
    public class HostDisconnectWatchdog : MonoBehaviour
    {
        private static HostDisconnectWatchdog _instance;
        private static bool _intentionalLeave;
        private static bool _inMatch;

        private bool _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[HostDisconnectWatchdog]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<HostDisconnectWatchdog>();
        }

        /// <summary>
        /// Set right before a deliberate teardown (back button, normal match end) so the
        /// subsequent client-stopped event doesn't show the "host disconnected" UI.
        /// Auto-resets after one disconnect cycle.
        /// </summary>
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

        private void Update()
        {
            // FishNet's NetworkManager may not exist until the first scene with one loads;
            // poll lightly until we can attach.
            if (_hooked) return;
            var nm = InstanceFinder.NetworkManager;
            if (nm == null) return;
            nm.ClientManager.OnClientConnectionState += OnClientConnectionState;
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
            if (args.ConnectionState != LocalConnectionState.Stopped) return;

            bool wasIntentional = _intentionalLeave;
            bool wasInMatch = _inMatch;
            _intentionalLeave = false;
            _inMatch = false;

            if (wasIntentional)
                return;

            // Host migration (off by default): the match continues on another player instead.
            if (wasInMatch && HostMigration.HostMigrationNetwork.TryHandleHostLoss())
                return;

            // Not intentional — either host crashed, network died, or host stopped server.
            // Either way, returning to the hub is the safe default. Match cleanup happens
            // implicitly: scene gets unloaded by FishNet, all NetworkObjects despawn.
            Debug.LogWarning("[HostDisconnectWatchdog] Client connection stopped unexpectedly" +
                             (wasInMatch ? " during active match." : " (was in lobby).") +
                             " Returning to multiplayer hub.");

            ConnectionLostNotifier.Show(wasInMatch
                ? "Host disconnected — match ended."
                : "Connection lost — returning to the multiplayer hub.");

            // TransitionBetweenScreens is a UI-only transition within the menu scene; if we're
            // sitting in a map scene (active match), it cannot move us out — the player just
            // stays stuck on the dead map. We must load the main menu SCENE first; then on the
            // next frame the lobby flow can kick in. If we're already in the menu scene, just
            // do the UI transition.
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool inMenuScene = activeScene.buildIndex == LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX;

            if (inMenuScene && MainMenuManager.HasInstance)
            {
                MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.MULTIPLAYER_HUB, 0.5f);
            }
            else
            {
                // Force-load the main menu scene. The menu's normal startup flow will land
                // the player on its default screen; the hub transition can be issued from
                // there if desired.
                MultiplayerSceneTeardown.LoadMainMenu();
            }
        }
    }
}
