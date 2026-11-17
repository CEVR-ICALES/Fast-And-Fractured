using TMPro;
using UnityEngine;

namespace FastAndFractured
{
    [RequireComponent(typeof(TMP_Text))]
    public class DamagePercentText : MonoBehaviour
    {
        private const float SNAP_EPSILON = 0.0005f;
        private const float FULL_HIT = 0.12f;
        private const float MIN_HIT_STRENGTH = 0.35f;

        [SerializeField] private Gradient damageColors = DefaultColors();
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField] private Color healColor = new Color(0.55f, 1f, 0.55f);
        [SerializeField, Range(0f, 1f)] private float percentSignSize = 0.55f;
        [SerializeField] private float countSharpness = 10f;
        [SerializeField] private float punchScale = 0.45f;
        [SerializeField] private float punchSeconds = 0.35f;
        [SerializeField] private float flashSeconds = 0.15f;
        [SerializeField] private float shakePixels = 12f;
        [SerializeField] private float shakeSeconds = 0.3f;
        [SerializeField, Range(0f, 1f)] private float dangerFrom = 0.75f;
        [SerializeField] private float dangerPulseScale = 0.08f;
        [SerializeField] private float dangerPulseSpeed = 9f;
        [SerializeField] private float dangerTremblePixels = 1.5f;

        private TMP_Text _text;
        private RectTransform _rect;
        private Vector2 _basePosition;
        private Vector3 _baseScale;
        private float _target;
        private float _shown;
        private int _shownPercent = -1;
        private float _hitAge = float.MaxValue;
        private float _hitStrength;
        private float _healAge = float.MaxValue;

        [SerializeField] private float overflowDarkestAt = 2.5f;
        [SerializeField] private Color overflowColor = new Color(0.45f, 0f, 0.04f);
        [SerializeField] private float maxDangerBoost = 2f;

        public float Damage => _target;

        public static int PercentOf(float damage)
        {
            // Any damage at all shows at least 1%, and 100% only when the endurance is gone.
            if (damage <= 0f) return 0;
            if (damage >= 1f) return Mathf.Max(100, Mathf.CeilToInt(damage * 100f - SNAP_EPSILON));
            return Mathf.Clamp(Mathf.CeilToInt(damage * 100f - SNAP_EPSILON), 1, 99);
        }

        public static string Format(int percent, float signSize) =>
            percent + "<size=" + Mathf.RoundToInt(signSize * 100f) + "%>%</size>";

        public Color ColorFor(float damage)
        {
            if (damage <= 1f) return damageColors.Evaluate(Mathf.Max(0f, damage));
            float past = overflowDarkestAt > 1f ? Mathf.Clamp01((damage - 1f) / (overflowDarkestAt - 1f)) : 1f;
            return Color.Lerp(damageColors.Evaluate(1f), overflowColor, past);
        }

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            _rect = (RectTransform)transform;
            _basePosition = _rect.anchoredPosition;
            _baseScale = _rect.localScale;
            Render();
        }

        private void OnDisable()
        {
            if (_rect == null) return;
            _rect.anchoredPosition = _basePosition;
            _rect.localScale = _baseScale;
        }

        public void SetDamage(float damage)
        {
            damage = Mathf.Max(0f, damage);
            float delta = damage - _target;
            _target = damage;
            if (delta > 0f)
            {
                _hitAge = 0f;
                _hitStrength = Mathf.Max(MIN_HIT_STRENGTH, Mathf.Clamp01(delta / FULL_HIT));
            }
            else if (delta < 0f)
            {
                _healAge = 0f;
            }
        }

        public void SnapTo(float damage01)
        {
            _target = _shown = Mathf.Max(0f, damage01);
            _hitAge = _healAge = float.MaxValue;
            Render();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _hitAge += dt;
            _healAge += dt;

            _shown = Mathf.Abs(_target - _shown) < SNAP_EPSILON
                ? _target
                : Mathf.Lerp(_shown, _target, 1f - Mathf.Exp(-countSharpness * dt));
            Render();
        }

        private void Render()
        {
            if (_text == null) return;

            int percent = PercentOf(_shown);
            if (percent != _shownPercent)
            {
                _shownPercent = percent;
                _text.text = Format(percent, percentSignSize);
            }

            float hit = _hitAge < punchSeconds ? 1f - _hitAge / punchSeconds : 0f;
            float flash = _hitAge < flashSeconds ? 1f - _hitAge / flashSeconds : 0f;
            float heal = _healAge < punchSeconds ? 1f - _healAge / punchSeconds : 0f;

            Color color = ColorFor(_shown);
            color = Color.Lerp(color, healColor, heal);
            _text.color = Color.Lerp(color, hitFlashColor, flash * _hitStrength);

            float danger = _shown >= dangerFrom && dangerFrom < 1f ? Mathf.Min(maxDangerBoost, (_shown - dangerFrom) / (1f - dangerFrom)) : 0f;
            float time = Time.unscaledTime;
            float pulse = danger * dangerPulseScale * (0.5f + 0.5f * Mathf.Sin(time * dangerPulseSpeed));
            float punch = hit * hit * punchScale * _hitStrength;
            _rect.localScale = _baseScale * (1f + punch + pulse);

            float shake = _hitAge < shakeSeconds ? (1f - _hitAge / shakeSeconds) * shakePixels * _hitStrength : 0f;
            float tremble = danger * dangerTremblePixels;
            _rect.anchoredPosition = _basePosition + Random.insideUnitCircle * (shake + tremble);
        }

        private static Gradient DefaultColors()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1f, 0.93f, 0.45f), 0.25f),
                    new GradientColorKey(new Color(1f, 0.6f, 0.15f), 0.5f),
                    new GradientColorKey(new Color(1f, 0.25f, 0.15f), 0.75f),
                    new GradientColorKey(new Color(0.85f, 0.05f, 0.08f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }
    }
}
