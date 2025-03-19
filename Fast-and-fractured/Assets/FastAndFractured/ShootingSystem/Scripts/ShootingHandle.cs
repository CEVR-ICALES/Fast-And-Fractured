using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Game {
    public abstract class ShootingHandle : MonoBehaviour
    {
        public Vector3 CurrentShootDirection { get => currentShootDirection; set => currentShootDirection = value; }
        protected Vector3 currentShootDirection;
        [SerializeField] protected StatsController characterStatsController;
        [SerializeField]
        protected Transform shootPoint;
        [SerializeField]
        protected Pooltype pooltype;
        private Vector3 _velocity;
        private float _range;
        private float _damage;
        [SerializeField]
        protected Vector3 directionCenterOffSet;
        protected bool canShoot = true;

        protected virtual void CustomStart()
        {
            //Provisional, this will be replace
            if (characterStatsController == null)
                Debug.LogError("Character " + gameObject.name + " needs a StatsController for  " + name + " Script");
        }

        protected void ShootBullet(Vector3 velocity, float range)
        {
            _velocity = velocity;
            _range = range;
            _damage = characterStatsController.NormalShootDamage;
            GameObject bullet = ObjectPoolManager.Instance.GivePooledObject(pooltype);
            if (bullet != null)
            {
                bullet.transform.position = shootPoint.position;
                if (bullet.TryGetComponent<BulletBehaivour>(out var bulletBehaivour))
                {
                    SetBulletStats(bulletBehaivour);
                    bulletBehaivour.InitBulletTrayectory();
                }
            }
        }

        protected virtual void SetBulletStats(BulletBehaivour bulletBehaivour)
        {
            bulletBehaivour.Velocity = _velocity;
            bulletBehaivour.Range = _range;
            bulletBehaivour.Damage = _damage;
        }

        //Provisional Timer
        protected bool Timer(ref float currentTime,bool logic,float resetValue)
        {
            if (logic)
            {
                currentTime = resetValue;
                return true;
            }
            return false;
        }
    }
}
