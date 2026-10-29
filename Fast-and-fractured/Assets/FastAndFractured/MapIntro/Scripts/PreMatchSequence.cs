using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using Utilities.Managers.PauseSystem;

namespace FastAndFractured
{
    /// <summary>
    /// What plays before the 3-2-1: the map intro, then every PreMatchStep of the map scene by order. Offline it holds
    /// the countdown and releases it so the "3" lands in the last second of the last stage; online the server adds
    /// DurationFor to the countdown lead and the stages only run under it.
    /// </summary>
    public sealed class PreMatchSequence : MonoBehaviour
    {
        private const string COUNTDOWN_METHOD = "StartEventCountdown";
        private const string PAUSE_KEY = "escape";
        private const string PAUSE_BUTTON = "start";
        private const float SKIP_INPUT_GRACE_SECONDS = 0.5f;
        public const float COUNTDOWN_OVERLAP_SECONDS = 1f;
        // The intro's final orbit shot is 3 s long.
        private const float MAX_ONLINE_OVERLAP_SECONDS = 3.5f;

        public static PreMatchSequence Current { get; private set; }

        private readonly List<IPreMatchStage> _stages = new List<IPreMatchStage>();
        private int _index = -1;
        private bool _online, _countdownStarted, _cutToEnd;
        private Action _onCountdown;
        private IDisposable _skipListener;
        private float _stageStartTime;

        private IPreMatchStage Stage => _index >= 0 && _index < _stages.Count ? _stages[_index] : null;
        private bool OnLastStage => _index == _stages.Count - 1;
        private float CountdownLead => COUNTDOWN_OVERLAP_SECONDS + (_online ? 0f : IngameEventsManager.EVENT_START_DELAY);

        // Offline onCountdown runs when the 3-2-1 is released (right away when there is nothing to show).
        public static PreMatchSequence Run(Scene scene, Transform car, bool online, Action onCountdown)
        {
            var stages = new List<IPreMatchStage>();
            MapIntro intro = MapIntro.Create(scene, car);
            if (intro != null) stages.Add(intro);
            stages.AddRange(StepsIn(scene));
            if (stages.Count == 0 || Current != null)
            {
                if (intro != null) Destroy(intro.gameObject);
                onCountdown?.Invoke();
                return null;
            }

            var go = new GameObject(nameof(PreMatchSequence));
            SceneManager.MoveGameObjectToScene(go, scene);
            var sequence = go.AddComponent<PreMatchSequence>();
            sequence._stages.AddRange(stages);
            sequence._online = online;
            sequence._onCountdown = onCountdown;
            Current = sequence;
            sequence._skipListener = InputSystem.onAnyButtonPress.Call(sequence.OnAnyButtonPress);
            sequence.PlayNext();
            return sequence;
        }

        public static float DurationFor(Scene scene)
        {
            float duration = MapIntroLibrary.DurationFor(scene.name);
            foreach (PreMatchStep step in StepsIn(scene)) duration += step.Duration;
            return duration;
        }

        // Not scene.GetRootGameObjects(): LevelController starts the sequence while its scene is still loading.
        private static IEnumerable<PreMatchStep> StepsIn(Scene scene)
        {
            return Resources.FindObjectsOfTypeAll<PreMatchStep>()
                .Where(step => step.gameObject.scene == scene && step.enabled && step.gameObject.activeInHierarchy)
                .OrderBy(step => step.Order)
                .ToList();
        }

        private void PlayNext()
        {
            _index++;
            if (_cutToEnd || Stage == null)
            {
                StartCountdown();
                Destroy(gameObject);
                return;
            }
            _stageStartTime = Time.unscaledTime;
            IPreMatchStage stage = Stage;
            stage.Play(() => { if (Stage == stage) PlayNext(); });
        }

        private void Update()
        {
            if (!_online && !_countdownStarted)
                foreach (GameReadyScript countdown in FindObjectsByType<GameReadyScript>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    countdown.CancelInvoke(COUNTDOWN_METHOD);
            if (!_countdownStarted && OnLastStage && Stage != null && Stage.Remaining <= CountdownLead) StartCountdown();
        }

        public void EndForCountdown()
        {
            if (_countdownStarted) return;
            if (OnLastStage && Stage != null && Stage.Remaining <= MAX_ONLINE_OVERLAP_SECONDS)
            {
                StartCountdown();
                return;
            }
            _cutToEnd = true;
            Stage?.Skip();
        }

        private void StartCountdown()
        {
            if (_countdownStarted) return;
            _countdownStarted = true;
            Stage?.OnCountdownStarted();
            if (_online) return;
            foreach (GameReadyScript countdown in FindObjectsByType<GameReadyScript>(FindObjectsSortMode.None))
                countdown.Invoke(COUNTDOWN_METHOD, IngameEventsManager.EVENT_START_DELAY);
            Action onCountdown = _onCountdown;
            _onCountdown = null;
            onCountdown?.Invoke();
        }

        // MapIntro listens for its own skips.
        private void OnAnyButtonPress(InputControl control)
        {
            if (_countdownStarted || !(Stage is TimelinePreMatchStep step) || !step.Skippable) return;
            if (Time.unscaledTime - _stageStartTime < SKIP_INPUT_GRACE_SECONDS) return;
            if (control.name == PAUSE_KEY || control.name == PAUSE_BUTTON) return;
            if (PauseManager.HasInstance && PauseManager.Instance.IsGamePaused) return;
            step.Skip();
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            _skipListener?.Dispose();
            _skipListener = null;
        }
    }
}
