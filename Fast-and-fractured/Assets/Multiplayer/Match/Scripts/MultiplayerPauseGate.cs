using System.Collections.Generic;
using System.Reflection;
using Enums;
using FishNet;
using FishNet.Object;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Utilities;
using Utilities.Managers.PauseSystem;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// The pause menu in a multiplayer match: the match keeps running (IPausables are not paused), the menu is the
    /// local player's (MainMenuManager can register the remote player's hidden screens), and the local driving and
    /// shooting inputs are blocked while it is open. Singleplayer is untouched.
    /// </summary>
    public static class MultiplayerPauseGate
    {
        private static FieldInfo _pausablesField;
        private static readonly FieldInfo AllInputsBlockedField = typeof(PlayerInputController)
            .GetField("_isAllMechanicsInputsBlocked", BindingFlags.NonPublic | BindingFlags.Instance);
        private static PauseManager _hooked;
        private static bool _inputBlockedByUs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsForDomainReloadDisabled()
        {
            _hooked = null;
            _inputBlockedByUs = false;
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

            var runner = new GameObject("[MultiplayerPauseGate]");
            Object.DontDestroyOnLoad(runner);
            runner.hideFlags = HideFlags.HideInHierarchy;
            runner.AddComponent<ScreenBinder>();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _inputBlockedByUs = false;
            TryHook();
        }

        private static void TryHook()
        {
            var pm = PauseManager.ExistingInstance;
            if (pm == null || pm == _hooked)
                return;
            // An auxiliary PauseManager (created during teardown) has no serialized events: wait for the scene's.
            if (pm.onGamePaused == null || pm.onGameResumed == null)
                return;

            _hooked = pm;
            pm.onGamePaused.AddListener(OnPaused);
            pm.onGameResumed.AddListener(OnResumed);
        }

        private static bool Online =>
            InstanceFinder.NetworkManager != null && (InstanceFinder.IsServerStarted || InstanceFinder.IsClientStarted);

        private static void OnPaused()
        {
            if (!Online) return;
            ClearPausables();

            // PauseGame may have opened the other player's screen: show the local one.
            if (BindLocalScreens() && MainMenuManager.HasInstance)
            {
                var menu = MainMenuManager.Instance;
                if (menu.CurrentScreen != null && !IsOnLocalRoot(menu, menu.CurrentScreen))
                    menu.TransitionBetweenScreens(ScreensType.PAUSE, -1);
            }

            if (PlayerInputController.HasInstance)
            {
                var input = PlayerInputController.Instance;
                bool alreadyBlocked = AllInputsBlockedField != null && (bool)AllInputsBlockedField.GetValue(input);
                if (!alreadyBlocked)
                {
                    input.BlockInput(InputBlockTypes.ALL_MECHANICS);
                    _inputBlockedByUs = true;
                }
            }
        }

        private static void OnResumed()
        {
            if (!Online) return;
            ClearPausables();
            if (_inputBlockedByUs && PlayerInputController.HasInstance)
                PlayerInputController.Instance.EnableInput(InputBlockTypes.ALL_MECHANICS);
            _inputBlockedByUs = false;
        }

        private static void ClearPausables()
        {
            if (_pausablesField?.GetValue(_hooked) is List<IPausable> list && list.Count > 0)
            {
                Debug.Log($"[MultiplayerPauseGate] Network session active: suppressing {list.Count} IPausable pause/resume notifications.");
                list.Clear();
            }
        }

        /// <summary>Points MainMenuManager's screen table at its own player root's screens; false outside a match.</summary>
        public static bool BindLocalScreens()
        {
            if (!MainMenuManager.HasInstance) return false;
            var menu = MainMenuManager.Instance;
            var root = menu.GetComponentInParent<NetworkObject>();
            if (root == null) return false;

            var table = menu.menuScreens;
            var screens = root.GetComponentsInChildren<MenuScreen>(true);
            foreach (var screen in screens)
                if (!table.TryGetValue(screen.screenType, out var current) || current != screen)
                    table[screen.screenType] = screen;

            // Back on settings/how-to-play leads to MAIN_MENU, which a match lacks: point it at PAUSE. The pause
            // screen's own Back is dropped: Escape already toggles the pause, and pressing Back too resumed it the
            // same frame it opened.
            if (table.TryGetValue(ScreensType.PAUSE, out var pause) && pause.backButton != null)
                pause.backButton = null;

            if (table.ContainsKey(ScreensType.PAUSE))
                foreach (var screen in screens)
                {
                    var back = screen.backButton != null ? screen.backButton.GetComponent<MenuTransitionButton>() : null;
                    if (back != null && !table.ContainsKey(back.screenToOpen))
                        back.screenToOpen = ScreensType.PAUSE;
                }

            if (pause != null) RouteExitToRoom(pause);

            // Settings > Multiplayer in the pause menu too.
            foreach (var screen in screens)
                if (screen.TryGetComponent(out SettingsMenuBehaviour settings) && settings.GetComponent<MultiplayerSettingsTab>() == null)
                    settings.gameObject.AddComponent<MultiplayerSettingsTab>();
            return true;
        }

        /// <summary>The pause menu's home button leads back to the room (MultiplayerRoomReturn), not the main menu.</summary>
        private static void RouteExitToRoom(MenuScreen pause)
        {
            foreach (var button in pause.GetComponentsInChildren<Button>(true))
            {
                if (button.GetComponent<ExitToRoomMarker>() != null) continue;
                var click = button.onClick;
                bool exits = false;
                for (int i = 0; i < click.GetPersistentEventCount(); i++)
                {
                    if (click.GetPersistentMethodName(i) != nameof(MainMenuManager.LoadScene)) continue;
                    click.SetPersistentListenerState(i, UnityEventCallState.Off);
                    exits = true;
                }
                if (!exits) continue;
                button.gameObject.AddComponent<ExitToRoomMarker>();
                click.AddListener(MultiplayerRoomReturn.LeaveMatchToRoom);
            }
        }

        private sealed class ExitToRoomMarker : MonoBehaviour { }

        private static bool IsOnLocalRoot(MainMenuManager menu, MenuScreen screen)
        {
            var root = menu.GetComponentInParent<NetworkObject>();
            return root == null || screen.transform.IsChildOf(root.transform);
        }

        /// <summary>Keeps the table right before Escape is pressed (players spawn after scene load).</summary>
        private sealed class ScreenBinder : MonoBehaviour
        {
            private const float BIND_INTERVAL = 0.5f;

            private float _next;

            private void Update()
            {
                if (Time.unscaledTime < _next) return;
                _next = Time.unscaledTime + BIND_INTERVAL;
                if (Online) BindLocalScreens();
            }
        }
    }
}
