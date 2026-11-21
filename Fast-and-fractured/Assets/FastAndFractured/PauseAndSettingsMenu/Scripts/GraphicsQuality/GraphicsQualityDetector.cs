using System.Collections.Generic;
using Assets.SimpleLocalization.Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FastAndFractured
{
    public sealed class GraphicsQualityDetector : MonoBehaviour
    {
        private const int MAIN_MENU_BUILD_INDEX = 1;
        private const float START_DELAY = 1.5f;
        private const int WARMUP_FRAMES = 30;
        private const float WARMUP_MAX_SECONDS = 1f;
        private const int MEASURE_FRAMES = 90;
        private const float MEASURE_MAX_SECONDS = 2f;
        private const float PERCENTILE = 0.9f;
        private const float TARGET_FRAME_MS = 1000f / 60f;
        private const float MENU_HEADROOM = 0.75f;
        private const float IN_GAME_HEADROOM = 0.9f;
        private const float NOTICE_SECONDS = 3f;
        private const string DETECTING_KEY = "Graphics.QualityDetecting";
        private const string DETECTED_KEY = "Graphics.QualityDetected";
        private const int NOTICE_SORTING_ORDER = 32000;
        private const float NOTICE_FONT_SIZE = 26f;
        private const float NOTICE_ALPHA = 0.85f;
        private static readonly Vector2 NOTICE_MARGIN = new Vector2(-32f, 28f);
        private static readonly Vector2 NOTICE_SIZE = new Vector2(700f, 60f);
        private static readonly Vector2 REFERENCE_RESOLUTION = new Vector2(1920f, 1080f);

        private enum Phase { Waiting, Warmup, Measure, Notice }

        private static GraphicsQualityDetector _running;

        private readonly List<float> _samples = new List<float>();
        private readonly FrameTiming[] _timing = new FrameTiming[1];
        private GraphicsQualitySearch _search;
        private Phase _phase;
        private float _phaseStart;
        private int _phaseFrames;
        private int _levelBefore;
        private bool _finished;
        private TextMeshProUGUI _notice;

        public static bool IsRunning => _running != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Application.isEditor && scene.buildIndex == MAIN_MENU_BUILD_INDEX && GraphicsQuality.NeedsDetection)
                Run();
        }

        public static void Run()
        {
            if (_running != null)
                return;
            if (GraphicsQuality.IsSteamDeck)
            {
                GraphicsQuality.ApplySteamDeckProfile();
                return;
            }
            var go = new GameObject("[GraphicsQualityDetector]");
            _running = go.AddComponent<GraphicsQualityDetector>();
        }

        public static void Cancel()
        {
            if (_running == null)
                return;
            _running._finished = true;
            GraphicsQuality.ApplyFrameRate();
            Destroy(_running.gameObject);
            _running = null;
        }

        private void Start()
        {
            _levelBefore = GraphicsQuality.CurrentLevel;
            bool inMenu = SceneManager.GetActiveScene().buildIndex == MAIN_MENU_BUILD_INDEX;
            float budget = TARGET_FRAME_MS * (inMenu ? MENU_HEADROOM : IN_GAME_HEADROOM);
            int start = GraphicsQualitySearch.StartLevel(GraphicsQuality.LevelCount, SystemInfo.graphicsMemorySize,
                SystemInfo.systemMemorySize, GraphicsQualitySearch.LooksIntegrated(SystemInfo.graphicsDeviceName));
            _search = new GraphicsQualitySearch(GraphicsQuality.LevelCount, start, budget);

            UncapFrameRate();
            CreateNotice();
            ShowNotice(LocalizationManager.Localize(DETECTING_KEY));
            EnterPhase(Phase.Waiting);
        }

        private void Update()
        {
            float elapsed = Time.unscaledTime - _phaseStart;
            _phaseFrames++;
            if (_phase != Phase.Notice)
                UncapFrameRate();
            switch (_phase)
            {
                case Phase.Waiting:
                    if (elapsed >= START_DELAY)
                        BeginLevel(_search.Current);
                    break;
                case Phase.Warmup:
                    SampleFrameMs();
                    if (_phaseFrames >= WARMUP_FRAMES || elapsed >= WARMUP_MAX_SECONDS)
                    {
                        _samples.Clear();
                        EnterPhase(Phase.Measure);
                    }
                    break;
                case Phase.Measure:
                    _samples.Add(SampleFrameMs());
                    if (_phaseFrames >= MEASURE_FRAMES || elapsed >= MEASURE_MAX_SECONDS)
                        FinishLevel();
                    break;
                case Phase.Notice:
                    if (elapsed >= NOTICE_SECONDS)
                        Destroy(gameObject);
                    break;
            }
        }

        private void BeginLevel(int level)
        {
            if (GraphicsQuality.CurrentLevel != level)
                QualitySettings.SetQualityLevel(level, true);
            EnterPhase(Phase.Warmup);
        }

        private void FinishLevel()
        {
            float measured = GraphicsQualitySearch.Percentile(_samples, PERCENTILE);
            Debug.Log($"[GraphicsQuality] {QualitySettings.names[_search.Current]}: p90 {measured:0.0} ms");
            _search.Report(measured);
            if (!_search.IsDone)
            {
                BeginLevel(_search.Current);
                return;
            }

            _finished = true;
            GraphicsQuality.SaveDetected(_search.Result);
            string levelName = LocalizationManager.Localize(GraphicsQuality.LevelNameKey(_search.Result) ?? string.Empty);
            ShowNotice(LocalizationManager.Localize(DETECTED_KEY, levelName));
            EnterPhase(Phase.Notice);
        }

        private float SampleFrameMs()
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.IsFeatureEnabled() && FrameTimingManager.GetLatestTimings(1, _timing) > 0 &&
                _timing[0].gpuFrameTime > 0d)
                return (float)_timing[0].gpuFrameTime;
            return Time.unscaledDeltaTime * 1000f;
        }

        private static void UncapFrameRate()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
        }

        private void EnterPhase(Phase phase)
        {
            _phase = phase;
            _phaseStart = Time.unscaledTime;
            _phaseFrames = 0;
        }

        private void OnDestroy()
        {
            if (_running == this)
                _running = null;
            if (_finished)
                return;
            if (GraphicsQuality.CurrentLevel != _levelBefore)
                QualitySettings.SetQualityLevel(_levelBefore, true);
            GraphicsQuality.ApplyFrameRate();
        }

        private void CreateNotice()
        {
            var canvasObject = new GameObject("Notice", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = NOTICE_SORTING_ORDER;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = REFERENCE_RESOLUTION;

            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = NOTICE_MARGIN;
            rect.sizeDelta = NOTICE_SIZE;
            _notice = textObject.AddComponent<TextMeshProUGUI>();
            _notice.alignment = TextAlignmentOptions.BottomRight;
            _notice.fontSize = NOTICE_FONT_SIZE;
            _notice.raycastTarget = false;
            _notice.color = new Color(1f, 1f, 1f, NOTICE_ALPHA);
        }

        private void ShowNotice(string text)
        {
            if (_notice != null)
                _notice.text = text;
        }
    }
}
