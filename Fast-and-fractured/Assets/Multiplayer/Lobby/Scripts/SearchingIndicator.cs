using TMPro;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Lobby
{
    public sealed class SearchingIndicator : MonoBehaviour
    {
        private const int BAR_COUNT = 3;
        private const float BAR_WIDTH = 34f;
        private const float BAR_HEIGHT = 10f;
        private const float BAR_SPACING = 12f;
        private const float BAR_SKEW = 8f;
        private const float GAP_BELOW_TEXT = 6f;
        private const float CYCLE_SECONDS = 1.1f;
        private const float BAR_DELAY = 0.18f;
        private const float DIM_ALPHA = 0.12f;
        private const float LIT_HEIGHT_SCALE = 1.8f;

        private RectTransform[] _bars;
        private SkewedImage[] _images;
        private Color _color;

        public static SearchingIndicator Create(TMP_Text label)
        {
            var go = new GameObject(nameof(SearchingIndicator), typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(label.rectTransform, false);
            rect.anchorMin = rect.anchorMax = label.rectTransform.pivot;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(BAR_COUNT * BAR_WIDTH + (BAR_COUNT - 1) * BAR_SPACING + BAR_SKEW, BAR_HEIGHT * LIT_HEIGHT_SCALE);

            var indicator = go.AddComponent<SearchingIndicator>();
            indicator._color = label.color;
            indicator._bars = new RectTransform[BAR_COUNT];
            indicator._images = new SkewedImage[BAR_COUNT];
            for (int i = 0; i < BAR_COUNT; i++)
            {
                var bar = new GameObject("Bar" + i, typeof(RectTransform));
                var barRect = (RectTransform)bar.transform;
                barRect.SetParent(rect, false);
                barRect.anchorMin = barRect.anchorMax = new Vector2(0f, 0.5f);
                barRect.pivot = new Vector2(0f, 0.5f);
                barRect.sizeDelta = new Vector2(BAR_WIDTH, BAR_HEIGHT);
                barRect.anchoredPosition = new Vector2(i * (BAR_WIDTH + BAR_SPACING), 0f);
                var image = bar.AddComponent<SkewedImage>();
                image.Skew = BAR_SKEW;
                image.raycastTarget = false;
                indicator._bars[i] = barRect;
                indicator._images[i] = image;
            }
            indicator.FollowText(label);
            go.SetActive(false);
            return indicator;
        }

        // Sits under the rendered text, not the label's rect, which is much taller than one line.
        public void FollowText(TMP_Text label)
        {
            label.ForceMeshUpdate();
            Bounds text = label.textBounds;
            float textBottom = text.size.y > 0f ? text.min.y : -label.fontSize * 0.5f;
            ((RectTransform)transform).anchoredPosition = new Vector2(text.size.x > 0f ? text.center.x : 0f, textBottom - GAP_BELOW_TEXT);
        }

        private void Update()
        {
            float time = Time.unscaledTime / CYCLE_SECONDS;
            for (int i = 0; i < _bars.Length; i++)
            {
                float phase = Mathf.Repeat(time - i * BAR_DELAY, 1f);
                float lit = Mathf.Clamp01(1f - Mathf.Abs(phase - 0.3f) * 3f);
                lit = lit * lit * (3f - 2f * lit);
                _images[i].color = new Color(_color.r, _color.g, _color.b, _color.a * Mathf.Lerp(DIM_ALPHA, 1f, lit));
                _bars[i].localScale = new Vector3(1f, Mathf.Lerp(1f, LIT_HEIGHT_SCALE, lit), 1f);
            }
        }
    }
}
