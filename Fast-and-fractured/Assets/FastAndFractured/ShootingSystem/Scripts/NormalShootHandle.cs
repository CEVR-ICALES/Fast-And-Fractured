// RUTA: Assets/FastAndFractured/ShootingSystem/Scripts/NormalShootHandle.cs

using System;
using UnityEngine;
using UnityEngine.Events;
using Utilities;
using Enums;

namespace FastAndFractured
{
    public class NormalShootHandle : ShootingHandle
    {
        public UnityEvent<float, float> onOverheatUpdate;
        public bool isInState;

        public float CountOverHeat => _countOverHeat;
        private float _countOverHeat;
        public float previousCountOverHeat;

        public Transform ShootPoint => shootPoint;
        public bool IsOverHeat => _isOverHeat;
        private bool _isOverHeat = false;

        private const float DELAY_BEFORE_COOLING_SHOOT = 2f;

        private ITimer _overheatTimer;
        private string _delayUntilStartDecreaseTimerId;

        protected override void Start()
        {
            base.Start();
            _countOverHeat = 0;
        }

        public void NormalShooting()
        {
            if (_isOverHeat || !canShoot) return;

            Vector3 shootingDirection = currentShootDirection + directionCenterOffSet;
            float angle = Vector3.Angle(transform.forward, shootingDirection);

            if (angle > characterStatsController.NormalShootAngle / 2) return;

            Vector3 velocity = shootingDirection * characterStatsController.NormalShootSpeed;

            ShootBullet(velocity, characterStatsController.NormalShootMaxRange);

            canShoot = false;
            TimerSystem.Instance.CreateTimer(characterStatsController.NormalShootCadenceTime,
                TimerDirection.INCREASE, () => { canShoot = true; });
        }

        #region CALLBACKS
        private void OnOverheatComplete()
        {
            _isOverHeat = true;
            DecreaseOverheatTime();
        }

        private void OnCoolingComplete()
        {
            _isOverHeat = false;
            _overheatTimer = null;
        }

        private void OnOverHeatUpdateIncrease(float currentTimerValue)
        {
            previousCountOverHeat = _countOverHeat;
            _countOverHeat = currentTimerValue;
            onOverheatUpdate?.Invoke(currentTimerValue, characterStatsController.NormalOverHeat);
        }

        private void OnOverHeatUpdateDecrease(float currentTimerValue)
        {
            _countOverHeat = currentTimerValue;
            onOverheatUpdate?.Invoke(currentTimerValue, characterStatsController.NormalOverHeat);
        }
        #endregion

        public void DecreaseOverheatTime()
        {
            if (_overheatTimer == null) return;
            if (_overheatTimer.GetData().IsRunning && !_isOverHeat && isInState)
            {
                TimerSystem.Instance.PauseTimer(_overheatTimer.GetData().ID);
            }
            if (string.IsNullOrEmpty(_delayUntilStartDecreaseTimerId))
            {
                _delayUntilStartDecreaseTimerId = TimerSystem.Instance.CreateTimer(DELAY_BEFORE_COOLING_SHOOT,
                    TimerDirection.DECREASE, onTimerDecreaseComplete: () =>
                    {
                        if (_overheatTimer != null)
                        {
                            _overheatTimer = TimerSystem.Instance.CreateTimer(_overheatTimer);
                            TimerSystem.Instance.ModifyTimer(_overheatTimer, newDirection: TimerDirection.DECREASE, isRunning: true);
                            _overheatTimer.ResumeTimer();
                        }
                        _delayUntilStartDecreaseTimerId = string.Empty;
                    }).GetData().ID;
            }
            isInState = false;
        }

        public void StopDelayDecreaseOverheat()
        {
            if (_isOverHeat) return;
            if (!string.IsNullOrEmpty(_delayUntilStartDecreaseTimerId))
            {
                TimerSystem.Instance.StopTimer(_delayUntilStartDecreaseTimerId);
                _delayUntilStartDecreaseTimerId = string.Empty;
            }
            if (_overheatTimer == null)
            {
                _overheatTimer = TimerSystem.Instance.CreateTimer(characterStatsController.NormalOverHeat,
                    TimerDirection.INCREASE,
                    onTimerIncreaseComplete: OnOverheatComplete,
                    onTimerDecreaseComplete: OnCoolingComplete,
                    onTimerIncreaseUpdate: OnOverHeatUpdateIncrease,
                    onTimerDecreaseUpdate: OnOverHeatUpdateDecrease);
            }
            else
            {
                if (_overheatTimer.GetData().IsRunning)
                {
                    TimerSystem.Instance.ModifyTimer(_overheatTimer, newDirection: TimerDirection.INCREASE, isRunning: true);
                }
                else
                {
                    TimerSystem.Instance.CreateTimer(_overheatTimer);
                }
                TimerSystem.Instance.ResumeTimer(_overheatTimer.GetData().ID);
            }
            isInState = true;
        }
    }
}