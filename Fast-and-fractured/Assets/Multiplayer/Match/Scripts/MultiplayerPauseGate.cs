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
    /// The pause / options menu in a multiplayer match: it opens, it works, and the match keeps
    /// running.
    ///
    /// 1. Nothing gameplay-side stops. The server keeps simulating, so pausing rigidbodies /
    ///    inputs / timers locally only desyncs this peer (and a paused host would fight every
    ///    client). PauseManager already leaves Time.timeScale alone, but it still notifies every
    ///    registered IPausable. It invokes onGamePaused/onGameResumed BEFORE iterating its
    ///    pausable list, so while a network session is active this empties the private list
    ///    right before the loop runs.
    ///
    /// 2. The menu is the LOCAL player's. The pause, settings and how-to-play screens live on
    ///    each player root, and MainMenuManager.RegisterScreens collects them with a scene-wide
    ///    FindObjectsOfType keyed by screen type - with two players the remote player's screens
    ///    can win, and those sit under a disabled PlayerCanvas: Escape "opened" an invisible
    ///    pause menu (and Settings an invisible options screen). The screens of the root that
    ///    owns MainMenuManager.Instance are put back in its table, and a menu already opened on
    ///    the wrong root is reopened on the right one. Back from settings leads to the pause menu.
    ///
    /// 3. Clicking in the menu must not drive or shoot: the local driving / shooting inputs are
    ///    blocked while it is open and given back on resume (only if they were not already
    ///    blocked by the game, e.g. during the countdown).
    ///
    /// Singleplayer sessions (no active connection) are untouched.
    /// </summary>
    public static class MultiplayerPauseGate
    {
        private static FieldInfo _pausablesField;
        private static readonly FieldInfo AllInputsBlockedField = typeof(PlayerInputController)
            .GetField("_isAllMechanicsInputsBlocked", BindingFlags.NonPublic | BindingFlags.Instance);
        private static PauseManager _hooked;
        private static bool _inputBlockedByUs;

        // Domain reload is disabled in Enter Play Mode options; statics survive between plays,
        // so both state and the sceneLoaded subscription must be reset or they accumulate.
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
            // An auxiliary PauseManager created on the fly (AbstractSingleton, while a scene is
            // being torn down) has no serialized events; wait for the scene's real one.
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

            // PauseGame already transitioned before raising this event; if it used the other
            // player's screen, show the local one instead.
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

        // ── Local menu screens ─────────────────────────────────────────────────────────
        /// <summary>
        /// Points the local MainMenuManager's screen table at the screens on its own player root.
        /// Returns true when the manager lives on a networked player (a match), false otherwise.
        /// </summary>
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

            // The settings / how-to-play Back buttons on the player root lead to MAIN_MENU (the
            // singleplayer maps override that to PAUSE in the scene); in a match that screen
            // does not exist, so Back - and Escape, which presses it - did nothing.
            // Escape reaches the menu twice: PauseManager toggles the pause, and dev's
            // MenuInputsController (one per player root, so twice more) presses the current
            // screen's Back. The pause screen's Back is its Resume button, so whenever the
            // toggle ran first the menu opened and was resumed in the same frame - on the host it
            // never appeared. Escape already resumes through the toggle, so the pause screen
            // needs no Back of its own; settings / how-to-play keep theirs (a second press during
            // their fade-out is ignored by MainMenuManager).
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
            return true;
        }

        /// <summary>
        /// The pause menu's home button loads the main menu scene (MainMenuManager.LoadScene):
        /// online that left the player in the menu, still connected, outside the room. It leads
        /// back to the room instead (MultiplayerRoomReturn).
        /// </summary>
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
            private float _next;

            private void Update()
            {
                if (Time.unscaledTime < _next) return;
                _next = Time.unscaledTime + 0.5f;
                if (Online) BindLocalScreens();
            }
        }
    }
}
