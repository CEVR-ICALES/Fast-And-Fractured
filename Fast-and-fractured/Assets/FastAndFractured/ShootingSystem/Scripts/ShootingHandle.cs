using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Game {
    public abstract class ShootingHandle : MonoBehaviour
    {
        [SerializeField]
        protected StatsController characterStatsController;
        [SerializeField]
        protected Transform shootPoint;
        [SerializeField]
        protected Pooltype pooltype;
        protected Camera mainCamera;
        private Vector3 _velocity;
        private float _range;
        private float _damage;
        [SerializeField]
        protected Vector3 cameraCenterOffSet;
    
    protected virtual void CustomStart()
        {
            //Provisional, this will be replace
            if (characterStatsController == null)
                Debug.LogError("Character " + gameObject.name + " needs a StatsController for  " + name + " Script");
            //Provisional Camera
            mainCamera = Camera.main;
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
