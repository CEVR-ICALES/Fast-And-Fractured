using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Keyboard / gamepad focus for the multiplayer menu screens.
    ///
    /// Every multiplayer MenuScreen had its Back button as default interactable, so with a gamepad
    /// the focus started on "Back" (A left the screen) and the lobby / map entries created at
    /// runtime were never focused (playtest feedback 2026-09-30). On each screen this focuses the
    /// main action instead - Host, Start, Join, Ready, or the first room / map entry - and puts the
    /// focus back when it is lost (a clicked-away mouse, an entry list rebuilt under it).
    ///
    /// Added at runtime to the screens under the MultiplayerUICanvas; no scene wiring.
    /// </summary>
    public sealed class MultiplayerScreenFocus : MonoBehaviour
    {
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
        private bool IsCurrent => _group != null ? _group.interactable && _group.alpha > 0.5f : isActiveAndEnabled;

        private void LateUpdate()
        {
            bool current = IsCurrent;
            bool becameCurrent = current && !_wasCurrent;
            _wasCurrent = current;
            if (!current) return;
            if (becameCurrent) { FocusPrimary(); return; }

            EventSystem events = EventSystem.current;
            if (events == null) return;
            GameObject selected = events.currentSelectedGameObject;
            bool lost = selected == null || !selected.activeInHierarchy ||
                        !selected.transform.IsChildOf(transform) ||
                        selected.TryGetComponent(out Selectable s) && !s.IsInteractable();
            if (lost) FocusPrimary();
        }

        private void FocusPrimary()
        {
            Selectable primary = FindPrimary();
            if (primary == null) return;
            if (_screen != null) _screen.defaultInteractable = primary;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(primary.gameObject);
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
