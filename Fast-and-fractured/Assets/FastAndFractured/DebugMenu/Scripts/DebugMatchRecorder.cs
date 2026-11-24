using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FastAndFractured
{
    public sealed class DebugMatchRecorder : MonoBehaviour
    {
        public const string FILE_PREFIX = "matches_";
        public const float MIN_SECONDS = 5f;

        private const int MAX_FRAMES = 1000000;
        private const float LEVEL_SEARCH_SECONDS = 1f;
        private const float MILLISECONDS = 1000f;
        private const float PERCENT = 100f;

        private readonly List<float> _frames = new List<float>();
        private readonly FrameTiming[] _timings = new FrameTiming[1];
        private LevelController _level;
        private string _scene;
        private string _mood;
        private float _nextLevelSearch;
        private double _main;
        private double _render;
        private double _gpu;
        private int _timed;
        private int _focused;

        public static string FileName(string pipeline, string version) => FILE_PREFIX + pipeline + "_" + version + ".txt";

        public static bool ShouldRecord(bool hasLevel, bool matchStarted, float timeScale) => hasLevel && matchStarted && timeScale > 0f;

        public static bool WorthKeeping(DebugFrameStats stats) => stats.Mean * stats.Frames >= MIN_SECONDS * MILLISECONDS;

        public static string Extra(DateTime time, float seconds, float focusedPercent, double main, double render, double gpu, string quality, int width, int height) =>
            string.Format(CultureInfo.InvariantCulture, "seconds={0:F0}\tfocused={1:F0}%\tmain={2:F2}\trender={3:F2}\tgpu={4:F2}\tquality={5}\tscreen={6}x{7}\ttime={8}",
                seconds, focusedPercent, main, render, gpu, quality, width, height, time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!DebugMenuSwitch.Available || Application.isEditor) return;
            var go = new GameObject("[DebugMatchRecorder]");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<DebugMatchRecorder>();
        }

        private void OnEnable() => SceneManager.activeSceneChanged += OnSceneChanged;

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            Flush();
        }

        private void OnSceneChanged(Scene previous, Scene next)
        {
            Flush();
            _level = null;
            _nextLevelSearch = 0f;
        }

        private void Update()
        {
            if (_level == null && Time.unscaledTime >= _nextLevelSearch)
            {
                _nextLevelSearch = Time.unscaledTime + LEVEL_SEARCH_SECONDS;
                _level = LevelController.ExistingInstance;
            }
            if (!ShouldRecord(_level != null, _level != null && _level.HasGameStarted, Time.timeScale) || _frames.Count >= MAX_FRAMES) return;
            if (_frames.Count == 0) _scene = _level.gameObject.scene.name;
            _mood = Mood();
            _frames.Add(Time.unscaledDeltaTime * MILLISECONDS);
            if (Application.isFocused) _focused++;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timings) == 0) return;
            _main += _timings[0].cpuMainThreadFrameTime;
            _render += _timings[0].cpuRenderThreadFrameTime;
            _gpu += _timings[0].gpuFrameTime;
            _timed++;
        }

        private static string Mood()
        {
            global::VolumeManager volumes = global::VolumeManager.ExistingInstance;
            string mood = DebugVolumes.NameOf(volumes, DebugVolumes.CurrentIndex(volumes));
            return string.IsNullOrEmpty(mood) ? "none" : mood;
        }

        private void Flush()
        {
            DebugFrameStats stats = DebugFrameStats.Of(_frames);
            if (WorthKeeping(stats)) Write(stats);
            _frames.Clear();
            _main = _render = _gpu = 0d;
            _timed = _focused = 0;
        }

        private void Write(DebugFrameStats stats)
        {
            int timed = Mathf.Max(1, _timed);
            string extra = Extra(DateTime.Now, stats.Mean * stats.Frames / MILLISECONDS, PERCENT * _focused / stats.Frames, _main / timed, _render / timed, _gpu / timed,
                QualitySettings.names[QualitySettings.GetQualityLevel()], Screen.width, Screen.height);
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            string file = FileName(pipeline != null ? pipeline.GetType().Name : "BuiltIn", Application.version);
            try
            {
                Directory.CreateDirectory(DebugBenchmark.ReportFolder);
                File.AppendAllText(Path.Combine(DebugBenchmark.ReportFolder, file), DebugBenchmark.ReportLine(_scene, _mood, stats, extra) + Environment.NewLine, Encoding.UTF8);
            }
            catch (IOException)
            {
            }
        }
    }
}
