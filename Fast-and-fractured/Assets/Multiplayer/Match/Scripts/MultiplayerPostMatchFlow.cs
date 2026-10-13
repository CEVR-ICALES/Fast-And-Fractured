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
    /// Post-match routing for multiplayer. The WinLoseScreen's buttons are singleplayer-defaults:
    /// "retry" calls LoadingManager.ReplayScene() (reloads the previous map scene locally) and
    /// the menu button goes to MAIN_MENU. After a multiplayer match the room is still there
    /// (the connection stays up), so both lead back to it - LOBBY_ROOM, ready for the next
    /// match. If the room closed meanwhile (the host left), they lead to the multiplayer hub.
    ///
    /// Armed by NetworkedLevelControllerManager.ShowWinLoseScreen right before it loads the
    /// MainMenu. While armed, the (already existing, approved) LoadingManager.SceneLoadOverride
    /// hook intercepts scene loads triggered FROM the WIN_LOSE screen only, and the menu
    /// button(s) of that screen are re-pointed when the menu loads. Singleplayer behaviour is
    /// untouched: the override no-ops when not armed, and disarms itself after one use or as
    /// soon as a load is requested from any other screen.
    /// </summary>
    public static class MultiplayerPostMatchFlow
    {
        private static bool _armed;

        private static readonly FieldInfo SpawnedCutsceneField = typeof(WinLoseScreenBehaviour)
            .GetField("_spawnedObject", BindingFlags.NonPublic | BindingFlags.Instance);

        // Domain reload is disabled in Enter Play Mode options; statics survive between plays.
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

            // Only hijack loads coming from the WIN_LOSE screen (the retry button). A load
            // requested from any other screen means the player moved on (e.g. started a normal
            // singleplayer game) — get out of the way permanently.
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
        /// The three buttons of the screen, wired in the menu prefab:
        ///  - home: GoToMainMenu + OpenScreen(MAIN_MENU) + menu cameras → to the room;
        ///  - change character: GoToMainMenu + OpenScreen(CHARACTER_SELECTION) + cameras → the
        ///    room's own character / map pick, which ends back in the room;
        ///  - retry: shows the loading screen, then LoadingManager.ReplayScene (caught by
        ///    HandleSceneLoad) → to the room, without the loading screen.
        /// Camera switches are kept: they put the menu cameras back after the final cutscene.
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
                MainMenuManager.Instance.TransitionBetweenScreens(target, 0.25f);
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
