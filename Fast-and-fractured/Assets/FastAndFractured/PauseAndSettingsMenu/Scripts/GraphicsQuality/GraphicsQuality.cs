using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace FastAndFractured
{
    public static class GraphicsQuality
    {
        public const string MODE_KEY = "GraphicsQualityMode";
        public const string LEVEL_KEY = "GraphicsQualityLevel";
        public const string DETECTION_VERSION_KEY = "GraphicsDetectionVersion";
        public const int DETECTION_VERSION = 1;
        public const int MODE_AUTO = 0;
        public const int MODE_MANUAL = 1;

        public const string VSYNC_KEY = "Vsync";
        public const string MAX_FPS_KEY = "MaxFPS";
        public const string ANTI_ALIASING_KEY = "Anti-Aliasing";
        public const string TAA_SHARPENING_KEY = "TAA_Sharpening";
        public const int FPS_DEFAULT = 60;
        private const float SHARPENING_DEFAULT = 0.5f;
        private const string MAIN_CAMERA_TAG = "MainCamera";

        public static readonly string[] LEVEL_NAME_KEYS =
        {
            "Graphics.QualityVeryLow",
            "Graphics.QualityLow",
            "Graphics.QualityMedium",
            "Graphics.QualityHigh",
            "Graphics.QualityVeryHigh",
            "Graphics.QualityUltra",
        };

        public static event Action<int> LevelChanged;

        private static readonly HashSet<int> _camerasWithSavedSettings = new HashSet<int>();

        public static int LevelCount => QualitySettings.names.Length;
        public static int CurrentLevel => QualitySettings.GetQualityLevel();
        public static bool IsAuto => PlayerPrefs.GetInt(MODE_KEY, MODE_AUTO) == MODE_AUTO;
        public static bool HasSavedLevel => PlayerPrefs.HasKey(LEVEL_KEY);
        public static int SavedLevel => ClampLevel(PlayerPrefs.GetInt(LEVEL_KEY, CurrentLevel), LevelCount);

        public static bool NeedsDetection =>
            NeedsDetectionFor(IsAuto, HasSavedLevel, PlayerPrefs.GetInt(DETECTION_VERSION_KEY, 0));

        public static bool NeedsDetectionFor(bool isAuto, bool hasSavedLevel, int savedDetectionVersion) =>
            isAuto && (!hasSavedLevel || savedDetectionVersion != DETECTION_VERSION);

        public static int ClampLevel(int level, int levelCount) => Mathf.Clamp(level, 0, Mathf.Max(0, levelCount - 1));

        public const int STEAM_DECK_LEVEL = 0;
        public const int STEAM_DECK_FPS = 40;
        private const string STEAM_DECK_ENV = "SteamDeck";
        private static readonly string[] STEAM_DECK_GPUS = { "VANGOGH", "Custom GPU 0405", "Custom GPU 0932" };

        public static bool IsSteamDeck =>
            IsSteamDeckDevice(Environment.GetEnvironmentVariable(STEAM_DECK_ENV), SystemInfo.graphicsDeviceName);

        // Steam sets SteamDeck=1 on the Deck; under Proton the APU's name is the fallback.
        public static bool IsSteamDeckDevice(string steamDeckEnv, string gpuName)
        {
            if (steamDeckEnv == "1") return true;
            if (string.IsNullOrEmpty(gpuName)) return false;
            foreach (var deckGpu in STEAM_DECK_GPUS)
                if (gpuName.IndexOf(deckGpu, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        public static void ApplySteamDeckProfile()
        {
            if (!PlayerPrefs.HasKey(MAX_FPS_KEY))
                PlayerPrefs.SetInt(MAX_FPS_KEY, STEAM_DECK_FPS);
            SaveDetected(STEAM_DECK_LEVEL);
        }

        public static string LevelNameKey(int level) =>
            level >= 0 && level < LEVEL_NAME_KEYS.Length ? LEVEL_NAME_KEYS[level] : null;

        public static void SaveDetected(int level)
        {
            PlayerPrefs.SetInt(MODE_KEY, MODE_AUTO);
            PlayerPrefs.SetInt(LEVEL_KEY, ClampLevel(level, LevelCount));
            PlayerPrefs.SetInt(DETECTION_VERSION_KEY, DETECTION_VERSION);
            PlayerPrefs.Save();
            Apply(level);
        }

        public static void SaveManual(int level)
        {
            PlayerPrefs.SetInt(MODE_KEY, MODE_MANUAL);
            PlayerPrefs.SetInt(LEVEL_KEY, ClampLevel(level, LevelCount));
            PlayerPrefs.Save();
            Apply(level);
        }

        public static void SetAuto()
        {
            PlayerPrefs.SetInt(MODE_KEY, MODE_AUTO);
            PlayerPrefs.DeleteKey(DETECTION_VERSION_KEY);
            PlayerPrefs.Save();
        }

        public static void Apply(int level)
        {
            level = ClampLevel(level, LevelCount);
            if (CurrentLevel != level)
                QualitySettings.SetQualityLevel(level, true);
            // Every quality level carries its own vSyncCount: the player's choice wins.
            ApplyFrameRate();
            LevelChanged?.Invoke(level);
        }

        public static void ApplySaved()
        {
            if (HasSavedLevel)
                Apply(SavedLevel);
            else
                ApplyFrameRate();
        }

        public static void ApplyFrameRate()
        {
            bool vsync = PlayerPrefs.GetInt(VSYNC_KEY, 0) != 0;
            QualitySettings.vSyncCount = vsync ? 1 : 0;
            Application.targetFrameRate = vsync ? -1 : PlayerPrefs.GetInt(MAX_FPS_KEY, FPS_DEFAULT);
        }

        public static void ApplySavedAntiAliasing(Camera camera)
        {
            if (camera == null || !camera.TryGetComponent(out HDAdditionalCameraData data))
                return;
            string saved = PlayerPrefs.GetString(ANTI_ALIASING_KEY, string.Empty);
            if (!Enum.TryParse(saved, out HDAdditionalCameraData.AntialiasingMode mode))
                return;
            data.antialiasing = mode;
            if (mode == HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing)
                data.taaSharpenStrength = PlayerPrefs.GetFloat(TAA_SHARPENING_KEY, SHARPENING_DEFAULT);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOnBoot()
        {
            ApplySaved();
            _camerasWithSavedSettings.Clear();
            RenderPipelineManager.beginCameraRendering -= ApplySavedSettingsOnFirstRender;
            RenderPipelineManager.beginCameraRendering += ApplySavedSettingsOnFirstRender;
            SceneManager.sceneLoaded -= ForgetCameras;
            SceneManager.sceneLoaded += ForgetCameras;
        }

        private static void ApplySavedSettingsOnFirstRender(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType != CameraType.Game || !camera.CompareTag(MAIN_CAMERA_TAG))
                return;
            if (!_camerasWithSavedSettings.Add(camera.GetInstanceID()))
                return;
            ApplySavedAntiAliasing(camera);
            Upscaling.ApplySaved(camera);
            UiOutsideUpscaling.MoveToOverlay(camera);
        }

        private static void ForgetCameras(Scene scene, LoadSceneMode mode) => _camerasWithSavedSettings.Clear();
    }
}
