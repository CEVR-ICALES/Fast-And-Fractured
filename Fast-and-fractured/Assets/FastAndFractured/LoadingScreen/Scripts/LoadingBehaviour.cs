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
        [SerializeField] private Slider progressBar; // Opcional: barra de progreso
        private ITimer timerReference;
        private const float IMAGE_TIMER_DURATION = 5f;
        private const float SCENE_LOAD_DELAY = 0.1f;

        void OnEnable()
        {
            SetRandomBackgroundImage();
        }
        void OnDisable()
        {
            if (timerReference != null)
            {
                timerReference.StopTimer();
                timerReference = null;
            }
        }

        public void LoadScene(int sceneBuildIndex)
        {
            TimerSystem.Instance.CreateTimer(SCENE_LOAD_DELAY, onTimerDecreaseComplete: () =>
            {
                StartCoroutine(LoadSceneAsync(sceneBuildIndex));
            });
        }

        private IEnumerator LoadSceneAsync(int sceneBuildIndex)
        {
            // Inicia la carga de la escena en segundo plano
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneBuildIndex);
            operation.allowSceneActivation = false; // Espera antes de activar la escena

            while (!operation.isDone)
            {
                float progress = Mathf.Clamp01(operation.progress / 0.9f); // Normaliza el progreso (0 a 1)
                Debug.Log($"Loading progress: {progress}");
                if (progressBar != null)
                {
                    progressBar.value = progress;
                }
                if (operation.progress >= 0.9f)
                {

                    operation.allowSceneActivation = true; // Activa la escena cuando esté lista
                }
                yield return null;
            }
        }

        private void SetRandomBackgroundImage()
        {
            if (backgroundSprites.Length > 0 && backgroundImage != null)
            {
                int randomIndex = Random.Range(0, backgroundSprites.Length);
                backgroundImage.sprite = backgroundSprites[randomIndex];
                timerReference = TimerSystem.Instance.CreateTimer(IMAGE_TIMER_DURATION, onTimerDecreaseComplete: () =>
                {
                    SetRandomBackgroundImage();
                });
            }
        }
    }
}