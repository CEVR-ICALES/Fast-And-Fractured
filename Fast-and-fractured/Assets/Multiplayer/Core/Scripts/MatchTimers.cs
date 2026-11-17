using System;
using UnityEngine;
using Utilities;

namespace FastAndFractured.Multiplayer
{
    public static class MatchTimers
    {
        private const string MISSING_TIMER_SYSTEM = "[MatchTimers] No TimerSystem in the scene: the delayed action will not run.";

        public static ITimer After(float seconds, Action onComplete)
        {
            var timers = TimerSystem.ExistingInstance;
            if (timers == null)
            {
                Debug.LogWarning(MISSING_TIMER_SYSTEM);
                return null;
            }
            return timers.CreateTimer(Mathf.Max(0f, seconds), onTimerDecreaseComplete: onComplete);
        }

        public static bool IsRunning(ITimer timer)
        {
            var timers = TimerSystem.ExistingInstance;
            return timer != null && timers != null && timers.HasTimer(timer) && timer.GetData().IsRunning;
        }

        public static void Stop(ref ITimer timer)
        {
            var timers = TimerSystem.ExistingInstance;
            if (timer != null && timers != null && timers.HasTimer(timer))
                timers.StopTimer(timer.GetData().ID);
            timer = null;
        }
    }
}
