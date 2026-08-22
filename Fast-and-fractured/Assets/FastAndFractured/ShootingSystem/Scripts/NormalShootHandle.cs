using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Utilities;
using Enums;
using UnityEngine.UIElements;
using Unity.Cinemachine;

namespace FastAndFractured
{
    public class NormalShootHandle : ShootingHandle
    {
        #region VARIABLES
        public UnityEvent<float, float> onOverheatUpdate;
        public bool isInState;
        public float CountOverHeat
        {
            get => _countOverHeat;
        }

        private float _countOverHeat;
        public float previousCountOverHeat;
        [SerializeField] private Collider ignoredCollider;
        private Vector3 _lastShootingDirection;
        public Transform ShootPoint => shootPoint;
        public bool IsOverHeat
        {
            get => _isOverHeat;
        }

        private bool _isOverHeat = false;

        //Delay before overheat variable starts to dwindle down
        private const float DELAY_BEFORE_COOLING_SHOOT = 2f;

        private ITimer _overheatTimer;
        private string _delayUntilStartDecreaseTimerId;
        [SerializeField]
        private ParticleSystem shootingTurretVFX;
        [SerializeField]
        private ParticleSystem overHeatSmokeVFX;
        [SerializeField]
        private float _overHeathMaxEmissionRate = 50;
        [SerializeField]
        private float _overHeathMinEmissionRate = 0;
        private float _overHeathEmissionRateXTime;
        [SerializeField]
        private ScreenShakeSourceController screenShakeSourceController;
        #endregion

        #region UNITY_EVENTS

        protected override void Start()
        {
            base.Start();
            _countOverHeat = 0;
            DesactivateShootingVFX(shootingTurretVFX);
            DesactivateShootingVFX(overHeatSmokeVFX);
            _overHeathEmissionRateXTime = (_overHeathMaxEmissionRate - _overHeathMinEmissionRate)/characterStatsController.NormalOverHeat;
        }

        #endregion

        #region OVERRIDE_METHODS

        protected override void SetBulletStats(BulletBehaviour bulletBehaivour)
        {
            base.SetBulletStats(bulletBehaivour);
            ((NormalBulletBehaviour)bulletBehaivour).IgnoreCollider = ignoredCollider;
        }

        #endregion


        public void IgnoreCollider(Collider collider)
        {
            ignoredCollider = collider;
        }

        public void NormalShooting()
        {
            if (_isOverHeat)
            {
                return;
            }

            if (canShoot)
            {
                screenShakeSourceController.PlayGlobalShakeFromProfile(ScreenShakeProfileType.NormalShoot);
                ActiveShootingVFX(shootingTurretVFX);
                Vector3 shootingDirection =  currentShootDirection + directionCenterOffSet;
                float angle = Vector3.Angle(shootingDirection, transform.forward);
                if (angle > characterStatsController.NormalShootAngle)
                {
                  float signedAngle = Vector3.SignedAngle(shootingDirection,transform.forward,Vector3.up);
                  float signedLimitAngle = characterStatsController.NormalShootAngle * Mathf.Sign(-signedAngle);
                  Vector3 forwardVectorToLimitAngle = Quaternion.AngleAxis(signedLimitAngle,Vector3.up)*transform.forward;
                  Vector3 limitedShootingDirection = forwardVectorToLimitAngle + (Vector3.up*shootingDirection.y);
                  shootingDirection = limitedShootingDirection;
                }

                Vector3 velocity = shootingDirection * characterStatsController.NormalShootSpeed;

                ShootBullet(velocity, characterStatsController.NormalShootMaxRange);

                canShoot = false;

                TimerSystem.Instance.CreateTimer(characterStatsController.NormalShootCadenceTime,
                    TimerDirection.INCREASE,
                    () => { canShoot = true; }
                );
            }

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
            ChangeCurrentOverHeathVFX();
            onOverheatUpdate?.Invoke(currentTimerValue, characterStatsController.NormalOverHeat);
        }

        private void OnOverHeatUpdateDecrease(float currentTimerValue)
        {
            _countOverHeat = currentTimerValue;
            ChangeCurrentOverHeathVFX(-1);
            onOverheatUpdate?.Invoke(currentTimerValue, characterStatsController.NormalOverHeat);
        }

        #endregion

        //When user exits normal shoot state
        public void DecreaseOverheatTime()
        {
            if (_overheatTimer != null)
            {
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

                                _overheatTimer.ResumeTimer(); //And Call All The Resumes 

                            }

                            _delayUntilStartDecreaseTimerId = string.Empty;
                        }).GetData().ID;

                }

                isInState = false;
            }
        }

        //When user enters normal shoot state
        public void StopDelayDecreaseOverheat()
        {
            //If is overheated, don't stop the decrease
            if (_isOverHeat) return;

            if (!string.IsNullOrEmpty(_delayUntilStartDecreaseTimerId))
            {
                TimerSystem.Instance.StopTimer(
                    _delayUntilStartDecreaseTimerId);
                _delayUntilStartDecreaseTimerId = string.Empty;
            }

            if (_overheatTimer == null)
            {
                _overheatTimer = TimerSystem.Instance.CreateTimer(characterStatsController.NormalOverHeat,
                    TimerDirection.INCREASE,
                    onTimerIncreaseComplete: OnOverheatComplete,
                    onTimerDecreaseComplete: OnCoolingComplete,
                    onTimerIncreaseUpdate: OnOverHeatUpdateIncrease,
                    onTimerDecreaseUpdate: OnOverHeatUpdateDecrease
                );
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

        private void ActiveShootingVFX(ParticleSystem particleSystem)
        {
            if(particleSystem!=null&&particleSystem.isStopped){
            particleSystem.Play();
            }
        }
        private void DesactivateShootingVFX(ParticleSystem particleSystem)
        {
            if (particleSystem != null&&particleSystem.isPlaying)
            {
                particleSystem.Stop();
            }
        }

        private void ChangeCurrentOverHeathVFX(float timeDirection = 1)
        {
            if(overHeatSmokeVFX==null)
                return;
            ActiveShootingVFX(overHeatSmokeVFX);
            var overHeatEmission = overHeatSmokeVFX.emission;
            var overHeatRateOverTime = overHeatEmission.rateOverTime;
            overHeatRateOverTime.constant+= timeDirection*_overHeathEmissionRateXTime*Time.deltaTime;
            overHeatRateOverTime.constant = Mathf.Clamp(overHeatRateOverTime.constant,_overHeathMinEmissionRate,_overHeathMaxEmissionRate);
            overHeatEmission.rateOverTime = overHeatRateOverTime;
            if (overHeatRateOverTime.constant == _overHeathMinEmissionRate)
            {
                DesactivateShootingVFX(overHeatSmokeVFX);
            }
            
        }
    }
}