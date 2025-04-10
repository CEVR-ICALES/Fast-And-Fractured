using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Utilities;
namespace FastAndFractured
{
    public class LoadingBehaviour : MonoBehaviour
    {
        [SerializeField] private GameObject loadingScreenUI;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite[] backgroundSprites;
        [SerializeField] private float imageTimerDuration = 5f;
        void Start()
        {
            loadingScreenUI.SetActive(true);
            SetRandomBackgroundImage();
        }
        void Update()
        {
            // TODO: call HideLoadingScreen() when game is ready(level controller task)
        }
        public void HideLoadingScreen()
        {
            loadingScreenUI.SetActive(false);
        }
        public void SetRandomBackgroundImage()
        {
            if (backgroundSprites.Length > 0)
            {
                int randomIndex = Random.Range(0, backgroundSprites.Length);
                backgroundImage.sprite = backgroundSprites[randomIndex];
                TimerSystem.Instance.CreateTimer(imageTimerDuration, onTimerDecreaseComplete: () =>
                {
                    SetRandomBackgroundImage();
                });
            }
        }
    }
}
