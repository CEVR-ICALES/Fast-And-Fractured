using System;
public class Timer
{
    public float duration;
    public float elapsedTime;
    public Action callback;
    public Action<float> timerUpdate;
    public bool isRepeating;
    public bool isPaused;
    public bool isReversed;
    public string id;

    public Timer(float duration, Action callback, Action<float> timerUpdate, bool isRepating, string id)
    {
        Reset(duration, callback, timerUpdate, isRepating, id);
    }

    public void Reset(float duration, Action callback, Action<float> timerUpdate, bool isRepating, string id)
    {
        this.duration = duration;
        elapsedTime = 0;
        this.callback = callback;
        this.timerUpdate = timerUpdate;
        this.isRepeating = isRepating;
        this.id = id;
        isPaused = false;
        isReversed = false;
    }

    public float GetProgress()
    {
        //return duration > 0 ? duration / elapsedTime : 1f;
        return elapsedTime / duration;
    }
    public void SetElapsedTime(float newElapsedTime)
    {
        elapsedTime = newElapsedTime;
    }
    public void Pause()
    {
        isPaused = true;
    }

    public void Resume()
    {
        isPaused = false;
    }

    public void Reverse()
    {
        isReversed = true;
    }

    public void Unreverse()
    {
        isReversed = false;
    }

    public override string ToString()
    {
        return "Timer: " + id + " Duration: " + duration + " ElapsedTime: " + elapsedTime + " IsRepeating: " + isRepeating + " IsPaused: " + isPaused + " IsReversed: " + isReversed ;
    }
}
