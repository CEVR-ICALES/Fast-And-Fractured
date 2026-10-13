using UnityEngine;

namespace FastAndFractured.GameTelemetry
{
    /// <summary>Which menu screens are opened, and from where (e.g. how many pick Singleplayer vs Multiplayer).</summary>
    public sealed class MenuCollector : MonoBehaviour
    {
        private const float CHECK_EVERY = 0.5f;
        private string _screen;
        private float _nextCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[Telemetry] Menu");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<MenuCollector>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + CHECK_EVERY;
            // HasInstance only: asking for Instance outside the menu would create an empty manager.
            if (!MainMenuManager.HasInstance) { _screen = null; return; }
            var current = MainMenuManager.Instance.CurrentScreen;
            if (current == null) return;
            string screen = current.screenType.ToString();
            if (screen == _screen) return;
            Telemetry.Track(TelemetryKeys.MENU_SCREEN, ("screen", screen), ("from", _screen ?? "start"));
            _screen = screen;
        }
    }
}
