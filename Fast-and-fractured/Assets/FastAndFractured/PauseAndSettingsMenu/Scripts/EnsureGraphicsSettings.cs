using UnityEngine;
using Utilities;

namespace FastAndFractured
{
    public class EnsureGraphicsSettings : MonoBehaviour
    {
        private const float callDelay = 0.5f;

        private void Start()
        {
            TimerSystem.Instance.CreateTimer(callDelay, onTimerDecreaseComplete: ApplySettings);
        }

        private void ApplySettings()
        {
            if (GraphicsQualityDetector.IsRunning)
                return;
            GraphicsQuality.ApplyFrameRate();
            GraphicsQuality.ApplySavedAntiAliasing(Camera.main);
        }
    }
}
