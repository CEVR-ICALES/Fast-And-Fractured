using System;
using TMPro;
using UnityEngine;

namespace FastAndFractured
{
    public sealed class FpsCounter : MonoBehaviour
    {
        public const string PREF_KEY = "ShowFps";
        private const float SAMPLE_SECONDS = 0.5f;
        private const int SORTING_ORDER = 32000;
        // Small and tight to the corner: menu titles and Back buttons start just below.
        private const float FONT_SIZE = 18f;
        private static readonly Vector2 MARGIN = new Vector2(8f, -2f);
        private static readonly Color GOOD = new Color(0.45f, 1f, 0.5f);
        private static readonly Color FAIR = new Color(1f, 0.85f, 0.3f);
        private static readonly Color POOR = new Color(1f, 0.35f, 0.3f);
        private const float GOOD_FPS = 55f;
        private const float FAIR_FPS = 30f;

        public static event Action<bool> Changed;

        public static bool Visible
        {
            get => PlayerPrefs.GetInt(PREF_KEY, 0) == 1;
            set
            {
                if (value == Visible) return;
                PlayerPrefs.SetInt(PREF_KEY, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke(value);
            }
        }

        public static Color ColorFor(float fps) => fps >= GOOD_FPS ? GOOD : fps >= FAIR_FPS ? FAIR : POOR;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[FpsCounter]");
            DontDestroyOnLoad(go);
            go.AddComponent<FpsCounter>();
        }

        private Canvas _canvas;
        private TMP_Text _text;
        private int _frames;
        private float _elapsed;
        private int _shownFps = -1;

        private void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SORTING_ORDER;

            var textGo = new GameObject("Fps", typeof(RectTransform));
            var rect = (RectTransform)textGo.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = MARGIN;
            rect.sizeDelta = new Vector2(200f, 40f);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.fontSize = FONT_SIZE;
            text.fontStyle = FontStyles.Bold;
            text.outlineWidth = 0.25f;
            text.outlineColor = Color.black;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.TopLeft;
            _text = text;

            Apply(Visible);
            Changed += Apply;
        }

        private void OnDestroy() => Changed -= Apply;

        private void Apply(bool visible)
        {
            _canvas.enabled = visible;
            _frames = 0;
            _elapsed = 0f;
        }

        private void Update()
        {
            if (!_canvas.enabled) return;
            _frames++;
            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < SAMPLE_SECONDS) return;

            float fps = _frames / _elapsed;
            _frames = 0;
            _elapsed = 0f;
            int rounded = Mathf.RoundToInt(fps);
            if (rounded == _shownFps) return;
            _shownFps = rounded;
            _text.text = rounded + " FPS";
            _text.color = ColorFor(fps);
        }
    }
}
