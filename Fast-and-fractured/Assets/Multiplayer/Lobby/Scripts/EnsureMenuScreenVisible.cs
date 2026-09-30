using FastAndFractured;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Defensive helper: forces the screen's CanvasGroup to fully visible state shortly after
    /// the GameObject becomes active. Worked around an issue where the MainMenuManager fade-in
    /// timer would leave the new screen at alpha 0 — the screen was "active" and its camera
    /// switched, but the UI was completely invisible.
    ///
    /// Attached to every multiplayer MenuScreen by LobbySetupWizard's CreateScreenGO. Singleplayer
    /// behavior is untouched.
    /// </summary>
    [RequireComponent(typeof(MenuScreen))]
    public sealed class EnsureMenuScreenVisible : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;

        // Two-frame settle window: lets MainMenuManager's fadeIn fire at least once before we
        // forcibly snap. If the fade is healthy and reaches 1 naturally, the snap is a no-op.
        private const int SETTLE_FRAMES = 8;
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

        // EVERY frame while enabled — not just first 8 — clamp alpha to 1. The MainMenuManager
        // fade callback was setting it to 0 every Update tick, so a one-shot snap got overridden.
        private void LateUpdate()
        {
            if (_canvasGroup == null) return;
            if (_canvasGroup.alpha < 0.99f) ForceVisible();
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
