 

 using System;
 using UnityEngine;

 namespace Utilities
 {
     
 public class DebugTimerDecorator : ITimer
    {
        private readonly ITimer _decoratedTimer;

        public DebugTimerDecorator(ITimer timer)
        {
            _decoratedTimer = timer;
        }

        public void ChangeOnComplete(Action onComplete)
        {
            _decoratedTimer.ChangeOnComplete(onComplete); //Just in case
            Debug.Log($"[DebugTimer - {GetTimerID()}] OnComplete Action Changed.");
        }

        public void UpdateTimer(float deltaTime)
        {
            _decoratedTimer.UpdateTimer(deltaTime);
            Debug.Log(
                $"[DebugTimer - {GetTimerID()}] Update: CurrentTime={_decoratedTimer.GetData().CurrentTime:F2}, Normalized={_decoratedTimer.GetData().NormalizedProgress:P2} Direction={_decoratedTimer.GetData().Direction}");
        }

        public void StartTimer()
        {
            _decoratedTimer.StartTimer();
            Debug.Log($"[DebugTimer - {GetTimerID()}] Started");
        }

        public void StopTimer()
        {
            _decoratedTimer.StopTimer();
            Debug.Log($"[DebugTimer - {GetTimerID()}] Stopped");
        }

        public void PauseTimer()
        {
            _decoratedTimer.PauseTimer();
            Debug.Log($"[DebugTimer - {GetTimerID()}] Paused");
        }

        public void ResumeTimer()
        {
            _decoratedTimer.ResumeTimer();
            Debug.Log($"[DebugTimer - {GetTimerID()}] Resumed");
        }

        public void InvertTimerDirection()
        {
            _decoratedTimer.InvertTimerDirection();
            Debug.Log($"[DebugTimer - {GetTimerID()}] Changed Direction");
        }

        public void ChangeDirection(TimerDirection newDirection)
        {
            _decoratedTimer.ChangeDirection(newDirection);
            Debug.Log($"[DebugTimer - {GetTimerID()}] Direction Changed to {newDirection}");
        }

        public TimerData GetData()
        {
            return _decoratedTimer.GetData();
        }

        private string GetTimerID()
        {
            return _decoratedTimer.GetData().ID;
        }
    }
 }