 
using Enums;
using FMODUnity;
using System;
using UnityEngine;
using Utilities;

namespace FastAndFractured
{
 
    public abstract class ShootingHandle : MonoBehaviour
    { 
        public event Action<BulletSpawnParameters> OnShootRequest;

        public Vector3 CurrentShootDirection { get => currentShootDirection; set => currentShootDirection = value; }
        public Pooltype Pooltype { get => pooltype; set => pooltype = value; }
        public bool CanShoot { get => canShoot; }

        protected Vector3 currentShootDirection;
        [SerializeField] public StatsController characterStatsController;
        [SerializeField] protected Transform shootPoint;
        [SerializeField] internal Pooltype pooltype;
        [SerializeField] protected Vector3 directionCenterOffSet;
        protected bool canShoot = true;
        [SerializeField] private EventReference bulletSound;
        [SerializeField] protected PhysicsBehaviour physicsBehaviour;

        protected virtual void Start()
        {
            if (characterStatsController == null)
                Debug.LogError($"Character {gameObject.name} needs a StatsController for {name} Script");
            if (physicsBehaviour == null)
                Debug.LogError($"Character {gameObject.name} needs a PhysicsBehaviour for {name} Script");
        }

         
        protected virtual BulletSpawnParameters PopulateBaseSpawnParameters(Vector3 velocity, float range)
        {
            return new BulletSpawnParameters
            {
                Pooltype = this.pooltype,
                Velocity = velocity + physicsBehaviour.Rb.linearVelocity,
                Range = range,
                Damage = characterStatsController.NormalShootDamage,
                ShootPoint = this.shootPoint,
                IgnoredCollider = this.GetComponentInParent<Collider>()
            };
        }

        
        protected void ShootBullet(Vector3 velocity, float range)
        {
             BulletSpawnParameters spawnParams = PopulateBaseSpawnParameters(velocity, range);

             if (this is PushShootHandle pushHandle)
            {
                spawnParams.PushForce = characterStatsController.PushShootForce;
                spawnParams.ExplosionRadius = characterStatsController.ExplosionRadius;
                spawnParams.ExplosionCenterOffset = characterStatsController.ExplosionCenterOffset;
                spawnParams.CustomGravity = Physics.gravity * characterStatsController.PushShootGravityMultiplier;
                spawnParams.IsMine = pushHandle.ShootingMine;

                if (spawnParams.IsMine)
                {
                    spawnParams.TimeToExplode = characterStatsController.MineExplosionTime;
                    spawnParams.BouncingNum = 0;
                    spawnParams.BouncingStrenght = 0;
                }
                else
                {
                    spawnParams.TimeToExplode = 0;
                    spawnParams.BouncingNum = characterStatsController.PushShootBounceNum;
                    spawnParams.BouncingStrenght = characterStatsController.PushShootBounceForce;
                }
            }

             OnShootRequest?.Invoke(spawnParams);

            if (!bulletSound.IsNull)
            {
                SoundManager.Instance.PlayOneShot(bulletSound, shootPoint.position);
            }
        }
    }
}