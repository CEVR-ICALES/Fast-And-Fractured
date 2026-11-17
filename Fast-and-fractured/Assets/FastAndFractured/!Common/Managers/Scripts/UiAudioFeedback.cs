using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FastAndFractured
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Selectable))]
    public sealed class UiAudioFeedback : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        private static readonly System.Random _variation = new System.Random();
        private Selectable _selectable;
        private UiAudioSettings _settings;
        private float _lastFeedback = -1f;

        public static void AttachTo(Transform root)
        {
            foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
                if (!selectable.TryGetComponent<UiAudioFeedback>(out _))
                    selectable.gameObject.AddComponent<UiAudioFeedback>();
        }

        #region Lifecycle
        private void Awake()
        {
            _settings = AudioPresentationProfile.Load().Ui;
            _selectable = GetComponent<Selectable>();
            if (_selectable is Button button) button.onClick.AddListener(Click);
            else if (_selectable is Toggle toggle) toggle.onValueChanged.AddListener(ValueChanged);
            else if (_selectable is Slider slider) slider.onValueChanged.AddListener(SliderChanged);
        }

        private void OnDestroy()
        {
            if (_selectable is Button button) button.onClick.RemoveListener(Click);
            else if (_selectable is Toggle toggle) toggle.onValueChanged.RemoveListener(ValueChanged);
            else if (_selectable is Slider slider) slider.onValueChanged.RemoveListener(SliderChanged);
        }
        #endregion

        #region Feedback
        // Earlier button listeners can already have disabled or destroyed their screen.
        private void Click()
        {
            GameplayAudio.Play(GameAudioCues.UI_EventsButtonClickEvent, Vector3.zero, _settings.ClickVolume, NextPitch());
        }

        private void ValueChanged(bool value)
        {
            if (isActiveAndEnabled && _selectable.IsInteractable()) Click();
        }

        private void SliderChanged(float value) => Hover();

        private void Hover()
        {
            if (!_selectable.IsInteractable() || Time.unscaledTime - _lastFeedback < _settings.HoverCooldownSeconds) return;
            _lastFeedback = Time.unscaledTime;
            GameplayAudio.Play(GameAudioCues.UI_EventsButtonHoverEvent, transform.position, _settings.HoverVolume, NextPitch());
        }

        private static float NextPitch() => AudioPresentationProfile.Load().Ui.CalculatePitch(_variation.NextDouble());

        public void OnPointerEnter(PointerEventData eventData) => Hover();
        public void OnSelect(BaseEventData eventData) => Hover();
        #endregion
    }
}
