using Enums;
using FMODUnity;
using UnityEngine;
using Utilities;

namespace FastAndFractured
{
    public abstract class ShootingHandle : MonoBehaviour
    {
        public Vector3 CurrentShootDirection { get => currentShootDirection; set => currentShootDirection = value; }
        public Pooltype Pooltype { get => pooltype; set => pooltype=value; }
        public bool CanShoot { get => canShoot;   }

        protected Vector3 currentShootDirection;
        [SerializeField] public StatsController characterStatsController;
        [SerializeField]
        protected Transform shootPoint;
        [SerializeField]
        internal Pooltype pooltype;
        private Vector3 _velocity;
        private float _range;
        private float _damage;
        [SerializeField]
        protected Vector3 directionCenterOffSet;
        protected bool canShoot = true;
        [SerializeField] private EventReference bulletSound;
        [SerializeField]
        protected PhysicsBehaviour physicsBehaviour;
        public bool SuppressInstantiation { get; set; } = false;

        protected virtual void Start()
        {
            if (characterStatsController == null)
                Debug.LogError("Character " + gameObject.name + " needs a StatsController for  " + name + " Script");
            if(physicsBehaviour == null)
                Debug.LogError("Character " + gameObject.name + " needs a PhysicsBehaivour for  " + name + " Script");
        }

        protected void ShootBullet(Vector3 velocity, float range)
        {
            if (SuppressInstantiation)
            {
                return;
            }
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
            bulletBehaivour.Velocity = _velocity + physicsBehaviour.Rb.linearVelocity;
            bulletBehaivour.Range = _range;
            bulletBehaivour.Damage = _damage;
        }
    }
}
