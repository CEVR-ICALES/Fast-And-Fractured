using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Gamepad focus for the multiplayer screens: keeps defaultInteractable on the main action (Host, Start, Join,
    /// Ready, first entry) instead of Back, and selects it when a screen opens with a gamepad in use.
    /// </summary>
    public sealed class MultiplayerScreenFocus : MonoBehaviour
    {
        private const float VISIBLE_ALPHA = 0.5f;

        private static readonly string[] PrimaryActions =
            { "ReadyButton", "HostButton", "StartButton", "JoinButton" };

        private MenuScreen _screen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachToMultiplayerScreens();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToMultiplayerScreens();

        private static void AttachToMultiplayerScreens()
        {
            foreach (var screen in FindObjectsByType<MenuScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!screen.transform.root.name.Contains("Multiplayer")) continue;
                if (screen.GetComponent<MultiplayerScreenFocus>() == null)
                    screen.gameObject.AddComponent<MultiplayerScreenFocus>();
            }
        }

        private CanvasGroup _group;
        private bool _wasCurrent;

        private void Awake()
        {
            _screen = GetComponent<MenuScreen>();
            _group = GetComponent<CanvasGroup>();
        }

        // Screens stay active and fade through their CanvasGroup; the interactable one is current.
        private bool IsCurrent => _group != null ? _group.interactable && _group.alpha > VISIBLE_ALPHA : isActiveAndEnabled;

        private void LateUpdate()
        {
            bool current = IsCurrent;
            bool becameCurrent = current && !_wasCurrent;
            _wasCurrent = current;
            if (!current) return;
            Selectable primary = FindPrimary();
            if (primary == null) return;
            if (_screen != null) _screen.defaultInteractable = primary;   // dev's recovery uses it

            EventSystem events = EventSystem.current;
            if (events == null || !GamepadInUse()) return;
            GameObject selected = events.currentSelectedGameObject;
            bool lost = selected == null || !selected.activeInHierarchy ||
                        !selected.transform.IsChildOf(transform) ||
                        selected.TryGetComponent(out Selectable s) && !s.IsInteractable();
            if (becameCurrent || lost) events.SetSelectedGameObject(primary.gameObject);
        }

        /// <summary>The gamepad was used more recently than the keyboard and the mouse.</summary>
        private static bool GamepadInUse()
        {
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad == null) return false;
            double padTime = pad.lastUpdateTime;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            return (keyboard == null || padTime >= keyboard.lastUpdateTime)
                && (mouse == null || padTime >= mouse.lastUpdateTime);
        }

        private Selectable FindPrimary()
        {
            Selectable[] selectables = GetComponentsInChildren<Selectable>(false);
            foreach (string name in PrimaryActions)
                foreach (var candidate in selectables)
                    if (candidate.name == name && candidate.IsInteractable()) return candidate;

            // Room list / map vote: the first entry created at runtime.
            foreach (var candidate in selectables)
                if (candidate.IsInteractable() && candidate.name.Contains("(Clone)")) return candidate;

            Selectable back = _screen != null ? _screen.backButton : null;
            foreach (var candidate in selectables)
                if (candidate.IsInteractable() && candidate != back && !(candidate is TMPro.TMP_InputField))
                    return candidate;
            return back;
        }
    }
}
