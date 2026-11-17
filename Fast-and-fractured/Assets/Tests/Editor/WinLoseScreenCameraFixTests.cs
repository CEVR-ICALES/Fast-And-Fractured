using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FastAndFractured.Tests
{
    public sealed class WinLoseScreenCameraFixTests
    {
        private const BindingFlags INSTANCE_PRIVATE = BindingFlags.Instance | BindingFlags.NonPublic;
        private const int LOADING_SCREEN_ORDER = 1;
        private const int MENU_ORDER = 3;

        [Test]
        public void OverlaidMenuStaysUnderTheLoadingScreenAndIsRestoredAfterwards()
        {
            var menu = new GameObject("Menu canvas", typeof(RectTransform));
            var cameraObject = new GameObject("Menu camera", typeof(Camera));
            try
            {
                var canvas = menu.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                // Without a camera a ScreenSpaceCamera canvas reports itself as overlay.
                canvas.worldCamera = cameraObject.GetComponent<Camera>();
                canvas.sortingOrder = MENU_ORDER;
                var screen = new GameObject("WinLose screen", typeof(RectTransform));
                screen.transform.SetParent(menu.transform, false);
                var fix = screen.AddComponent<WinLoseScreenCameraFix>();
                Invoke(fix, "Awake");

                Invoke(fix, "SetOverlay", true);
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
                Assert.Less(canvas.sortingOrder, LOADING_SCREEN_ORDER, "Retry shows the loading screen over the win/lose menu.");

                Invoke(fix, "SetOverlay", false);
                Assert.AreEqual(RenderMode.ScreenSpaceCamera, canvas.renderMode);
                Assert.AreEqual(MENU_ORDER, canvas.sortingOrder);
            }
            finally
            {
                Object.DestroyImmediate(menu);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static void Invoke(WinLoseScreenCameraFix fix, string method, params object[] args) =>
            typeof(WinLoseScreenCameraFix).GetMethod(method, INSTANCE_PRIVATE).Invoke(fix, args);
    }
}
