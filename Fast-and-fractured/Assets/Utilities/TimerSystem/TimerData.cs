using System;
using UnityEngine;
using Enums;

namespace Utilities
{
    public class TimerData
    {
        public string ID { get; private set; }
        public float Duration { get; set; }
        public float CurrentTime { get; set; }
        public bool IsRunning { get; set; }
        public bool IsPaused { get; set; }
        public float SpeedMultiplier { get; set; }
        public TimerDirection Direction { get; set; }

        public Action OnTimerComplete { get; set; }


        public Action<float> OnTimerUpdate { get; set; }
        public Action OnTimerIncreaseComplete { get; set; }

        public Action OnTimerDecreaseComplete { get; set; }

        public Action<float> OnTimerIncreaseUpdate { get; set; }

        public Action<float>
            OnTimerDecreaseUpdate
        {
            get;
            set;
        }

        public Action OnTimerPause { get; set; }
        public Action OnTimerResume { get; set; }

        public TimerData(string id, float duration, TimerDirection direction, Action onTimerComplete,
            Action<float> onTimerUpdate)
        {
            ID = id;
            Duration = duration;
            Direction = direction;
            OnTimerComplete = onTimerComplete;
            OnTimerUpdate =
                onTimerUpdate;

            CurrentTime = (direction == TimerDirection.INCREASE) ? 0 : duration;
            IsRunning = false;
            IsPaused = false;
            SpeedMultiplier = 1;
        }

        public void SetTimerDirection(TimerDirection newDirection)
        {
            Direction = newDirection;

        }

        public float NormalizedProgress
        {
            get
            {
                if (Duration <= 0) return 1f;
                return (Direction == TimerDirection.INCREASE)
                    ? Mathf.Clamp01(CurrentTime / Duration)
                    : Mathf.Clamp01(1 - (CurrentTime / Duration));
            }
        }


        public TimerDirection InvertDirection()
        {
            Direction = (Direction == TimerDirection.INCREASE) ? TimerDirection.DECREASE : TimerDirection.INCREASE;
            return Direction;
        }
        public string GetFormattedTime(TimeFormatStyle style)
        {
            switch (style)
            {
                case TimeFormatStyle.MINUTES_SECONDS:
                    var tsMs = TimeSpan.FromSeconds(CurrentTime);
                    return $"{tsMs.Minutes:D2}:{tsMs.Seconds:D2}";

                case TimeFormatStyle.HOURS_MINUTES_SECONDS:
                    var totalHours = (int)Math.Floor(TimeSpan.FromSeconds(CurrentTime).TotalHours);
                    var ts = TimeSpan.FromSeconds(CurrentTime);
                    return $"{totalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";

                default:
                    throw new ArgumentOutOfRangeException(nameof(style), style, null);
            }
        }
        public override string ToString()
        {
            return GetFormattedTime(TimeFormatStyle.HOURS_MINUTES_SECONDS);
        }
    }
}
public enum TimeFormatStyle
{
    MINUTES_SECONDS,
    HOURS_MINUTES_SECONDS
}