using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using UnityEngine;
namespace Game
{
    public class NormalShootHandle : ShootingHandle
    {
        public float CountOverHeat { get => _countOverHeat; }
        private float _countOverHeat;

        public bool IsOverHeat { get => _isOverHeat; }
        private bool _isOverHeat = false;

        //Delay before overheat variable starts to dwindle down
        private const float DELAY_BEFORE_COOLING_SHOOT = 0.75f;

        private bool _shouldDecreaseOverheat = false;
        private bool _shouldIncreaseOverheat = false;

        private Timer _overheatTimer;

        #region UnityEvents
        private void Start()
        {
            CustomStart();
        }
        private void Update()
        {
            if (_shouldDecreaseOverheat)
            {
                ModOverHeat(-Time.deltaTime);
                if (_countOverHeat <= 0)
                {
                    OverheatDone();
                }
            }
  
        }
        #endregion

        protected override void CustomStart()
        {
            base.CustomStart();
            _countOverHeat = 0;
        }
        protected override void SetBulletStats(BulletBehaivour bulletBehaivour)
        {
            base.SetBulletStats(bulletBehaivour);
        }

        public void NormalShooting()
        {
            if (_isOverHeat) return;

            if (_overheatTimer == null)
            {
                _overheatTimer = TimerManager.Instance.StartTimer(characterStatsController.NormalOverHeat, () =>
                {
                    _isOverHeat = true;
                }, (progress) =>
                {
                    _countOverHeat = progress * characterStatsController.NormalOverHeat;
                },
                nameof(NormalShooting),
                false,
                true);
            }


            if (canShoot)
            {
                Vector3 velocity = (currentShootDirection + directionOffset) * characterStatsController.NormalShootSpeed;
                ShootBullet(velocity, characterStatsController.NormalShootMaxRange);
                canShoot = false;
                TimerManager.Instance.StartTimer(characterStatsController.NormalShootCadenceTime,
                        () => { canShoot = true; },
                        null, "CadenceTimeNormalShoot " + characterStatsController.name,
                        false,
                        false);
            }

            
        }

        //When user exits normal shoot state
        public void DecreaseOverheatTime()
        {
            TimerManager.Instance.StartTimer(DELAY_BEFORE_COOLING_SHOOT, () =>
            {
                _shouldDecreaseOverheat = true;
                TimerManager.Instance.SetElapsedTimeToTimer(_overheatTimer, _countOverHeat);
                TimerManager.Instance.ReverseTimer(_overheatTimer);
            },
            null,
            nameof(DecreaseOverheatTime),
            false,
            true);
        }

        //When user enters normal shoot state
        public void StopDelayDecreaseOveheat()
        {
            TimerManager.Instance.StopTimer(nameof(DecreaseOverheatTime));
            TimerManager.Instance.UnreverseTimer(_overheatTimer);
        }

        private void ModOverHeat(float modificator)
        {
            _countOverHeat += modificator;
        }

        private void OverheatDone()
        {
            _isOverHeat = false;
            _shouldDecreaseOverheat = false;
            _overheatTimer = null;
        }

    }
}
