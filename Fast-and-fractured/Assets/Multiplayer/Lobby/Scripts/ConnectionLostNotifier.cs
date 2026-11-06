using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Code-built DontDestroyOnLoad toast for connection events (host left, kicked, network drop):
    /// <c>ConnectionLostNotifier.Show("...")</c>. A new call replaces the text and restarts the timer.
    /// </summary>
    public class ConnectionLostNotifier : MonoBehaviour
    {
        private const float DEFAULT_DURATION = 4f;
        private const float FADE_DURATION = 0.4f;
        // Above the win/lose screen and the loading fade.
        private const int SORTING_ORDER = 32700;
        private const float FONT_SIZE = 36f;
        private static readonly Vector2 REFERENCE_RESOLUTION = new Vector2(1920, 1080);
        private static readonly Vector2 TEXT_PADDING = new Vector2(40f, 5f);
        private static readonly Color BAR_COLOR = new Color(0f, 0f, 0f, 0.75f);
        private static readonly Color TEXT_COLOR = new Color(1f, 0.85f, 0.4f, 1f);

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

            var canvas = rootGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SORTING_ORDER;
            rootGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            rootGO.GetComponent<CanvasScaler>().referenceResolution = REFERENCE_RESOLUTION;
            rootGO.AddComponent<GraphicRaycaster>();

            var cg = rootGO.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;

            var barGO = new GameObject("Bar");
            barGO.transform.SetParent(rootGO.transform, false);
            var barRT = barGO.AddComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0f, 1f);
            barRT.anchorMax = new Vector2(1f, 1f);
            barRT.pivot = new Vector2(0.5f, 1f);
            barRT.anchoredPosition = new Vector2(0f, -50f);
            barRT.sizeDelta = new Vector2(0f, 90f);
            var bg = barGO.AddComponent<Image>();
            bg.color = BAR_COLOR;

            var txtGO = new GameObject("Label");
            txtGO.transform.SetParent(barGO.transform, false);
            var txtRT = txtGO.AddComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = TEXT_PADDING;
            txtRT.offsetMax = -TEXT_PADDING;
            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            txt.fontSize = FONT_SIZE;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = TEXT_COLOR;
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

            if (_group.alpha < 1f)
                _group.alpha = Mathf.Min(1f, _group.alpha + Time.unscaledDeltaTime / FADE_DURATION);

            if (Time.unscaledTime >= _hideAt)
                _visible = false;
        }
    }
}
