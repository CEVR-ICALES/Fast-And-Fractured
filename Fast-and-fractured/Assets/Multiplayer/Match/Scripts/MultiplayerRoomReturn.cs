using Assets.SimpleLocalization.Scripts;
using System.Collections;
using Enums;
using FastAndFractured.Multiplayer.Abstractions;
using FishNet;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// The room outlives the match: every way out of one leads back to it, still connected. A player leaving early
    /// waits there; the host leaving ends the match for everyone. Only the room's Back button disconnects.
    /// </summary>
    public static class MultiplayerRoomReturn
    {
        private const float MENU_START_TIMEOUT = 10f;

        private static Runner _runner;

        /// <summary>This player walked out of the match in progress: its end (results, win/lose) is no longer theirs.</summary>
        internal static bool LeftMatch { get; private set; }

        /// <summary>Set while the match is decided but its result not shown yet (the end reveal): the way out is the result.</summary>
        internal static System.Action ShowDecidedResult;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _runner = null;
            LeftMatch = false;
            ShowDecidedResult = null;
        }

        internal static void NotifyMatchStarted()
        {
            LeftMatch = false;
            ShowDecidedResult = null;
        }

        private static bool Online => InstanceFinder.IsClientStarted || InstanceFinder.IsServerStarted;

        private static bool InMenuScene =>
            SceneManager.GetActiveScene().buildIndex == LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX;

        public static void LeaveMatchToRoom()
        {
            if (ShowDecidedResult != null)
            {
                var showResult = ShowDecidedResult;
                ShowDecidedResult = null;
                showResult();
                return;
            }

            LeftMatch = true;
            HostDisconnectWatchdog.NotifyMatchEnded();
            var lobby = LobbyLocator.GetLobbyManager();
            if (!Online || lobby == null)
            {
                MultiplayerSceneTeardown.LoadMainMenu(ScreensType.MAIN_MENU);
                return;
            }

            if (InstanceFinder.IsServerStarted)
            {
                // The host's map only goes once its curtain has shut, by then everyone's too.
                MpCanary.Mark("Host left the match - ending it for everyone");
                lobby.ServerAbortMatch();
                MultiplayerSceneTeardown.LoadMainMenu(ScreensType.LOBBY_ROOM);
                return;
            }

            // Tell the server once the curtain hides the map: it removes the match's objects at once.
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
                ConnectionLostNotifier.Show(LocalizationManager.Localize("MP.HostEndedMatch"));
            MultiplayerSceneTeardown.LoadMainMenu(ScreensType.LOBBY_ROOM);
        }

        /// <summary>Opens a menu screen once the main menu has started, skipping its intro cutscene.</summary>
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
            while (!MainMenuManager.HasInstance && waited < MENU_START_TIMEOUT)
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
