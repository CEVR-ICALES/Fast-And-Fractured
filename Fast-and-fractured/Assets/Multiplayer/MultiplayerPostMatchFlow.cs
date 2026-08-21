using Enums;
using FastAndFractured;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Post-match routing for multiplayer. The WinLoseScreen's buttons are singleplayer-defaults:
    /// "retry" calls LoadingManager.ReplayScene() (reloads the previous map scene locally) and
    /// the menu button returns to MAIN_MENU. After a multiplayer match, retrying alone in a
    /// networked map makes no sense — the room concept must be re-formed.
    ///
    /// Armed by NetworkedLevelControllerManager.ShowWinLoseScreen right before it loads the
    /// MainMenu. While armed, the (already existing, approved) LoadingManager.SceneLoadOverride
    /// hook intercepts scene loads triggered FROM the WIN_LOSE screen only, and routes:
    ///   - ex-host   → MULTIPLAYER_HOST_SETUP (recreate the room; same settings flow)
    ///   - ex-client → MULTIPLAYER_BROWSER    (find the recreated room)
    /// Singleplayer behaviour is untouched: the override no-ops when not armed, and disarms
    /// itself after one use or as soon as a load is requested from any other screen.
    /// </summary>
    public static class MultiplayerPostMatchFlow
    {
        private static bool _armed;
        private static bool _wasHost;

        // Domain reload is disabled in Enter Play Mode options; statics survive between plays.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsForDomainReloadDisabled()
        {
            _armed = false;
            _wasHost = false;
            if (LoadingManager.SceneLoadOverride == HandleSceneLoad)
                LoadingManager.SceneLoadOverride = null;
        }

        /// <summary>Call when a multiplayer match ends, before loading the MainMenu scene.</summary>
        public static void ArmAfterMultiplayerMatch(bool wasHost)
        {
            _armed = true;
            _wasHost = wasHost;
            LoadingManager.SceneLoadOverride = HandleSceneLoad;
            Debug.Log($"[MultiplayerPostMatchFlow] Armed (wasHost={wasHost}). WinLose 'retry' will route to the multiplayer hub.");
        }

        public static void Disarm()
        {
            _armed = false;
            if (LoadingManager.SceneLoadOverride == HandleSceneLoad)
                LoadingManager.SceneLoadOverride = null;
        }

        private static bool HandleSceneLoad(int sceneBuildIndex)
        {
            if (!_armed)
                return false;

            var menu = MainMenuManager.HasInstance ? MainMenuManager.Instance : null;
            var currentScreenType = menu != null && menu.CurrentScreen != null
                ? menu.CurrentScreen.screenType
                : (ScreensType?)null;

            // Only hijack loads coming from the WIN_LOSE screen (the retry button). A load
            // requested from any other screen means the player moved on (e.g. started a normal
            // singleplayer game) — get out of the way permanently.
            if (currentScreenType != ScreensType.WIN_LOSE)
            {
                Disarm();
                return false;
            }

            Disarm();
            var target = _wasHost ? ScreensType.MULTIPLAYER_HOST_SETUP : ScreensType.MULTIPLAYER_BROWSER;
            Debug.Log($"[MultiplayerPostMatchFlow] Retry after MP match → routing to {target}.");
            menu.TransitionBetweenScreens(target, 0.25f);
            return true;
        }
    }
}
