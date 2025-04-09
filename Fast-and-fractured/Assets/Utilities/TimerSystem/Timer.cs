using System;
using UnityEngine;
using Enums;

namespace Utilities
{
    public class Timer : ITimer
    {
        protected TimerData _data; 


        public void ChangeOnComplete(Action onComplete) //NO Needed Anymore
        {
            _data.OnTimerComplete = onComplete;
        }

        public Timer(TimerData data)
        {
            _data = data;
        }

        public virtual void UpdateTimer(float deltaTime)
        {
            if (!_data.IsRunning || _data.IsPaused) return;

            var speedMultiplier = GetData().SpeedMultiplier;
            _data.CurrentTime += (_data.Direction == TimerDirection.INCREASE) ? deltaTime * speedMultiplier : -deltaTime * speedMultiplier;


            if (_data.Direction == TimerDirection.INCREASE)
            {
                _data.OnTimerIncreaseUpdate
                    ?.Invoke(_data.CurrentTime);  

                if (_data.CurrentTime >= _data.Duration)
                {
                    _data.CurrentTime = Mathf.Clamp(_data.CurrentTime, 0, _data.Duration);
                    StopTimer();
                    _data.OnTimerIncreaseComplete?.Invoke();  
                    return;  
                }
            }

            if (_data.Direction == TimerDirection.DECREASE)
            {
                _data.OnTimerDecreaseUpdate?.Invoke(_data.CurrentTime);  

                if (_data.CurrentTime <= 0)
                {
                    _data.CurrentTime = Mathf.Clamp(_data.CurrentTime, 0, _data.Duration);
                    StopTimer();
                    _data.OnTimerDecreaseComplete?.Invoke();  
                }
            }
        }

        public virtual void StartTimer()
        {
            _data.IsRunning = true;
            _data.IsPaused = false;
        }

        public virtual void StopTimer()
        {
            _data.IsRunning = false;
            _data.IsPaused = false;
        }
 
        public virtual void PauseTimer()
        {
            if (_data.IsRunning)
            {
                _data.IsPaused = true;
                _data.OnTimerPause?.Invoke();
            }
        }

        public virtual void ResumeTimer()
        {
            if (_data.IsRunning)
            {
                _data.IsPaused = false;
                _data.OnTimerResume?.Invoke();
            }
        }

        public virtual void InvertTimerDirection()
        {
            _data.SetTimerDirection(_data.InvertDirection());
        }

        public void ChangeDirection(TimerDirection newDirection)
        {
            _data.Direction = newDirection;
        }

        public TimerData GetData()
        {
            return _data;
        }
    }
}