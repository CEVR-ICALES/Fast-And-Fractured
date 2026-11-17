using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured.GameTelemetry
{
    /// <summary>Tutorial events: start, each exercise passed, finish, and where players leave.</summary>
    public static class TutorialCollector
    {
        private const string SCENE_MARK = "Training";

        private static Scene _scene;
        private static float _start;
        private static int _steps, _total;
        private static bool _active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _active = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            SceneManager.sceneUnloaded -= OnUnloaded;
            SceneManager.sceneUnloaded += OnUnloaded;
            Telemetry.Quitting -= Exit;
            Telemetry.Quitting += Exit;
            OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        internal static bool IsTutorial(Scene scene) => scene.name.Contains(SCENE_MARK);

        private static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsTutorial(scene) || (_active && _scene == scene)) return;
            _scene = scene;
            _start = Time.realtimeSinceStartup;
            _steps = 0;
            _active = true;
            var exercises = Object.FindObjectsByType<TrainingInputSucceded>(FindObjectsSortMode.None);
            _total = exercises.Length;
            foreach (var exercise in exercises) exercise.onInputPerformed.AddListener(OnStep);
            Telemetry.Track(TelemetryKeys.TUTORIAL_START, ("total", _total));
        }

        private static void OnStep()
        {
            if (!_active) return;
            _steps++;
            float time = Time.realtimeSinceStartup - _start;
            Telemetry.Track(TelemetryKeys.TUTORIAL_STEP, ("step", _steps), ("total", _total), ("duration_s", Mathf.RoundToInt(time)));
            if (_steps == _total)
            {
                Telemetry.Track(TelemetryKeys.TUTORIAL_COMPLETE, ("duration_s", Mathf.RoundToInt(time)));
                Telemetry.Flush();
            }
        }

        private static void OnUnloaded(Scene scene)
        {
            if (_active && scene == _scene) Exit();
        }

        private static void Exit()
        {
            if (!_active) return;
            _active = false;
            Telemetry.Track(TelemetryKeys.TUTORIAL_EXIT,
                ("steps_done", _steps), ("total", _total), ("completed", _total > 0 && _steps >= _total),
                ("duration_s", Mathf.RoundToInt(Time.realtimeSinceStartup - _start)));
            Telemetry.Flush();
        }
    }
}
