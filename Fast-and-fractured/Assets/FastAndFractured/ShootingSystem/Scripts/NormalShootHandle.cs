using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Game
{
    public class NormalShootHandle : ShootingHandle
    {
        public float CountOverHeat { get => _countOverHeat; }
        private float _countOverHeat;
        public bool IsOverHeat { get => _isOverHeat; }
        private bool _isOverHeat = false;

        #region UnityEvents
        private void Start()
        {
            CustomStart();
        }
        private void Update()
        {

            //In the real method, the overHeat should be reduce after some time
            if (!Input.GetKey(KeyCode.Mouse0))
            {
                    if (!Timer(ref _countOverHeat, _countOverHeat <= 0, 0))
                    {
                        ModOverHeat(-Time.deltaTime);
                    }
                    else
                    {
                        _isOverHeat = false;
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
            if (!Timer(ref _countOverHeat, _countOverHeat >= characterStatsController.NormalOverHead,
                characterStatsController.NormalOverHead) && !_isOverHeat)
            {
                if (canShoot)
                {
                    Debug.Log("Shoot");
                    Vector3 velocity = (currentShootDirection + directionOffset) * characterStatsController.NormalShootSpeed;
                    ShootBullet(velocity, characterStatsController.NormalShootMaxRange);
                    canShoot = false;
                    TimerManager.Instance.StartTimer(characterStatsController.NormalShootCadenceTime,
                        () => { canShoot = true; },
                        null, "CadenceTimeNormalShoot " + characterStatsController.name,
                        false,
                        false)
                        ;
                }
            }
            else
            {
                _isOverHeat = true;
            }
            if (!_isOverHeat)
            {
                ModOverHeat(Time.deltaTime);
            }
        }

        private void ModOverHeat(float modificator)
        {
            _countOverHeat += modificator;
        }

    }
}
