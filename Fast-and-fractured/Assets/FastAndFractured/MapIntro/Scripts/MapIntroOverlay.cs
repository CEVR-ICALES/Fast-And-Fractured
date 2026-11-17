using Assets.SimpleLocalization.Scripts;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured
{
    public class MapIntroOverlay : MonoBehaviour
    {
        [Header("Letterbox")]
        [SerializeField] private RectTransform letterboxTop;
        [SerializeField] private RectTransform letterboxBottom;
        [Tooltip("Height of each cinema bar as a fraction of the screen height.")]
        [Range(0f, 0.3f)][SerializeField] private float letterboxHeight = 0.11f;

        [Header("Title card")]
        [SerializeField] private CanvasGroup titleGroup;
        [SerializeField] private RectTransform titlePanel;
        [SerializeField] private RectTransform titleStripe;
        [SerializeField] private TMP_Text titleText;
        [Tooltip("Parent of the map name that slides; its resting anchored position must be (0, 0).")]
        [SerializeField] private RectTransform titleSlide;
        [Tooltip("Distance (px) the map name slides while it comes in.")]
        [SerializeField] private float titleEnterSlide = 140f;
        [Tooltip("Distance (px) the map name slides while it goes away.")]
        [SerializeField] private float titleLeaveSlide = 90f;

        [Header("Other")]
        [SerializeField] private CanvasGroup skipHint;
        [Tooltip("Black screen used to hide the cut when the intro is skipped.")]
        [SerializeField] private Image fade;

        private bool _skippable;

        private void Awake()
        {
            SetLetterbox(0f);
            SetTitle(0f, false);
        }

        public void SetMapNameKey(string key)
        {
            var localized = titleText.GetComponent<LocalizedText>();
            if (localized == null) localized = titleText.gameObject.AddComponent<LocalizedText>();
            localized.LocalizationKey = key;
            localized.Localize();
        }

        public void SetSkippable(bool skippable) => _skippable = skippable;

        public void SetLetterbox(float weight)
        {
            float height = letterboxHeight * EaseInOutCubic(weight);
            letterboxTop.anchorMin = new Vector2(0f, 1f - height);
            letterboxBottom.anchorMax = new Vector2(1f, height);
            if (skipHint != null) skipHint.alpha = _skippable ? EaseInOutCubic(weight) : 0f;
        }

        public void SetTitle(float weight, bool leaving)
        {
            titleGroup.alpha = leaving ? weight : Mathf.Clamp01(weight * 2f);
            float enter = leaving ? 1f : EaseOutCubic(weight);
            float leave = leaving ? EaseInCubic(1f - weight) : 0f;
            titlePanel.localScale = new Vector3(enter, 1f, 1f);
            titleStripe.localScale = new Vector3(leaving ? 1f - leave : EaseOutCubic(Mathf.InverseLerp(0.3f, 1f, weight)), 1f, 1f);
            titleSlide.anchoredPosition = Vector2.right * (leave * titleLeaveSlide - (1f - enter) * titleEnterSlide);
        }

        public Tween FadeTo(float alpha, float seconds)
        {
            fade.DOKill();
            return fade.DOFade(alpha, seconds).SetEase(Ease.InOutSine).SetLink(gameObject);
        }

        private void OnDestroy() => fade.DOKill();

        private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp01(x), 3f);

        private static float EaseInCubic(float x) => Mathf.Pow(Mathf.Clamp01(x), 3f);

        private static float EaseInOutCubic(float x)
        {
            x = Mathf.Clamp01(x);
            return x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
        }
    }
}
