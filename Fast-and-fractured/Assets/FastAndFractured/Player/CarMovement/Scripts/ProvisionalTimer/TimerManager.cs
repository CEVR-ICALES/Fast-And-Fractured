using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimerManager : MonoBehaviour
{
    public static TimerManager Instance { get; private set; }
    private LinkedList<Timer> timersPool = new LinkedList<Timer>();
    private LinkedList<Timer> activeTimers = new LinkedList<Timer>();
    private Dictionary<string, Timer> accessibleTimers = new Dictionary<string, Timer>();
    [SerializeField] private int maxPoolSize;
    [SerializeField] private int initalPoolSize;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
            InitializePool();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializePool()
    {
        for (int i = 0; i < initalPoolSize; i++)
        {
            var Timer = new Timer(0, null, null, false, "");
            timersPool.AddLast(Timer);
        }
    }
    public void AddTimer(Timer timer)
    {
        if (timer != null  && !activeTimers.Contains(timer))
        {
            activeTimers.AddLast(timer);
        }
        else
        {
            Debug.LogWarning("Trying to add a null timer");
        }
    }
    public Timer StartTimer(float duration, Action callback, Action<float> timerUpdate, string id, bool isRepating = false, bool isAccessible = false)
    {
        Timer timer;
        if (isAccessible && accessibleTimers.ContainsKey(id))
        {
            Debug.LogWarning($"A timer with the identifier : {id} already exists");
            return null;
        }

        if (timersPool.Count > 0) //there is aveliable timers in the opool
        {
            var node = timersPool.First;
            timer = node.Value;
            timersPool.RemoveFirst();
            timer.Reset(duration, callback, timerUpdate, isRepating, id);
        }
        else if (timersPool.Count + 1 < maxPoolSize) //there are no aveliable timers on the pool creates a new one if it doesnt exceed the maxLimit
        {
            timer = new Timer(duration, callback, timerUpdate, isRepating, id);
            timersPool.AddLast(timer);
            Debug.Log($"Expanding pool size to {timersPool.Count + 1}");
        }
        else
        {
            Debug.LogWarning($"Timer pool size reached its limit: {maxPoolSize}");
            return null;
        }

        if (isAccessible && !accessibleTimers.ContainsKey(id))
        {
            accessibleTimers.Add(id, timer);
        }
        activeTimers.AddLast(timer);
        return timer;
    }

    public void PauseTimer(string id)
    {
        if (accessibleTimers.TryGetValue(id, out var timer))
        {
            timer.Pause(); ;
        }
        else
        {
            Debug.LogWarning($"{id} is not accessible");
        }
    }

    public void PauseTimer(Timer timer)
    {
        if (timer != null)
        {
            timer.Pause(); ;
        }
        else
        {
            Debug.LogWarning("Trying to pause a null timer");
        }
    }

    public void ResumeTimer(string id)
    {
        if (accessibleTimers.TryGetValue(id, out var timer))
        {
            timer.Resume(); ;
        }
        else
        {
            Debug.LogWarning($"{id} is not accessible");
        }
    }

    public void ResumeTimer(Timer timer)
    {
        if (timer != null)
        {
            timer.Resume(); ;
        }
        else
        {
            Debug.LogWarning("Trying to resume a null timer");
        }
    }

    public void StopTimer(string id)
    {
        if (accessibleTimers.TryGetValue(id, out var timer))
        {
            RemoveTimer(timer);
        }
        else
        {
            Debug.LogWarning($"{id} is not accessible");
        }
    }

    public void StopTimer(Timer timer)
    {
        if (timer != null)
        {
            RemoveTimer(timer);
        }
        else
        {
            Debug.LogWarning("Trying to stop a null timer");
        }
    }
    public void SetElapsedTimeToTimer(string id,float newElapsedTime) {

        if (accessibleTimers.TryGetValue(id, out var timer))
        {
            timer.SetElapsedTime(newElapsedTime);
        }
        else
        {
            Debug.LogWarning($"{id} is not accessible");
        }
    }
    public void SetElapsedTimeToTimer(Timer timer, float newElapsedTime)
    {
        if (timer != null)
        {
            timer.SetElapsedTime(newElapsedTime);
        }
        else
        {
            Debug.LogWarning("Trying to modify the elapsed time of a null timer");
        }
    }

    public void ReverseTimer(string id)
    {
        if (accessibleTimers.TryGetValue(id, out var timer))
        {
            timer.Reverse();
        }
        else
        {
            Debug.LogWarning($"{id} is not accessible");
        }
    }
    public void ReverseTimer(Timer timer)
    {
        if (timer != null)
        {
            timer.Reverse();
        }
        else
        {
            Debug.LogWarning("Trying to reverse a null timer");
        }
    }

    public void UnreverseTimer(string id)
    {
        if (accessibleTimers.TryGetValue(id, out var timer))
        {
            timer.Unreverse();
        }
        else
        {
            Debug.LogWarning($"{id} is not accessible");
        }
    }
    public void UnreverseTimer(Timer timer)
    {
        if (timer != null)
        {
            timer.Unreverse();
        }
        else
        {
            Debug.LogWarning("Trying to unreverse a null timer");
        }
    }


    private void RemoveTimer(Timer timer)
    {
        activeTimers.Remove(timer);
        if (accessibleTimers.ContainsKey(timer.id))
        {
            accessibleTimers.Remove(timer.id);
        }
        timersPool.AddLast(timer);
    }

    

    private void Update()
    {
        foreach (var timer in new LinkedList<Timer>(activeTimers)) //create a copy to ensure safety while iteratoin over the list
        {
            if (timer.isPaused) continue;

            if (timer.isReversed)
            {
                timer.elapsedTime -= Time.deltaTime;
            }
            else
            {
                timer.elapsedTime += Time.deltaTime;
            }

            timer.timerUpdate?.Invoke(timer.GetProgress());//call progress if it has content on it
            if (  timer.id.Contains("c344"))
            {
                Debug.Log(timer.ToString());
            }

            bool finished = timer.isReversed ? timer.elapsedTime <= 0 : timer.elapsedTime >= timer.duration;

            if (finished)
            {
                if (timer.isRepeating)
                {
                    timer.elapsedTime = 0;
                }
                else
                {
                    RemoveTimer(timer);
                }
                timer.callback?.Invoke(); //the ? (non-conditional operator) this expression evaluates to null if no callback has been assigned to prevent NullReferences

            }
        }
    }

    private void OnDestroy()
    {
        if (activeTimers == null) return;
        activeTimers.Clear();
        accessibleTimers.Clear();
    }
}
