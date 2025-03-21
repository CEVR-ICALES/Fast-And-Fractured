using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game
{
    public class NormalShootHandle : ShootingHandle
    {
        public float CountOverHeat { get => _countOverHeat; }
        private float _countOverHeat;
        [SerializeField]private Collider ignoredCollider;

        public bool IsOverHeat { get => _isOverHeat; }
        private bool _isOverHeat = false;

        //Delay before overheat variable starts to dwindle down
        private const float DELAY_BEFORE_COOLING_SHOOT = 0.75f;

        private bool _shouldDecreaseOverheat = false;
        private bool _shouldIncreaseOverheat = false;

     [SerializeField]   private TimerSystem.Timer _overheatTimer;
     [FormerlySerializedAs("_decreaseOverheatTimer")] [SerializeField]     private TimerSystem.Timer _delayUntilStartDecreaseTimer;

        #region UnityEvents
        private void Start()
        {
            CustomStart();
        }
        private void Update()
        {
            //if (_shouldDecreaseOverheat)
            //{
            //    ModOverHeat(-Time.deltaTime);
            //    if (_countOverHeat <= 0)
            //    {
            //        OverheatDone();
            //    }
            //}
  
        }
        #endregion

        protected override void CustomStart()
        {
            base.CustomStart();
            _countOverHeat = 0;
            _delayUntilStartDecreaseTimer.Id = null;
            _overheatTimer.Id = null;
        }
        protected override void SetBulletStats(BulletBehaivour bulletBehaivour)
        {
            base.SetBulletStats(bulletBehaivour);
            ((NormalBulletBehaivour)bulletBehaivour).IgnoreCollider = ignoredCollider;
        }
        public float previousCountOverHeat;
        /// <summary>
        /// Send the shoot user collider at the start for the projectiles to ignore it's own collider
        /// </summary>
        public void IgnoreCollider(Collider collider)
        {
            ignoredCollider = collider;
        }

        public void NormalShooting()
        {
            // Debug.Log(_isOverHeat);
            // Debug.Log(_overheatTimer.id);
            if (_isOverHeat)
            {
                return;
            }
            if (_overheatTimer.Id ==null)
            {
                _overheatTimer = TimerSystem.CreateIncreasingTimer(characterStatsController.NormalOverHeat,()=>
                {
                    if (_overheatTimer.TimerType == TimerSystem.TimerType.Increase)
                    {
                        _isOverHeat = true;
                        _shouldDecreaseOverheat = true;
                        DecreaseOverheatTime();
                    } else
                    {
                        OverheatDone();
                    }
                }, (deltaTime,progress) =>
                {
                    previousCountOverHeat = _countOverHeat;
                    _countOverHeat = progress ;
                },
                nameof(NormalShooting) + Guid.NewGuid().ToString());
            }else
            {
                _overheatTimer.TimerType = TimerSystem.TimerType.Increase;
                _overheatTimer.IsComplete = false;
                 TimerSystem.ReAddTimer(_overheatTimer);
            }

            if (canShoot)
            {
                // Debug.Log("can shoot");
                Vector3 velocity = (currentShootDirection + directionCenterOffSet) * characterStatsController.NormalShootSpeed;
                ShootBullet(velocity, characterStatsController.NormalShootMaxRange);
                canShoot = false;
                TimerManager.Instance.StartTimer(characterStatsController.NormalShootCadenceTime,
                        () => { canShoot = true; },
                        null, "CadenceTimeNormalShoot c34" + Guid.NewGuid().ToString(),
                        false,
                        false);
            }

            _shouldIncreaseOverheat = true;
        }

        //When user exits normal shoot state
        public void DecreaseOverheatTime()
        { 
            _shouldIncreaseOverheat = false;
            TimerSystem.PauseTimer(_overheatTimer.Id);
            if (_delayUntilStartDecreaseTimer.Id != null)
            { 
               // TimerManager.Instance.ResumeTimer(_overheatTimer);
               // TimerManager.Instance.ReverseTimer(_overheatTimer);
               // TimerManager.Instance.AddTimer(_overheatTimer);
                return;
                /*
                TimerManager.Instance.StopTimer(_decreaseOverheatTimer);
                _decreaseOverheatTimer = null;
           */ }

            _delayUntilStartDecreaseTimer = TimerSystem.CreateDecreasingTimer(DELAY_BEFORE_COOLING_SHOOT, () =>
                {
                _shouldDecreaseOverheat = true;
//                TimerManager.Instance.SetElapsedTimeToTimer(_overheatTimer, _countOverHeat);
                _overheatTimer.TimeRemaining = _countOverHeat;
                _overheatTimer.TimerType = TimerSystem.TimerType.Decrease;
                _overheatTimer.IsComplete = false;
                _overheatTimer.Resume();
                TimerSystem.ReAddTimer(_overheatTimer);
               // TimerManager.Instance.ReverseTimer(_overheatTimer);
               // TimerManager.Instance.ResumeTimer(_overheatTimer);
               // TimerManager.Instance.AddTimer(_overheatTimer);

               _delayUntilStartDecreaseTimer.Id = null;
            },
            null,
            nameof(DecreaseOverheatTime) +"c34"+ Guid.NewGuid().ToString()
            );
         }

        //When player exits normal shoot state
        public void PauseOverheatTime()
        {
             // TimerSystem.PauseTimer(_overheatTimer.Id);
//            TimerManager.Instance.PauseTimer(_overheatTimer);
        }

        //When user enters normal shoot state
        public void StopDelayDecreaseOveheat()
        {
            _shouldDecreaseOverheat = false;
            _shouldIncreaseOverheat = true;
            if (_delayUntilStartDecreaseTimer.Id!=null)
            {
                TimerSystem.RemoveTimer(_delayUntilStartDecreaseTimer);
                _delayUntilStartDecreaseTimer.Id=null;   
                
            }
            if (_overheatTimer.Id != null && !_isOverHeat) 
            { 
                _overheatTimer.TimerType = TimerSystem.TimerType.Increase;
              TimerSystem.ResumeTimer(_overheatTimer.Id);
                // TimerManager.Instance.UnreverseTimer(_overheatTimer);
               // TimerManager.Instance.ResumeTimer(_overheatTimer);
              //  TimerManager.Instance.AddTimer(_overheatTimer);
            }
        }

        private void ModOverHeat(float modificator)
        {
            _countOverHeat += modificator;
        }

        private void OverheatDone()
        {
            _isOverHeat = false;
            _shouldDecreaseOverheat = false; 
        }

    }
}
