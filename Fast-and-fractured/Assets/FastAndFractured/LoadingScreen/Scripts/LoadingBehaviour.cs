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
                float targetProgress = Mathf.Clamp01(_operation.progress / 0.9f);
                _visualProgress = Mathf.SmoothDamp(_visualProgress, targetProgress, ref _smoothVelocity, 0.5f);

                if (progressBarImage != null)
                {
                    progressBarImage.fillAmount = _visualProgress;
                }

                if (_operation.progress >= 0.9f && !_operation.allowSceneActivation)
                {
                    if (progressBarImage != null && progressBarImage.fillAmount >= 0.9999f)
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