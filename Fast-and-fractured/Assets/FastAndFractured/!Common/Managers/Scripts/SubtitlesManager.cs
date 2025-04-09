using Assets.SimpleLocalization.Scripts;
using Enums;
using TMPro;
using UnityEngine;
using Utilities;

namespace FastAndFractured
{
    public struct SubtitleData
    {
        public string TextLocalizeCode;
        public float Duration;
        public string SpeakerName;
    }
    public class SubtitlesManager : AbstractSingleton<SubtitlesManager>
    {
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private TextMeshProUGUI speakerText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeDuration = 0.8f;

        private bool _subtitlesEnabled;
        private SubtitleData _currentSubtitle;
        private ITimer _displayTimer;
        private ITimer _fadeTimer;

        private const string SUBTITLES_KEY = "Substitles";
        protected override void Awake()
        {
            base.Awake();
            ToggleSubtitles(PlayerPrefs.GetInt(SUBTITLES_KEY, 0) == 1);
            canvasGroup.alpha = 0f;
        }

        public void ToggleSubtitles(bool enabled)
        {
            _subtitlesEnabled = enabled;
            if (!_subtitlesEnabled)
            {
                HideImmediate();
            }
            else
            {
                canvasGroup.gameObject.SetActive(true);
            }
        }

        public void ShowSubtitle(SubtitleData data)
        {
            if (!_subtitlesEnabled) return;

            CleanupTimers();
            _currentSubtitle = data;
            UpdateDisplay();
            _fadeTimer = TimerSystem.Instance.CreateTimer(fadeDuration, TimerDirection.INCREASE,
                onTimerIncreaseComplete: () => StartSubtitleDisplay(),
                onTimerIncreaseUpdate: (progress) => canvasGroup.alpha = progress); ;
        }

        private void StartSubtitleDisplay()
        {
            // main display duration
            canvasGroup.alpha = 1f;
            _displayTimer = TimerSystem.Instance.CreateTimer(
                _currentSubtitle.Duration,
                onTimerDecreaseComplete: () => FadeOut()
            );
        }

        private void FadeOut()
        {
            _fadeTimer = TimerSystem.Instance.CreateTimer(
                fadeDuration,
                TimerDirection.DECREASE,
                onTimerDecreaseComplete: () => {
                    canvasGroup.alpha = 0f;
                },
                onTimerDecreaseUpdate: (progress) => canvasGroup.alpha = progress
            );
        }

        private void UpdateDisplay()
        {
            subtitleText.gameObject.GetComponent<LocalizedText>().LocalizationKey = _currentSubtitle.TextLocalizeCode;
            subtitleText.gameObject.GetComponent<LocalizedText>().Localize();
            if (speakerText != null)
            {
                speakerText.text = !string.IsNullOrEmpty(_currentSubtitle.SpeakerName)
                    ? _currentSubtitle.SpeakerName + ":"
                    : "";
            }
        }

        public void HideImmediate()
        {
            CleanupTimers();
            canvasGroup.alpha = 0f;
            canvasGroup.gameObject.SetActive(false);
        }

        private void CleanupTimers()
        {
            if(_displayTimer != null)
            {
                TimerSystem.Instance.StopTimer(_displayTimer.GetData().ID);
                _displayTimer = null;
            }
            if (_fadeTimer != null)
            {
                TimerSystem.Instance.StopTimer(_fadeTimer.GetData().ID);
                _fadeTimer = null;
            }   
        }
    }

}
