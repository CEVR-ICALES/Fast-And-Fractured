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
    /// After a multiplayer match the win/lose screen's retry and menu buttons lead back to the room (or the hub if
    /// it closed). Armed before the MainMenu loads; it disarms after one use or a load from any other screen.
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

            // Only loads from the WIN_LOSE screen; any other means the player moved on.
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
        /// The screen's home, change character and retry buttons lead to the room (change character through the room's
        /// pick). Their camera switches are kept: they restore the menu cameras after the cutscene.
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
                    routed |= goToMenu;
                }
                if (!routed) continue;
                if (picksCharacter) click.AddListener(PickCharacterForNextMatch);
                else click.AddListener(BackToRoom);
            }
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
