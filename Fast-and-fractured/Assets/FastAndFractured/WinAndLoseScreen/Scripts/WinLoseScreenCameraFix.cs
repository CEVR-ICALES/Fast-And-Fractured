using Enums;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured
{
    /// <summary>
    /// The win/lose cutscene camera renders on top of the menu camera, hiding the menu canvas. While WIN_LOSE shows, the
    /// canvas is drawn as an overlay: over every camera and out of the cutscene's post-processing, which darkened it.
    /// </summary>
    public sealed class WinLoseScreenCameraFix : MonoBehaviour
    {
        private MenuScreen _screen;
        private Canvas _rootCanvas;
        private RenderMode _originalRenderMode;
        private bool _overlaid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => SceneManager.sceneLoaded -= OnSceneLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid()) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var screen in root.GetComponentsInChildren<MenuScreen>(true))
                    if (screen.screenType == ScreensType.WIN_LOSE && screen.GetComponent<WinLoseScreenCameraFix>() == null)
                        screen.gameObject.AddComponent<WinLoseScreenCameraFix>();
        }

        private void Awake()
        {
            _screen = GetComponent<MenuScreen>();
            var canvas = GetComponentInParent<Canvas>(true);
            _rootCanvas = canvas != null ? canvas.rootCanvas : null;
        }

        private void LateUpdate()
        {
            bool showing = MainMenuManager.HasInstance && MainMenuManager.Instance.CurrentScreen == _screen;
            if (showing != _overlaid) SetOverlay(showing);
        }

        private void OnDisable() => SetOverlay(false);

        private void SetOverlay(bool overlay)
        {
            if (_rootCanvas == null || overlay == _overlaid) return;
            if (overlay) _originalRenderMode = _rootCanvas.renderMode;
            _rootCanvas.renderMode = overlay ? RenderMode.ScreenSpaceOverlay : _originalRenderMode;
            _overlaid = overlay;
        }
    }
}
