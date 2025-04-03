using Enums;
using UnityEngine;

namespace FastAndFractured
{
    public class MenuScreen : MonoBehaviour
    {
        public ScreensType screenType;
        [SerializeField] private CanvasGroup canvasGroup;
        // MORE FUTURE VARIABLES
        // [SerializeField] protected bool isLocked = false;
        // [SerializeField] protected bool hasFade;
        // [SerializeField] protected float fadeDuration;

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

