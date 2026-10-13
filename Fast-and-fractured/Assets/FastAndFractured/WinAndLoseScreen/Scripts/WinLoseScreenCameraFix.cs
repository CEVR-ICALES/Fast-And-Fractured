using Enums;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured
{
    /// <summary>
    /// The menu canvas draws through "Main Camera" (depth -1), but the win/lose cutscene brings
    /// its own camera (depth 0, e.g. CameraPepeVictory) that stays on after the cutscene ends and
    /// is drawn on top: the whole WIN_LOSE screen was invisible behind the final shot, in
    /// singleplayer and multiplayer alike. While that screen shows, the canvas draws through the
    /// top camera instead, over the final shot; it goes back when the screen closes.
    ///
    /// Added at runtime to the WIN_LOSE screen of the main menu; no scene edits.
    /// </summary>
    public sealed class WinLoseScreenCameraFix : MonoBehaviour
    {
        private Canvas _rootCanvas;
        private Camera _originalCamera;
        private float _originalPlaneDistance;

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

        private void LateUpdate()
        {
            if (_rootCanvas == null)
            {
                var canvas = GetComponentInParent<Canvas>(true);
                _rootCanvas = canvas != null ? canvas.rootCanvas : null;
                if (_rootCanvas == null || _rootCanvas.renderMode != RenderMode.ScreenSpaceCamera) { enabled = false; return; }
                _originalCamera = _rootCanvas.worldCamera;
                _originalPlaneDistance = _rootCanvas.planeDistance;
            }
            Camera top = null;
            foreach (var cam in Camera.allCameras)
                if (cam.targetTexture == null && (top == null || cam.depth > top.depth)) top = cam;
            if (top != null && _rootCanvas.worldCamera != top)
            {
                _rootCanvas.worldCamera = top;
                _rootCanvas.planeDistance = Mathf.Max(top.nearClipPlane + 0.05f, 0.5f);
            }
        }

        private void OnDisable()
        {
            if (_rootCanvas == null || _originalCamera == null) return;
            _rootCanvas.worldCamera = _originalCamera;
            _rootCanvas.planeDistance = _originalPlaneDistance;
        }
    }
}
