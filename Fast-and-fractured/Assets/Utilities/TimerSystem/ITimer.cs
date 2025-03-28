using System;

namespace Utilities
{
    public interface ITimer
    {
        void UpdateTimer(float deltaTime);
        void StartTimer();
        void StopTimer();
        void PauseTimer();
        void ResumeTimer();
        void InvertTimerDirection();

        void ChangeDirection(TimerDirection newDirection);
        TimerData GetData();
        void ChangeOnComplete(Action onComplete); //No needed any more
    }
}