using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

public class TimerSystem : MonoBehaviour
{
    public enum TimerType
    {
        Decrease,
        Increase
    }

    private static TimerSystem _instance;

    private readonly List<Timer> _activeTimers = new();
    private readonly List<Timer> _timersToRemove = new();

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        for (var i = 0; i < _activeTimers.Count; i++)
        {
            var timer = _activeTimers[i];
            timer.Update(Time.deltaTime);

            if (timer.IsComplete) _timersToRemove.Add(timer);
        }

        foreach (var timer in _timersToRemove) _activeTimers.Remove(timer);

        _timersToRemove.Clear();
    }

    public static Timer CreateDecreasingTimer(float duration, Action onComplete, Action<float, float> onUpdate = null, string id = null)
    {
        if (_instance == null)
        {
            Debug.LogError(
                "TimerSystem not found in the scene.  Make sure there's a GameObject with TimerSystem attached.");
            return null;
        }

        if (id == null)
        {
            id =  GUID.Generate().ToString();
        }
        var timer = new Timer(duration, onComplete, id, onUpdate);
        _instance._activeTimers.Add(timer);
        return timer;
    }

    public static Timer CreateIncreasingTimer(float duration, Action onComplete,
        Action<float, float> onUpdate = null, string id = null)
    {
        var timer = CreateDecreasingTimer(duration, onComplete, onUpdate, id);
        timer.TimeRemaining = 0;
        timer.TimerType = TimerType.Increase;
        return timer;
    }

    public static void PauseTimer(string id)
    {
        _instance._activeTimers.Find((t) => t.Id == id)?.Pause();
    }
    public static void ResumeTimer(string id)
    {
        _instance._activeTimers.Find((t) => t.Id == id)?.Resume();
    }

    public static void RemoveTimer(Timer timer)
    {
        _instance._activeTimers.Remove(timer);
        _instance._timersToRemove.Add(timer);


    }
    
    public static void ReAddTimer(Timer timer)
    {
        if (_instance._activeTimers.Contains(timer))
        {
            return;
        }
        _instance._activeTimers.Add(timer);
    }
    [Serializable] public class Timer
    {
        private readonly Action _onComplete;
        private readonly Action<float, float> _onUpdate;


        public Timer(float duration, Action onComplete, string id, Action<float, float> onUpdate = null,
            TimerType timerType = TimerType.Decrease)
        {
            Duration = duration;
            switch (TimerType)
            {
                case TimerType.Decrease:
                      TimeRemaining  = Duration;
                    break;
                case TimerType.Increase:
                      TimeRemaining =0 ;
                    break;
            }

            TimeRemaining = duration;
            this._onComplete = onComplete;
            this._onUpdate = onUpdate;
            this.Id = id;
        }
 

        public float Duration { get;  }
        public float TimeRemaining { get;   set; }
        public bool IsComplete { get;   set; }
        public bool IsPaused { get; private set; }
        public TimerType TimerType { get; set; }
        public string Id { get;   set; }

        public void Update(float deltaTime)
        {
            if (IsComplete || IsPaused) return;

            switch (TimerType)
            {
                case TimerType.Decrease:
                    TimeRemaining -= deltaTime;
                    break;
                case TimerType.Increase:
                    TimeRemaining += deltaTime;
                    break;
            }

            _onUpdate?.Invoke(deltaTime, TimeRemaining);
 

            if (IsTimerFinished())
            {
                IsComplete = true;
                _onComplete?.Invoke();
            }
        }

        private bool IsTimerFinished()
        {
            switch (TimerType)
            {
                case TimerType.Decrease:
                    return TimeRemaining <= 0;
                case TimerType.Increase:
                    return TimeRemaining >= Duration;
            }

            return true;
        }

        public void Pause()
        {
            IsPaused = true;
        }

        public void Resume()
        {
            IsPaused = false;
        }

        public void Reset()
        {
            switch (TimerType)
            {
                case TimerType.Decrease:
                      TimeRemaining  = Duration;
                    break;
                case TimerType.Increase:
                    TimeRemaining = 0;
                    break;
            }
            IsComplete = false;
            IsPaused = false;
        }
    }
}