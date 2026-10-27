using System.Reflection;
using Enums;
using FastAndFractured;
using FishNet;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// After a multiplayer match the win/lose screen's retry button (and Esc) lead back to the room, change character
    /// through the room's pick, and home out of the room to the main menu (the room migrates to another player).
    /// Armed before the MainMenu loads; it disarms after one use or a load from any other screen.
    /// </summary>
    public static class MultiplayerPostMatchFlow
    {
        private const float SCREEN_TRANSITION_SECONDS = 0.25f;

        private static bool _armed;

        private static readonly FieldInfo SpawnedCutsceneField = typeof(WinLoseScreenBehaviour)
            .GetField("_spawnedObject", BindingFlags.NonPublic | BindingFlags.Instance);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsForDomainReloadDisabled()
        {
            _armed = false;
            if (LoadingManager.SceneLoadOverride == HandleSceneLoad)
                LoadingManager.SceneLoadOverride = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>Call when a multiplayer match ends, before loading the MainMenu scene.</summary>
        public static void ArmAfterMultiplayerMatch(bool wasHost)
        {
            _armed = true;
            LoadingManager.SceneLoadOverride = HandleSceneLoad;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Debug.Log($"[MultiplayerPostMatchFlow] Armed (wasHost={wasHost}). The win/lose screen will lead back to the room.");
        }

        public static void Disarm()
        {
            _armed = false;
            if (LoadingManager.SceneLoadOverride == HandleSceneLoad)
                LoadingManager.SceneLoadOverride = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private static bool HandleSceneLoad(int sceneBuildIndex)
        {
            if (!_armed)
                return false;

            var menu = MainMenuManager.HasInstance ? MainMenuManager.Instance : null;
            var currentScreenType = menu != null && menu.CurrentScreen != null
                ? menu.CurrentScreen.screenType
                : (ScreensType?)null;

            if (currentScreenType != ScreensType.WIN_LOSE)
            {
                Disarm();
                return false;
            }

            BackToRoom();
            return true;
        }

        /// <summary>The menu button(s) of the win/lose screen lead to the room as well.</summary>
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.buildIndex != LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var screen in root.GetComponentsInChildren<MenuScreen>(true))
                    if (screen.screenType == ScreensType.WIN_LOSE)
                        RouteMenuButtons(screen);
        }

        /// <summary>
        /// Retry leads to the room, change character to the room's pick and home out of the room. Their camera switches
        /// are kept: they restore the menu cameras after the cutscene.
        /// </summary>
        private static void RouteMenuButtons(MenuScreen screen)
        {
            foreach (var button in screen.GetComponentsInChildren<Button>(true))
            {
                var click = button.onClick;
                var transition = button.GetComponent<MenuTransitionButton>();
                bool picksCharacter = transition != null && transition.screenToOpen == ScreensType.CHARACTER_SELECTION;
                bool routed = false;
                for (int i = 0; i < click.GetPersistentEventCount(); i++)
                {
                    string method = click.GetPersistentMethodName(i);
                    var target = click.GetPersistentTarget(i);
                    bool goToMenu = method == nameof(WinLoseScreenBehaviour.GoToMainMenu);
                    bool openMenu = method == nameof(MenuTransitionButton.OpenScreen) && !picksCharacter;
                    bool loadingScreen = method == nameof(GameObject.SetActive) && target is GameObject go &&
                                         go.GetComponentInChildren<LoadingManager>(true) != null;
                    if (!goToMenu && !openMenu && !loadingScreen) continue;
                    click.SetPersistentListenerState(i, UnityEventCallState.Off);
                    routed = true;
                }
                if (!routed) continue;
                if (picksCharacter) click.AddListener(PickCharacterForNextMatch);
                else if (transition != null && transition.screenToOpen == ScreensType.MAIN_MENU) click.AddListener(LeaveRoomToMainMenu);
                else
                {
                    click.AddListener(BackToRoom);
                    screen.backButton = button;
                }
            }
        }

        /// <summary>Home: out of the room. If this player hosted it, the room carries on under another (RoomMigration).</summary>
        private static async void LeaveRoomToMainMenu()
        {
            if (!_armed) return;
            Disarm();
            HideFinalCutscene();
            HostDisconnectWatchdog.NotifyIntentionalLeave();
            try { await Abstractions.LobbyServiceLocator.Transport.LeaveLobbyAsync(); }
            catch (System.Exception e) { Debug.LogException(e); }
            if (MainMenuManager.HasInstance)
                MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.MAIN_MENU, SCREEN_TRANSITION_SECONDS);
        }

        /// <summary>Character selection through the room's flow: picking a map returns to the room.</summary>
        private static void PickCharacterForNextMatch()
        {
            if (!_armed) return;
            HideFinalCutscene();
            if (!InstanceFinder.IsClientStarted)
            {
                BackToRoom();
                return;
            }
            Disarm();
            Lobby.MultiplayerMenuRouter.BeginLobbyFlow();
        }

        private static void BackToRoom()
        {
            if (!_armed) return;
            Disarm();
            HideFinalCutscene();
            var target = InstanceFinder.IsClientStarted ? ScreensType.LOBBY_ROOM : ScreensType.MAIN_MENU;
            Debug.Log($"[MultiplayerPostMatchFlow] Leaving the win/lose screen → {target}.");
            if (MainMenuManager.HasInstance)
                MainMenuManager.Instance.TransitionBetweenScreens(target, SCREEN_TRANSITION_SECONDS);
        }

        /// <summary>The character's win/lose cutscene stays in front of the menu cameras otherwise.</summary>
        private static void HideFinalCutscene()
        {
            if (!WinLoseScreenBehaviour.HasInstance || SpawnedCutsceneField == null) return;
            if (SpawnedCutsceneField.GetValue(WinLoseScreenBehaviour.Instance) is GameObject cutscene && cutscene != null)
                cutscene.SetActive(false);
        }
    }
}
