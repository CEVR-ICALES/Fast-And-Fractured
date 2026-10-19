using Enums;
using UnityEngine;

namespace FastAndFractured
{
    /// <summary>
    /// The HUD storm timer in singleplayer: time until the level calls the storm, then 00:00. Online the networked
    /// storm writes it instead.
    /// </summary>
    public sealed class StormCountdownHud : MonoBehaviour
    {
        private const float CHECK_EVERY = 0.2f;

        /// <summary>Set by the multiplayer storm while it drives the timer itself.</summary>
        public static bool DrivenByNetwork;

        private string _shown;
        private bool _counting;
        private float _nextCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => DrivenByNetwork = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[StormCountdownHud]");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<StormCountdownHud>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + CHECK_EVERY;
            // ExistingInstance only: outside a level asking for Instance would create an empty one.
            var level = LevelController.ExistingInstance;
            if (DrivenByNetwork || level == null) { _shown = null; _counting = false; return; }

            float? left = level.StormSecondsLeft;
            if (left.HasValue) _counting = true;
            else if (!_counting) return;   // no countdown in this level (debug mode): leave the HUD alone

            Show(Format(left ?? 0f));
        }

        private void Show(string text)
        {
            if (text == _shown) return;
            var hud = HUDManager.ExistingInstance;
            if (hud == null) return;
            _shown = text;
            hud.UpdateUIElement(UIDynamicElementType.TIMER_TEXT, true);
            hud.UpdateUIElement(UIDynamicElementType.TIMER_TEXT, text);
        }

        public static string Format(float seconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
