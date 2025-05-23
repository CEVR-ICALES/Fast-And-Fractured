using UnityEngine;
using Utilities;

namespace FastAndFractured
{
    public class EnsureGraphicsSettings : MonoBehaviour
    {
        private const int FPS_DEFAULT = 60;
        private const int VSYNC_DEFAULT = 0;
        private const float callDelay = 0.5f;

        private void Start()
        {
            TimerSystem.Instance.CreateTimer(callDelay, onTimerDecreaseComplete: ApplySettings);
        }

        private void ApplySettings()
        {
            bool isVsyncEnabled = false;
            int vsync = PlayerPrefs.GetInt("Vsync", VSYNC_DEFAULT);
            if (vsync != VSYNC_DEFAULT)
                isVsyncEnabled = true;
            QualitySettings.vSyncCount = isVsyncEnabled ? 1 : VSYNC_DEFAULT;
            Application.targetFrameRate = isVsyncEnabled ? -1 : PlayerPrefs.GetInt("MaxFPS", FPS_DEFAULT);
        }
    }
}


