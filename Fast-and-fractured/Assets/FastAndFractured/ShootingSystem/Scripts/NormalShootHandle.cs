using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Game
{
    public class NormalShootHandle : ShootingHandle
    {
        private float _countCadenceTime;
        private float _countOverHeat;
        private float _overHeatModificator = 1.5f;
        private void Start()
        {
            CustomStart();
            _countCadenceTime = characterStatsController.NormalShootCadenceTime;
            _countOverHeat = 0;
        }
        private void Update()
        {
            if(Timer(ref _countCadenceTime,_countCadenceTime<characterStatsController.NormalShootCadenceTime,_countCadenceTime))
            {
                _countCadenceTime += Time.deltaTime;
            }
            if (!Input.GetKey(KeyCode.Mouse0))
            {
                if (!Timer(ref _countOverHeat, _countOverHeat <= 0, 0))
                {
                    ModOverHeat(-_overHeatModificator);
                }
            }
        }
        protected override void SetBulletStats(BulletBehaivour bulletBehaivour)
        {
            base.SetBulletStats(bulletBehaivour);
        }

        public void NormalShooting()
        {
            if (!Timer(ref _countOverHeat, _countOverHeat >= characterStatsController.NormalOverHead, 
                characterStatsController.NormalOverHead))
            {
                if (Timer(ref _countCadenceTime, _countCadenceTime >= characterStatsController.NormalShootCadenceTime, 0))
                {
                    Debug.Log("Shoot");
                    Vector3 velocity = (mainCamera.transform.forward + cameraCenterOffSet) * characterStatsController.NormalShootSpeed;
                    ShootBullet(velocity, characterStatsController.NormalShootMaxRange);
                }
            }
            else
                Debug.Log("OVER HEAT");
            ModOverHeat(_overHeatModificator * Time.deltaTime);
        }

        private void ModOverHeat(float modificator)
        {
            _countOverHeat += modificator;
        }
    }
}
