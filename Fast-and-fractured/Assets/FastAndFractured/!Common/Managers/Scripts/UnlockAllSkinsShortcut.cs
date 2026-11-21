using Enums;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FastAndFractured
{
    // For the event builds: set ENABLED to false before the real release.
    public sealed class UnlockAllSkinsShortcut : MonoBehaviour
    {
        public static readonly bool ENABLED = true;
        public const Key SHORTCUT = Key.U;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!ENABLED) return;
            var go = new GameObject("[UnlockAllSkinsShortcut]");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<UnlockAllSkinsShortcut>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[SHORTCUT].wasPressedThisFrame) return;

            MainMenuManager menu = MainMenuManager.ExistingInstance;
            MenuScreen screen = menu != null ? menu.CurrentScreen : null;
            if (!ShouldUnlock(ENABLED, screen != null, screen != null ? screen.screenType : default)) return;

            CharacterSelectorManager selector = CharacterSelectorManager.ExistingInstance;
            if (selector != null) selector.UnlockEverySkin();
        }

        public static bool ShouldUnlock(bool enabled, bool hasScreen, ScreensType screen) =>
            enabled && hasScreen && screen == ScreensType.CHARACTER_SELECTION;
    }
}
