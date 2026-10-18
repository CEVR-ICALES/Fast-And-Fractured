using Enums;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured
{
    /// <summary>
    /// The win/lose cutscene camera stays on top of the menu camera after the cutscene: while WIN_LOSE shows, the
    /// menu canvas draws through the top camera instead.
    /// </summary>
    public sealed class WinLoseScreenCameraFix : MonoBehaviour
    {
        private const float NEAR_CLIP_MARGIN = 0.05f;
        private const float MIN_PLANE_DISTANCE = 0.5f;

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
                _rootCanvas.planeDistance = Mathf.Max(top.nearClipPlane + NEAR_CLIP_MARGIN, MIN_PLANE_DISTANCE);
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
