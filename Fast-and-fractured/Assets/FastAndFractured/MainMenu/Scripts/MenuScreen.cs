using Enums;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured
{
    public class MenuScreen : MonoBehaviour
    {
        public ScreensType screenType;
        [SerializeField] private CanvasGroup canvasGroup;
        public Button backButton;
        public Button defaultButton;

        public void SetAlpha(float alpha)
        {
            canvasGroup.alpha = alpha;
        }

        public void SetInteractable(bool interactable)
        {
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
        }
    }
}

