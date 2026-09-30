using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Lightweight DontDestroyOnLoad toast that surfaces multiplayer connection events
    /// (host left, you got kicked, network drop) above whatever scene you're on. Built in
    /// code so it has zero scene-wiring dependency — survives scene loads cleanly.
    ///
    /// Usage:
    ///     ConnectionLostNotifier.Show("Host disconnected — returning to lobby.");
    ///
    /// Visual: full-width semi-transparent bar near the top of the screen, fades out
    /// after the given duration. Stacked calls reset the timer + replace the text.
    /// </summary>
    public class ConnectionLostNotifier : MonoBehaviour
    {
        private const float DEFAULT_DURATION = 4f;
        private const float FADE_DURATION = 0.4f;

        private static ConnectionLostNotifier _instance;

        private CanvasGroup _group;
        private TMP_Text _label;
        private float _hideAt;
        private bool _visible;

        public static void Show(string message, float durationSeconds = DEFAULT_DURATION)
        {
            EnsureInstance();
            _instance.DisplayInternal(message, durationSeconds);
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;

            var rootGO = new GameObject("[ConnectionLostNotifier]");
            DontDestroyOnLoad(rootGO);

            // Canvas
            var canvas = rootGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32700; // very on top, above WinLoseScreen / loading fade
            rootGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            rootGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            rootGO.AddComponent<GraphicRaycaster>();

            var cg = rootGO.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;

            // Background bar
            var barGO = new GameObject("Bar");
            barGO.transform.SetParent(rootGO.transform, false);
            var barRT = barGO.AddComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0f, 1f);
            barRT.anchorMax = new Vector2(1f, 1f);
            barRT.pivot = new Vector2(0.5f, 1f);
            barRT.anchoredPosition = new Vector2(0f, -50f);
            barRT.sizeDelta = new Vector2(0f, 90f);
            var bg = barGO.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);

            // Text
            var txtGO = new GameObject("Label");
            txtGO.transform.SetParent(barGO.transform, false);
            var txtRT = txtGO.AddComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = new Vector2(40f, 5f);
            txtRT.offsetMax = new Vector2(-40f, -5f);
            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            txt.fontSize = 36;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = new Color(1f, 0.85f, 0.4f, 1f);
            txt.text = string.Empty;

            _instance = rootGO.AddComponent<ConnectionLostNotifier>();
            _instance._group = cg;
            _instance._label = txt;
        }

        private void DisplayInternal(string message, float duration)
        {
            _label.text = message;
            _hideAt = Time.unscaledTime + duration;
            _visible = true;
        }

        private void Update()
        {
            if (!_visible)
            {
                if (_group.alpha > 0f)
                    _group.alpha = Mathf.Max(0f, _group.alpha - Time.unscaledDeltaTime / FADE_DURATION);
                return;
            }

            // Fade in until visible
            if (_group.alpha < 1f)
                _group.alpha = Mathf.Min(1f, _group.alpha + Time.unscaledDeltaTime / FADE_DURATION);

            if (Time.unscaledTime >= _hideAt)
                _visible = false;
        }
    }
}
