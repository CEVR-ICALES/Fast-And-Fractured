using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Utilities;

namespace FastAndFractured
{
    public class LoadingManager : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite[] backgroundSprites;
        [SerializeField] private Image progressBarImage;
        private ITimer _timerReference;
        private const float IMAGE_TIMER_DURATION = 5f;
        private const float SCENE_LOAD_DELAY = 0.1f;
        private AsyncOperation _operation;
        private bool _isLoadingScene = false;
        private float _visualProgress = 0f;
        private float _smoothVelocity = 0f;
        private const float SMOOTH_TIME = 0.5f;
        private const float OPERATION_PROGRESS_TARGET = 0.9f;

        void OnEnable()
        {
            SetRandomBackgroundImage();
        }
        void OnDisable()
        {
            if (_timerReference != null)
            {
                _timerReference.StopTimer();
                _timerReference = null;
            }
        }

        
        void Update()
        {
            if (_isLoadingScene && _operation != null)
            {
                float targetProgress = Mathf.Clamp01(_operation.progress / OPERATION_PROGRESS_TARGET);
                _visualProgress = Mathf.SmoothDamp(_visualProgress, targetProgress, ref _smoothVelocity, SMOOTH_TIME);

                if (progressBarImage != null)
                {
                    progressBarImage.fillAmount = _visualProgress;
                }

                if (_operation.progress >= OPERATION_PROGRESS_TARGET && !_operation.allowSceneActivation)
                {
                    if (progressBarImage != null && Mathf.Approximately(progressBarImage.fillAmount, 1f))
                    {
                        _operation.allowSceneActivation = true;
                        _isLoadingScene = false;
                    }
                }
            }
        }

        public void LoadScene(int sceneBuildIndex)
        {
            TimerSystem.Instance.CreateTimer(SCENE_LOAD_DELAY, onTimerDecreaseComplete: () =>
            {
                StartSceneLoading(sceneBuildIndex);
            });
        }

        private void StartSceneLoading(int sceneBuildIndex)
        {
            _operation = SceneManager.LoadSceneAsync(sceneBuildIndex);
            _operation.allowSceneActivation = false;
            _isLoadingScene = true;
        }

        private void SetRandomBackgroundImage()
        {
            if (backgroundSprites.Length > 0 && backgroundImage != null)
            {
                int randomIndex = Random.Range(0, backgroundSprites.Length);
                backgroundImage.sprite = backgroundSprites[randomIndex];
                _timerReference = TimerSystem.Instance.CreateTimer(IMAGE_TIMER_DURATION, onTimerDecreaseComplete: () =>
                {
                    SetRandomBackgroundImage();
                });
            }
        }
    }
}