using System;

public class Timer
{
    public float duration;
    public float elapsedTime;
    public Action callback;
    public Action<float> timerUpdate;
    public bool isRepeating;
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
    }

    public float getProgress()
    {
        //return duration > 0 ? duration / elapsedTime : 1f;
        return elapsedTime / duration;
    }
}
