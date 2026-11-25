using UnityEngine;

namespace FastAndFractured
{
    // A camera-space canvas is drawn inside the 3D frame: DLSS/FSR2 upscale it and TAA, fog and bloom smear it.
    public static class UiOutsideUpscaling
    {
        public static int MoveToOverlay(Camera camera)
        {
            int moved = 0;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!ShouldMove(canvas, camera)) continue;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                moved++;
            }
            return moved;
        }

        public static bool ShouldMove(Canvas canvas, Camera camera) =>
            canvas != null && canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceCamera &&
            canvas.worldCamera == camera && !HoldsSceneRenderers(canvas);

        // 3D models or particles placed in the canvas rely on the camera to be drawn.
        private static bool HoldsSceneRenderers(Canvas canvas) => canvas.GetComponentInChildren<Renderer>(true) != null;
    }
}
