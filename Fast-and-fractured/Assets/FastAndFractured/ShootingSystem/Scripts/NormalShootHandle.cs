using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Game
{
    public class NormalShootHandle : ShootingHandle
    {
        private float _countCadenceTime;
        [SerializeField]
        private float countOverHeat;
        private float _overHeatUpModificator = 1.5f;
        private float _overHeatDownModificator = 0.5f;
        //Provisional flag
        private bool _isOverHeat = false;

        #region UnityEvents
        private void Start()
        {
            CustomStart();
        }
        private void Update()
        {
            if(Timer(ref _countCadenceTime,_countCadenceTime<characterStatsController.NormalShootCadenceTime,_countCadenceTime))
            {
                _countCadenceTime += Time.deltaTime;
            }

            //In the real method, the overHeat should be reduce after some time
            if (!Input.GetKey(KeyCode.Mouse0))
            {
                    if (!Timer(ref countOverHeat, countOverHeat <= 0, 0))
                    {
                        ModOverHeat(-_overHeatDownModificator*Time.deltaTime);
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
            _countCadenceTime = characterStatsController.NormalShootCadenceTime;
            countOverHeat = 0;
        }
        protected override void SetBulletStats(BulletBehaivour bulletBehaivour)
        {
            base.SetBulletStats(bulletBehaivour);
        }

        public void NormalShooting()
        {
            if (!Timer(ref countOverHeat, countOverHeat >= characterStatsController.NormalOverHead,
                characterStatsController.NormalOverHead) && !_isOverHeat)
            {
                if (canShoot)
                {
                    Debug.Log("Shoot");
                    Vector3 velocity = (mainCamera.transform.forward + cameraCenterOffSet) * characterStatsController.NormalShootSpeed;
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
                ModOverHeat(_overHeatUpModificator * Time.deltaTime);
            }
        }

        private void ModOverHeat(float modificator)
        {
            countOverHeat += modificator;
        }

    }
}
