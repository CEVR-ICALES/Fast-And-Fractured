using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured
{
    // FindObjectsByType costs over a millisecond in a map: one shared scan when menus can appear, not a timer each.
    public static class MenuScan
    {
        public const float FALLBACK_SECONDS = 10f;
        private static readonly float[] DELAYS_AFTER_TRIGGER = { 0f, 0.5f, 1.5f, 4f };

        public static SettingsMenuBehaviour[] SettingsMenus { get; private set; } = new SettingsMenuBehaviour[0];
        public static MenuScreen[] Screens { get; private set; } = new MenuScreen[0];
        public static int Version { get; private set; }

        private static int _triggers;

        public static void RequestScan() => _triggers++;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SettingsMenus = new SettingsMenuBehaviour[0];
            Screens = new MenuScreen[0];
            Version = 0;
            _triggers = 0;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            var go = new GameObject("[MenuScan]");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<Pump>();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RequestScan();

        private static void Scan()
        {
            SettingsMenus = Object.FindObjectsByType<SettingsMenuBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Screens = Object.FindObjectsByType<MenuScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Version++;
        }

        private sealed class Pump : MonoBehaviour
        {
            private const float PLAYER_CHECK_SECONDS = 0.25f;
            private readonly ScanSchedule _schedule = new ScanSchedule(DELAYS_AFTER_TRIGGER, FALLBACK_SECONDS);
            private GameObject _localPlayer;
            private float _nextPlayerCheck;

            private void Update()
            {
                float now = Time.unscaledTime;
                if (now >= _nextPlayerCheck)
                {
                    _nextPlayerCheck = now + PLAYER_CHECK_SECONDS;
                    // The pause and settings screens of a match arrive with the player, after the scene.
                    GameObject localPlayer = LocalPlayerInMap();
                    if (localPlayer != _localPlayer)
                    {
                        _localPlayer = localPlayer;
                        RequestScan();
                    }
                }
                if (_schedule.IsDue(now, _triggers)) Scan();
            }

            // Asking a missing singleton searches the whole scene: not in the menu, where there is no level.
            private static GameObject LocalPlayerInMap()
            {
                if (SceneManager.GetActiveScene().buildIndex == LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX) return null;
                LevelController level = LevelController.ExistingInstance;
                return level != null ? level.LocalPlayer : null;
            }
        }
    }

    public sealed class ScanSchedule
    {
        private readonly float[] _delaysAfterTrigger;
        private readonly float _fallbackSeconds;
        private int _trigger = int.MinValue;
        private int _step;
        private float _triggerTime;
        private float _lastScan;

        public ScanSchedule(float[] delaysAfterTrigger, float fallbackSeconds)
        {
            _delaysAfterTrigger = delaysAfterTrigger;
            _fallbackSeconds = fallbackSeconds;
        }

        public bool IsDue(float now, int trigger)
        {
            if (trigger != _trigger)
            {
                _trigger = trigger;
                _step = 0;
                _triggerTime = now;
            }
            bool inBurst = _step < _delaysAfterTrigger.Length;
            bool due = inBurst ? now >= _triggerTime + _delaysAfterTrigger[_step] : now >= _lastScan + _fallbackSeconds;
            if (!due) return false;
            if (inBurst) _step++;
            _lastScan = now;
            return true;
        }
    }
}
