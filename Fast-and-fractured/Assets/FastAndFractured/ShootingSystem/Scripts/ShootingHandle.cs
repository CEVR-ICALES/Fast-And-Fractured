using Enums;
using FMODUnity;
using UnityEngine;
using Utilities;

namespace FastAndFractured
{
    public abstract class ShootingHandle : MonoBehaviour
    {
        public Vector3 CurrentShootDirection { get => currentShootDirection; set => currentShootDirection = value; }
        public bool CanShoot { get => canShoot;   }

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
        [SerializeField] private EventReference bulletSound;

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
                if (bullet.TryGetComponent<BulletBehaviour>(out var bulletBehaivour))
                {
                    SetBulletStats(bulletBehaivour);
                    bulletBehaivour.InitBulletTrayectory();
                    SoundManager.Instance.PlayOneShot(bulletSound, shootPoint.position);
                }
            }
        }

        protected virtual void SetBulletStats(BulletBehaviour bulletBehaivour)
        {
            bulletBehaivour.Velocity = _velocity;
            bulletBehaivour.Range = _range;
            bulletBehaivour.Damage = _damage;
        }
    }
}
