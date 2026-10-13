using UnityEngine;
using UnityEngine.InputSystem;

namespace FastAndFractured.GameTelemetry
{
    /// <summary>Game start (version, hardware, language, input) and game end (how long it was open).</summary>
    public static class SessionCollector
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            Telemetry.Quitting -= OnQuitting;
            Telemetry.Quitting += OnQuitting;

            var screen = Screen.currentResolution;
            Telemetry.Track(TelemetryKeys.SESSION_START,
                ("version", Telemetry.Version),
                ("os", SystemInfo.operatingSystem),
                ("cpu", SystemInfo.processorType),
                ("cpu_cores", SystemInfo.processorCount),
                ("gpu", SystemInfo.graphicsDeviceName),
                ("gpu_memory_mb", SystemInfo.graphicsMemorySize),
                ("ram_mb", SystemInfo.systemMemorySize),
                ("resolution", $"{Screen.width}x{Screen.height}"),
                ("refresh_hz", Mathf.RoundToInt((float)screen.refreshRateRatio.value)),
                ("fullscreen", Screen.fullScreen),
                ("quality", QualitySettings.names.Length > 0 ? QualitySettings.names[QualitySettings.GetQualityLevel()] : ""),
                ("language", Application.systemLanguage.ToString()),
                ("gamepad", Gamepad.current != null),
                ("steam", TelemetryIdentity.SteamHash != null));
        }

        private static void OnQuitting() =>
            Telemetry.Track(TelemetryKeys.SESSION_END, ("duration_s", Mathf.RoundToInt(Time.realtimeSinceStartup)));
    }
}
