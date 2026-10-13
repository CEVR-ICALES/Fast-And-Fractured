using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer.Progression
{
    /// <summary>
    /// The XP a match gave, on the results screen: "+N XP", the level bar filling up from where
    /// it was to where it is now (crossing levels with a LEVEL UP), and how far the next reward is.
    /// Built by code, so it can sit on any panel with a vertical layout.
    /// </summary>
    public sealed class XpGainBar : MonoBehaviour
    {
        private static readonly Color Gain = new Color(0.49f, 0.88f, 0.54f, 1f);
        private static readonly Color Accent = new Color(1f, 0.78f, 0.35f, 1f);
        private static readonly Color Muted = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color BarBack = new Color(1f, 1f, 1f, 0.12f);

        private const float START_DELAY = 0.8f;      // the screen fades in first
        private const float FILL_TIME = 1.8f;        // the whole gain, however many levels it crosses
        private const float LEVEL_UP_PAUSE = 0.35f;  // the full bar holds a moment before it empties

        private PlayerProgression.Award _award;
        private TextMeshProUGUI _gain, _level, _numbers, _next, _unlocked;
        private RectTransform _fill;
        private Image _fillImage;
        private bool _leveledUp;

        public static XpGainBar Create(Transform parent, PlayerProgression.Award award, TMP_FontAsset font)
        {
            var go = new GameObject("XpGain", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var bar = go.AddComponent<XpGainBar>();
            bar._award = award;
            bar.Build(font);
            return bar;
        }

        private void Build(TMP_FontAsset font)
        {
            var column = gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(12, 14, 6, 8);
            column.spacing = 4f;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            var top = Row(transform, 40f);
            _gain = Label(top, $"+{_award.Xp} XP", font, 32f, Gain, TextAlignmentOptions.MidlineLeft);
            _level = Label(top, "", font, 28f, Color.white, TextAlignmentOptions.MidlineRight);

            var back = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            back.transform.SetParent(transform, false);
            back.GetComponent<Image>().color = BarBack;
            back.GetComponent<Image>().raycastTarget = false;
            back.AddComponent<LayoutElement>().preferredHeight = 18f;
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(back.transform, false);
            _fill = (RectTransform)fill.transform;
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = new Vector2(0f, 1f);
            _fill.offsetMin = _fill.offsetMax = Vector2.zero;
            _fillImage = fill.GetComponent<Image>();
            _fillImage.color = Gain;
            _fillImage.raycastTarget = false;

            var bottom = Row(transform, 30f);
            _numbers = Label(bottom, "", font, 22f, Muted, TextAlignmentOptions.MidlineLeft);
            _next = Label(bottom, "", font, 22f, Muted, TextAlignmentOptions.MidlineRight);

            _unlocked = Label(transform, "", font, 22f, Accent, TextAlignmentOptions.MidlineLeft);
            _unlocked.gameObject.SetActive(false);

            Show(_award.TotalBefore);
        }

        // Start, not OnEnable: Create sets the award after AddComponent, and the animation should
        // run when the results screen is first shown.
        private void Start() => StartCoroutine(Animate());

        private IEnumerator Animate()
        {
            Show(_award.TotalBefore);
            _gain.text = "+0 XP";
            for (float t = 0f; t < START_DELAY; t += Time.unscaledDeltaTime) yield return null;

            int shownLevel = ProgressionMath.LevelForTotalXp(_award.TotalBefore);
            for (float t = 0f; t < FILL_TIME; t += Time.unscaledDeltaTime)
            {
                float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / FILL_TIME), 3f);
                int total = Mathf.RoundToInt(Mathf.Lerp(_award.TotalBefore, _award.TotalAfter, eased));
                int level = ProgressionMath.LevelForTotalXp(total);
                if (level > shownLevel)
                {
                    // The bar reaches the end, says so, and starts again from empty.
                    Fill(1f);
                    _level.text = "<color=#FFC759>LEVEL UP!</color>";
                    StartCoroutine(Pulse(_level.transform));
                    for (float p = 0f; p < LEVEL_UP_PAUSE; p += Time.unscaledDeltaTime) yield return null;
                    t += LEVEL_UP_PAUSE * 0.5f;   // the pause eats a little of the fill, not all
                    shownLevel = level;
                    _leveledUp = true;
                }
                Show(total);
                _gain.text = $"+{total - _award.TotalBefore} XP";
                yield return null;
            }
            Show(_award.TotalAfter);
            _gain.text = $"+{_award.Xp} XP";

            if (_award.NewRewards != null && _award.NewRewards.Count > 0)
            {
                _unlocked.text = "Unlocked: " + string.Join(", ", _award.NewRewards.ConvertAll(r => r.displayName));
                _unlocked.gameObject.SetActive(true);
                StartCoroutine(Pulse(_unlocked.transform));
            }
        }

        private void Show(int total)
        {
            int level = ProgressionMath.LevelForTotalXp(total);
            bool max = level >= ProgressionMath.MAX_LEVEL;
            int into = total - ProgressionMath.TotalXpForLevel(level);
            int needed = ProgressionMath.XpToNextLevel(level);
            Fill(max ? 1f : into / (float)needed);

            _level.text = _leveledUp ? $"<color=#FFC759>LEVEL {level}</color>" : $"LEVEL {level}";
            _numbers.text = max ? "Max level" : $"{into} / {needed} XP";
            _next.text = NextRewardText(total);
        }

        private void Fill(float amount) => _fill.anchorMax = new Vector2(Mathf.Clamp01(amount), 1f);

        /// <summary>"Next reward: Party Hat (level 3) · 120 XP to go".</summary>
        public static string NextRewardText(int totalXp)
        {
            var reward = NextReward(ProgressionRewards.Load().rewards, ProgressionMath.LevelForTotalXp(totalXp));
            if (reward == null) return "Every reward unlocked";
            int toGo = Mathf.Max(0, ProgressionMath.TotalXpForLevel(reward.level) - totalXp);
            return $"Next: {reward.displayName} (level {reward.level}) · {toGo} XP to go";
        }

        /// <summary>The first reward above <paramref name="level"/>, or null when none is left.</summary>
        public static ProgressionReward NextReward(IEnumerable<ProgressionReward> rewards, int level)
        {
            ProgressionReward next = null;
            if (rewards == null) return null;
            foreach (var reward in rewards)
                if (reward != null && reward.level > level && (next == null || reward.level < next.level)) next = reward;
            return next;
        }

        private static IEnumerator Pulse(Transform target)
        {
            const float time = 0.35f;
            for (float t = 0f; t < time && target != null; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Sin(Mathf.Clamp01(t / time) * Mathf.PI);
                target.localScale = Vector3.one * (1f + 0.18f * k);
                yield return null;
            }
            if (target != null) target.localScale = Vector3.one;
        }

        private static Transform Row(Transform parent, float height)
        {
            var go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            go.AddComponent<LayoutElement>().preferredHeight = height;
            return go.transform;
        }

        private static TextMeshProUGUI Label(Transform parent, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            go.AddComponent<LayoutElement>().flexibleWidth = 1f;
            return tmp;
        }
    }
}
