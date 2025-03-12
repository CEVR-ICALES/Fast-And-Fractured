using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Game {
    public abstract class ShootingHandle : MonoBehaviour, IRequestPool
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
            mainCamera = Camera.main;
        }

        protected void ShootBullet(Vector3 velocity, float range)
        {
            _velocity = velocity;
            _range = range;
            GameObject bullet = RequestPool(pooltype);
            bullet.transform.position = shootPoint.position;
            if (bullet != null)
            {
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

        public GameObject RequestPool(Pooltype pooltype)
        {
            return ObjectPoolManager.Instance.GivePooledObject(pooltype);
        }
        protected bool Timer(ref float currentTime, float timeToReach,bool logic)
        {
            if (logic)
            {
                currentTime = 0;
                return true;
            }
            currentTime += Time.deltaTime;
            return false;
        }
    }
}
