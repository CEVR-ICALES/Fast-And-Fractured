using System;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Linq;
using UnityEngine;
using Utilities.Managers.PauseSystem;
using Enums;

namespace Utilities
{
    public class TimerSystem : AbstractSingleton<TimerSystem> , IPausable
    {
        private Dictionary<string, ITimer> _timers = new Dictionary<string, ITimer>();

        private int _nextTimerId = 0;


        #region UNITY_LIFECYCLE

        protected override void Awake()
        {
            base.Awake(); 
        }

        private void Start()
        {
            PauseManager.Instance.RegisterPausable(this);
        }

        private void Update()
        {
            UpdateAllTimers(Time.deltaTime);
        }

        private void OnDestroy()
        {
            _timers.Clear();
            PauseManager.Instance.UnregisterPausable(this); 
        }
        #endregion

        #region TIMER_MANAGEMENT

        private void UpdateAllTimers(float deltaTime)
        {
            List<string> timersToRemove = new List<string>();
            for (int i = 0; i < _timers.Count; i++)
            {
                var timerPair = _timers.ElementAt(i);
                timerPair.Value.UpdateTimer(deltaTime);
                if (!timerPair.Value.GetData().IsRunning)
                {
                    timersToRemove.Add(timerPair.Key);
                }
            }

            for (int i = 0; i < timersToRemove.Count; i++)
            {
                _timers.Remove(timersToRemove[i]);
            }
        }


        public ITimer CreateTimer(float duration, TimerDirection direction= TimerDirection.Decrease, Action onTimerIncreaseComplete = null,
            Action onTimerDecreaseComplete = null, bool isDebug = false, Action<float> onTimerIncreaseUpdate = null,
            Action<float> onTimerDecreaseUpdate = null, Action onTimerPause = null, Action onTimerResume = null)
        {
            string id = GenerateTimerId();

            TimerData data = new TimerData(id, duration, direction, null, null);

            ITimer timer = new Timer(data);

            data.OnTimerIncreaseComplete =
                onTimerIncreaseComplete;
            data.OnTimerDecreaseComplete =
                onTimerDecreaseComplete;
            data.OnTimerIncreaseUpdate =
                onTimerIncreaseUpdate;
            data.OnTimerDecreaseUpdate =
                onTimerDecreaseUpdate;

            data.OnTimerPause =
                onTimerPause;
            data.OnTimerResume = onTimerResume;
            if (isDebug)
            {
                timer = new DebugTimerDecorator(timer);
            }

            _timers.Add(id, timer);
            timer.StartTimer();

            return timer;
        }


        public ITimer CreateTimer(ITimer existingTimer, Action onTimerIncreaseComplete = null,
            Action onTimerDecreaseComplete = null, bool isDebug = false, Action<float> onTimerIncreaseUpdate = null,
            Action<float> onTimerDecreaseUpdate = null, Action onTimerPause = null, Action onTimerResume = null)
        { 
            if (existingTimer == null)
            {
                Debug.LogError(
                    "Cannot re-add a null timer.  Use the non-overloaded CreateTimer method.");  

                return null;
            }


            if (onTimerIncreaseComplete != null)
            {
                existingTimer.GetData().OnTimerIncreaseComplete = onTimerIncreaseComplete;
            }

            if (onTimerDecreaseComplete != null)
            {
                existingTimer.GetData().OnTimerDecreaseComplete = onTimerDecreaseComplete;
            }

            if (onTimerIncreaseUpdate != null)
            {
                existingTimer.GetData().OnTimerIncreaseUpdate = onTimerIncreaseUpdate;
            }

            if (onTimerDecreaseUpdate != null)
            {
                existingTimer.GetData().OnTimerDecreaseUpdate = onTimerDecreaseUpdate;
            }

            if (onTimerPause != null)
            {
                existingTimer.GetData().OnTimerPause = onTimerPause;
            }

            if (onTimerResume != null)
            {
                existingTimer.GetData().OnTimerResume = onTimerResume;
            }

            ITimer
                timer = existingTimer;  
            if (isDebug)
            {
                timer = new DebugTimerDecorator(existingTimer);
            } 


            if (_timers.ContainsKey(existingTimer.GetData().ID))
            {
                _timers.Remove(existingTimer.GetData().ID);
            }

            _timers.Add(timer.GetData().ID, timer); 

            timer.StartTimer();  

            return timer;
        }


        //Allows changes Using the timer Reference itself
        public bool ModifyTimer(ITimer timer, float? newDuration = null, Action newOnTimerComplete = null,
            TimerDirection? newDirection = null, float? newCurrentTime = null, bool isRunning = true)
        {
            if (!_timers.ContainsKey(timer.GetData().ID))   
            {
                Debug.LogError(
                    $"Tried Modifing time values. Timer you tried Modifiy not Found. If Is not debug and still getting  messages in console probably Is an old Timer ID. If is an User Called Method make sure call StopTimer()");
                return false;
            }

            if (newDuration.HasValue)
            {
                timer.GetData().Duration = newDuration.Value;
            }

            if (newOnTimerComplete != null) 
            {
                timer.GetData().OnTimerComplete = newOnTimerComplete;
            }


            if (newDirection.HasValue)
            {
                timer.GetData().SetTimerDirection(newDirection.Value);  
            }

            if (newCurrentTime
                .HasValue) 
            {
                timer.GetData().CurrentTime =
                    newCurrentTime.Value; 
            }

            if (isRunning != timer.GetData().IsRunning)
            {
                if (isRunning)
                {
                    timer.StartTimer();
                }
                else
                {
                    timer.StopTimer();
                }
            }

            {
            }

            _timers.TryAdd(timer.GetData().ID, timer);
            return true; 
        }


        public ITimer GetTimer(string timerId)
        {
         
            if (string.IsNullOrEmpty(timerId))
            {
                Debug.LogError(
                    $"Called GetTimer But TimerId is Empty  check your  _timerID you called. If Is not debug probably an old TimeId called with out keeping track, the App Will Continue Running"); //Log precise

                return null;
            }


            if (_timers.TryGetValue(timerId, out ITimer timer))
            {
                return timer;
            }
            else
            {
                Debug.LogError(
                    $"Timer with ID '{timerId}' not found."); 
                return null;
            }
        }
        
        public bool HasTimer(ITimer timer)
        {
            return timer != null && _timers.ContainsKey(timer.GetData().ID);
        }
        
        public void StartTimer(string timerId)
        {
            if (_timers.TryGetValue(timerId, out ITimer timer))
            {
                timer.StartTimer(); 
            }
            else
            {
                 Debug.LogError(
                    $"Called StarTimer() , but Time Id not exist, Make Sure ID  Time your trying is Called after you CreateTime. Avoid Called In other Class Star(). If not Check for another Timer.App Still Works."); //Precise Error Log. Clear Where we should Call startimer
            }
        }

        
        public void StopTimer(string timerId)
        {
            ITimer timer = GetTimer(timerId);

            if (timer != null) 

            {
                timer.StopTimer();

                _timers.Remove(timerId); 
            }
        }


        public void PauseTimer(string timerId) 
        {
            if (_timers.TryGetValue(timerId,
                    out ITimer timer)) 
            {
                timer.PauseTimer(); 
            }
            else
            {
                Debug.LogError(
                    $" Called Pause Timer, with  OldTimer Id that No longer Exist on TimeSyste,_timers. To prevent this erros . Avoid Call  Id Timers If the user itself Called Timer Stop(),   You must Handle keeping track Correct TimerID values  "); // Clear Message where we call Pause;
            }
        }

        public void ResumeTimer(string timerId)
        {
            if (_timers.TryGetValue(timerId, out ITimer timer))
            {
                timer.ResumeTimer();
            }
            else
            {
                Debug.LogError(
                    $" Called Resume() But TimeID value probably Is old and not keep Tracking in Dictionary. Probably And old  Stop Timer Id() you must Handle correctly. Still Safe Call and Program Continues Running.");
            }
        }


        public void ChangeTimerDirection(string timerId) 

        {
            if (_timers.TryGetValue(timerId, out ITimer timer))  

            {
                timer.InvertTimerDirection();  
            }
            else

            {
                Debug.LogError(
                    $"Tried Inverted Timer using And ID Timer than probably is already deleted on SystemTimers.Probably AN stop called , If the error keep occurring. Do not call InvertTimer with Stop timers  by Id(), keep using Stop()");
            }
        }

        #endregion


        #region HELPER_METHODS

        private string GenerateTimerId()
        {
            return $"Timer_{_nextTimerId++}";
        }

        #endregion

        public void OnPause()
        {
            foreach (var timer in _timers)
            {
                timer.Value.PauseTimer();
            }
        }

        public void OnResume()
        {
            foreach (var timer in _timers)
            {
                timer.Value.ResumeTimer();
            }        }
    }

   

     
}
 

//public enum TimerDirection
//{
//    Increase,
//    Decrease
//}