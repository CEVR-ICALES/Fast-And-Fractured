using System;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace FastAndFractured
{
    public static class Upscaling
    {
        public const string MODE_KEY = "UpscalingMode";
        // Older builds saved the DLSS quality value here, not the dropdown index: keep reading it that way.
        public const string PRESET_KEY = "upscalingResolutionMode";
        public const string NONE = "NONE";
        public const string DLSS = "DLSS";
        public const string FSR2 = "FSR2";
        public const int DEFAULT_PRESET = 1;

        public static readonly string[] PRESET_NAMES = { "Max Quality", "Balanced", "Performance", "Ultra Performance" };

        // UnityEngine.NVIDIA.DLSSQuality and UnityEngine.AMD.FSR2Quality values, in PRESET_NAMES order.
        private static readonly uint[] DLSS_QUALITY = { 2, 1, 0, 3 };
        private static readonly uint[] FSR2_QUALITY = { 0, 1, 2, 3 };

        public static string SavedMode => PlayerPrefs.GetString(MODE_KEY, NONE);
        public static int SavedPreset => PresetFromDlssQuality(PlayerPrefs.GetInt(PRESET_KEY, (int)DLSS_QUALITY[DEFAULT_PRESET]));

        public static int ClampPreset(int preset) => Mathf.Clamp(preset, 0, PRESET_NAMES.Length - 1);
        public static uint DlssQuality(int preset) => DLSS_QUALITY[ClampPreset(preset)];
        public static uint Fsr2Quality(int preset) => FSR2_QUALITY[ClampPreset(preset)];

        public static int PresetFromDlssQuality(int quality)
        {
            int preset = quality < 0 ? -1 : Array.IndexOf(DLSS_QUALITY, (uint)quality);
            return preset < 0 ? DEFAULT_PRESET : preset;
        }

        public static bool IsAvailable(string mode)
        {
            if (mode == DLSS) return HDDynamicResolutionPlatformCapabilities.DLSSDetected;
            if (mode == FSR2) return HDDynamicResolutionPlatformCapabilities.FSR2Detected;
            return mode == NONE;
        }

        public static void SaveMode(string mode)
        {
            PlayerPrefs.SetString(MODE_KEY, mode);
            PlayerPrefs.Save();
        }

        public static void SavePreset(int preset)
        {
            PlayerPrefs.SetInt(PRESET_KEY, (int)DlssQuality(preset));
            PlayerPrefs.Save();
        }

        public static void ApplySaved(Camera camera) => Apply(camera, SavedMode, SavedPreset);

        public static void Apply(Camera camera, string mode, int preset)
        {
            if (camera == null || !camera.TryGetComponent(out HDAdditionalCameraData data))
                return;
            bool dlss = mode == DLSS && IsAvailable(DLSS);
            bool fsr2 = mode == FSR2 && IsAvailable(FSR2);

            camera.allowDynamicResolution = dlss || fsr2;
            data.allowDynamicResolution = dlss || fsr2;

            data.allowDeepLearningSuperSampling = dlss;
            data.deepLearningSuperSamplingUseCustomAttributes = dlss;
            data.deepLearningSuperSamplingUseCustomQualitySettings = dlss;
            // Optimal settings let the upscaler pick the render scale; the asset's percentage range must allow it.
            data.deepLearningSuperSamplingUseOptimalSettings = dlss;
            data.deepLearningSuperSamplingQuality = DlssQuality(preset);

            data.allowFidelityFX2SuperResolution = fsr2;
            data.fidelityFX2SuperResolutionUseCustomAttributes = fsr2;
            data.fidelityFX2SuperResolutionUseCustomQualitySettings = fsr2;
            data.fidelityFX2SuperResolutionUseOptimalSettings = fsr2;
            data.fidelityFX2SuperResolutionQuality = Fsr2Quality(preset);
        }
    }
}
