using Enums;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer
{
    public static class MultiplayerAvailability
    {
        public const string CONFIG_RESOURCES_PATH = "MultiplayerAvailabilityConfig";
        private static readonly Color BLOCKED_TINT = new Color(1f, 0.42f, 0.42f, 1f);

        private static MultiplayerAvailabilityConfig _config;
        private static bool _loaded;

        public static bool Enabled
        {
            get
            {
                if (!_loaded)
                {
                    _loaded = true;
                    _config = Resources.Load<MultiplayerAvailabilityConfig>(CONFIG_RESOURCES_PATH);
                }
                return _config == null || _config.MultiplayerEnabled;
            }
        }

        public static void Reload() => _loaded = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _loaded = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Enabled || !scene.IsValid()) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var transition in root.GetComponentsInChildren<MenuTransitionButton>(true))
                    if (transition.screenToOpen == ScreensType.MULTIPLAYER_HUB)
                        Block(transition);
        }

        private static void Block(MenuTransitionButton transition)
        {
            if (!transition.TryGetComponent(out Button button)) return;
            var colors = button.colors;
            colors.highlightedColor = BLOCKED_TINT;
            colors.pressedColor = BLOCKED_TINT;
            colors.selectedColor = BLOCKED_TINT;
            button.colors = colors;

            var click = button.onClick;
            for (int i = 0; i < click.GetPersistentEventCount(); i++)
                if (click.GetPersistentTarget(i) == transition && click.GetPersistentMethodName(i) == nameof(MenuTransitionButton.OpenScreen))
                    click.SetPersistentListenerState(i, UnityEventCallState.Off);
        }
    }
}
