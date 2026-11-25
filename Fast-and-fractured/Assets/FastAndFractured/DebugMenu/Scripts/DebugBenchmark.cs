using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FastAndFractured
{
    public sealed class DebugBenchmark : MonoBehaviour
    {
        public const string REPORT_FOLDER = "Benchmarks";

        private const int MENU_BUILD_INDEX = 1;
        private const int FIRST_SPAWN = 0;
        private const float MENU_SETTLE_SECONDS = 8f;
        private const float WARM_UP_SECONDS = 5f;
        private const float START_TIMEOUT_SECONDS = 180f;
        private const float MILLISECONDS = 1000f;
        private const double BYTES_TO_MEGABYTES = 1d / (1024d * 1024d);
        private const string NO_MOOD = "none";
        private static readonly string[] COUNTERS = { "Draw Calls Count", "Batches Count", "SetPass Calls Count", "Triangles Count", "Shadow Casters Count" };
        private static readonly string[] COUNTER_LABELS = { "draws", "batches", "setpass", "triangles", "shadowCasters" };

        private readonly FrameTiming[] _timings = new FrameTiming[1];
        private readonly StringBuilder _report = new StringBuilder();
        private DebugBenchmarkOptions _options;

        public static string ReportFolder => Path.Combine(Application.persistentDataPath, REPORT_FOLDER);

        public static string ReportName(string pipeline, string version, DateTime time) =>
            "benchmark_" + pipeline + "_" + version + "_" + time.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".txt";

        public static string ReportLine(string scene, string mood, DebugFrameStats stats, string extra) =>
            string.Format(CultureInfo.InvariantCulture, "scene={0}\tmood={1}\tfps={2:F1}\tframes={3}\tmean={4:F2}\tp50={5:F2}\tp95={6:F2}\tp99={7:F2}\tworst={8:F2}\t{9}",
                scene, mood, stats.Fps, stats.Frames, stats.Mean, stats.Median, stats.P95, stats.P99, stats.Worst, extra);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            DebugBenchmarkOptions options = DebugBenchmarkOptions.From(Environment.GetCommandLineArgs());
            if (!options.Requested) return;
            var go = new GameObject("[DebugBenchmark]");
            DontDestroyOnLoad(go);
            go.AddComponent<DebugBenchmark>()._options = options;
        }

        private void Start() => StartCoroutine(_Run());

        private IEnumerator _Run()
        {
            while (SceneManager.GetActiveScene().buildIndex < MENU_BUILD_INDEX) yield return null;
            yield return new WaitForSecondsRealtime(MENU_SETTLE_SECONDS);
            _report.AppendLine(Header());
            DebugVolumes.Show(global::VolumeManager.ExistingInstance, _options.Mood);
            yield return _Record(SceneManager.GetActiveScene().name);
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (_options.Wants(path)) yield return _Play(i, Path.GetFileNameWithoutExtension(path));
            }
            Write();
            Application.Quit();
        }

        private IEnumerator _Play(int buildIndex, string map)
        {
            DebugCustomMatch.Request(_options.Rivals, FIRST_SPAWN);
            SceneManager.LoadScene(buildIndex);
            float deadline = Time.realtimeSinceStartup + START_TIMEOUT_SECONDS;
            while (!MatchStarted(buildIndex) && Time.realtimeSinceStartup < deadline) yield return null;
            if (!MatchStarted(buildIndex))
            {
                _report.AppendLine("scene=" + map + "\tthe match never started");
                yield break;
            }
            LevelController level = LevelController.ExistingInstance;
            StatsController stats = DebugCheats.LocalStats(level);
            if (stats != null) stats.DebugGodMode = true;
            DebugCheats.ShieldRivals(level, true);
            SandstormController storm = FindFirstObjectByType<SandstormController>();
            if (storm != null) storm.DebugSpeed = 0f;
            DebugVolumes.Show(global::VolumeManager.ExistingInstance, _options.Mood);
            yield return _Record(map);
        }

        private static bool MatchStarted(int buildIndex)
        {
            LevelController level = LevelController.ExistingInstance;
            return level != null && level.gameObject.scene.buildIndex == buildIndex && level.HasGameStarted;
        }

        private IEnumerator _Record(string scene)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            yield return new WaitForSecondsRealtime(WARM_UP_SECONDS);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            var recorders = new ProfilerRecorder[COUNTERS.Length];
            for (int i = 0; i < COUNTERS.Length; i++) recorders[i] = ProfilerRecorder.StartNew(ProfilerCategory.Render, COUNTERS[i]);
            var counterSums = new double[COUNTERS.Length];
            var frames = new List<float>();
            double main = 0d, render = 0d, gpu = 0d, wait = 0d;
            int timed = 0;
            float until = Time.realtimeSinceStartup + _options.Seconds;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                frames.Add(Time.unscaledDeltaTime * MILLISECONDS);
                for (int i = 0; i < recorders.Length; i++)
                    if (recorders[i].Valid) counterSums[i] += recorders[i].LastValue;
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, _timings) == 0) continue;
                main += _timings[0].cpuMainThreadFrameTime;
                render += _timings[0].cpuRenderThreadFrameTime;
                gpu += _timings[0].gpuFrameTime;
                wait += _timings[0].cpuMainThreadPresentWaitTime;
                timed++;
            }

            var extra = new StringBuilder();
            int frameCount = Mathf.Max(1, frames.Count);
            int timedCount = Mathf.Max(1, timed);
            extra.AppendFormat(CultureInfo.InvariantCulture, "main={0:F2}\trender={1:F2}\tgpu={2:F2}\tpresentWait={3:F2}",
                main / timedCount, render / timedCount, gpu / timedCount, wait / timedCount);
            for (int i = 0; i < recorders.Length; i++)
            {
                if (recorders[i].Valid) extra.AppendFormat(CultureInfo.InvariantCulture, "\t{0}={1:F0}", COUNTER_LABELS[i], counterSums[i] / frameCount);
                recorders[i].Dispose();
            }
            extra.AppendFormat(CultureInfo.InvariantCulture, "\tmanagedMB={0:F0}\tusedMB={1:F0}",
                GC.GetTotalMemory(false) * BYTES_TO_MEGABYTES, UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() * BYTES_TO_MEGABYTES);
            _report.AppendLine(ReportLine(scene, Mood(), DebugFrameStats.Of(frames), extra.ToString()));
        }

        private static string Mood()
        {
            global::VolumeManager volumes = global::VolumeManager.ExistingInstance;
            string mood = DebugVolumes.NameOf(volumes, DebugVolumes.CurrentIndex(volumes));
            return string.IsNullOrEmpty(mood) ? NO_MOOD : mood;
        }

        private static string Header()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            return string.Format(CultureInfo.InvariantCulture,
                "version={0}\tpipeline={1}\tasset={2}\tquality={3}\tunity={4}\tscreen={5}x{6}\tmode={7}\tgpu={8}\tcpu={9}\tapi={10}",
                Application.version, PipelineName(), pipeline != null ? pipeline.name : "none", QualitySettings.names[QualitySettings.GetQualityLevel()],
                Application.unityVersion, Screen.width, Screen.height, Screen.fullScreenMode, SystemInfo.graphicsDeviceName, SystemInfo.processorType,
                SystemInfo.graphicsDeviceType);
        }

        private static string PipelineName()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            return pipeline != null ? pipeline.GetType().Name : "BuiltIn";
        }

        private void Write()
        {
            Directory.CreateDirectory(ReportFolder);
            File.WriteAllText(Path.Combine(ReportFolder, ReportName(PipelineName(), Application.version, DateTime.Now)), _report.ToString());
        }
    }
}
