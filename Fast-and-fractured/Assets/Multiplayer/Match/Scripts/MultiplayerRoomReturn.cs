using System.Collections;
using Enums;
using FastAndFractured.Multiplayer.Abstractions;
using FishNet;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// The room outlives the match. Every way out of a match leads back to it, still connected:
    ///  - the match ends: win/lose screen, whose buttons then lead to the room
    ///    (MultiplayerPostMatchFlow);
    ///  - a player leaves early (pause menu exit, Escape while spectating): that player goes to
    ///    the room and waits there; the match goes on without them;
    ///  - the host leaves early: the map runs on the host, so the match ends for everyone and
    ///    they all go back to the room.
    /// Leaving the room itself (its Back button) is what disconnects. Out of a match with no room
    /// left to go back to, the player lands on the main menu.
    /// </summary>
    public static class MultiplayerRoomReturn
    {
        private static Runner _runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _runner = null;

        private static bool Online => InstanceFinder.IsClientStarted || InstanceFinder.IsServerStarted;

        private static bool InMenuScene =>
            SceneManager.GetActiveScene().buildIndex == LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX;

        /// <summary>Local player leaves the match in progress for the room.</summary>
        public static void LeaveMatchToRoom()
        {
            HostDisconnectWatchdog.NotifyMatchEnded();
            var lobby = LobbyLocator.GetLobbyManager();
            if (!Online || lobby == null)
            {
                // No room to go back to (connection already gone): plain exit to the main menu.
                MultiplayerSceneTeardown.LoadMainMenu(ScreensType.MAIN_MENU);
                return;
            }

            if (InstanceFinder.IsServerStarted)
            {
                // Everyone's curtain starts now; the host's map (and with it everybody's view of
                // the match) only goes once its own curtain has shut, by then theirs too.
                MpCanary.Mark("Host left the match - ending it for everyone");
                lobby.ServerAbortMatch();
                MultiplayerSceneTeardown.LoadMainMenu(ScreensType.LOBBY_ROOM);
                return;
            }

            // Tell the server only once the curtain hides the map: leaving makes it take the
            // match's objects away from this player at once.
            MpCanary.Mark("Left the match for the room");
            SceneCurtain.Close(() =>
            {
                LobbyLocator.GetLobbyManager()?.ServerLeaveMatch();
                MultiplayerSceneTeardown.LoadMainMenu(ScreensType.LOBBY_ROOM);
            });
        }

        /// <summary>Every peer, when the host ended the match without a result.</summary>
        internal static void ReturnFromAbortedMatch()
        {
            HostDisconnectWatchdog.NotifyMatchEnded();
            if (InMenuScene)
                return;   // waiting in the room already
            if (!InstanceFinder.IsServerStarted)
                ConnectionLostNotifier.Show("The host ended the match - back to the room.");
            MultiplayerSceneTeardown.LoadMainMenu(ScreensType.LOBBY_ROOM);
        }

        /// <summary>
        /// Opens a menu screen once the freshly loaded main menu has started, skipping its intro
        /// cutscene (it plays when the menu is entered from nowhere, not on the way back).
        /// </summary>
        internal static void OpenScreenWhenMenuReady(ScreensType screen)
        {
            if (_runner == null)
            {
                var go = new GameObject("[MultiplayerRoomReturn]");
                Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<Runner>();
            }
            _runner.StopAllCoroutines();
            _runner.StartCoroutine(OpenScreen(screen));
        }

        private static IEnumerator OpenScreen(ScreensType screen)
        {
            float waited = 0f;
            while (!MainMenuManager.HasInstance && waited < 10f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            // StartMode runs from the manager's start and opens MAIN_MENU; go after it.
            for (int i = 0; i < 3; i++) yield return null;
            if (!MainMenuManager.HasInstance) yield break;

            SkipMenuIntro();
            if (screen == ScreensType.LOBBY_ROOM && !Online)
                screen = ScreensType.MAIN_MENU;   // the room closed on the way back
            MainMenuManager.Instance.TransitionBetweenScreens(screen, -1);
        }

        internal static void SkipMenuIntro()
        {
            if (!MenuSkipInitialCutscene.HasInstance) return;
            var skip = MenuSkipInitialCutscene.Instance;
            skip.AlreadySkipped = true;
            if (skip.timeLine != null)
            {
                skip.timeLine.time = skip.timeLine.duration;
                skip.timeLine.Evaluate();
            }
            if (skip.skipText != null) skip.skipText.SetActive(false);
        }

        private sealed class Runner : MonoBehaviour { }
    }
}
