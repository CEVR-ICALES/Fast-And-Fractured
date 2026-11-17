using FastAndFractured;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Keeps a multiplayer MenuScreen's CanvasGroup visible: MainMenuManager's fade could leave it at alpha 0.
    /// Added by LobbySetupWizard.
    /// </summary>
    [RequireComponent(typeof(MenuScreen))]
    public sealed class EnsureMenuScreenVisible : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;

        // Lets MainMenuManager's fade-in run once before snapping.
        private const int SETTLE_FRAMES = 8;
        private const float VISIBLE_ALPHA = 0.99f;
        private int _framesLeft;

        private void Reset() => _canvasGroup = GetComponent<CanvasGroup>();
        private void Awake()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            ForceVisible();
            _framesLeft = SETTLE_FRAMES;
        }

        // Every frame: the fade callback set it back to 0 after a one-shot snap.
        private void LateUpdate()
        {
            if (_canvasGroup == null) return;
            if (_canvasGroup.alpha < VISIBLE_ALPHA) ForceVisible();
            if (_framesLeft > 0) _framesLeft--;
        }

        private void ForceVisible()
        {
            if (_canvasGroup == null) return;
            _canvasGroup.alpha          = 1f;
            _canvasGroup.interactable   = true;
            _canvasGroup.blocksRaycasts = true;
        }
    }
}
