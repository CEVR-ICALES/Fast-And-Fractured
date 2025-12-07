// RUTA: Assets/FastAndFractured/ShootingSystem/Scripts/LocalShootController.cs
using FastAndFractured.Abstractions;
using UnityEngine;
using Utilities;  

namespace FastAndFractured
{ 
    public class LocalShootController : MonoBehaviour, IShootController
    {
        private NormalShootHandle _normalShootHandle;
        private PushShootHandle _pushShootHandle;

        private void Awake()
        {
            _normalShootHandle = GetComponentInChildren<NormalShootHandle>();
            _pushShootHandle = GetComponentInChildren<PushShootHandle>();
        }

        private void OnEnable()
        { 
            _normalShootHandle.OnShootRequest += HandleLocalSpawn;
            _pushShootHandle.OnShootRequest += HandleLocalSpawn;
        }

        private void OnDisable()
        {
            _normalShootHandle.OnShootRequest -= HandleLocalSpawn;
            _pushShootHandle.OnShootRequest -= HandleLocalSpawn;
        }

        public void TryNormalShoot()
        {
            _normalShootHandle.NormalShooting();
        }

        public void TryPushShoot()
        {
            _pushShootHandle.PushShooting();
        }

        public void TryMineShoot()
        {
            _pushShootHandle.MineShoot();
        }

        private void HandleLocalSpawn(BulletSpawnParameters spawnParams)
        {
            GameObject bulletGO = ObjectPoolManager.Instance.GivePooledObject(spawnParams.Pooltype);
            bulletGO.transform.parent = this.transform;
            if (bulletGO == null) return;

            bulletGO.transform.SetPositionAndRotation(spawnParams.ShootPoint.position, spawnParams.ShootPoint.rotation);
            
            var bulletBehaviour = bulletGO.GetComponent<BulletBehaviour>();
            if(bulletBehaviour != null)
            { 
                ConfigureBullet(bulletBehaviour, spawnParams);
                bulletBehaviour.InitBulletTrayectory();
            }
        }
        
        private void ConfigureBullet(BulletBehaviour bullet, BulletSpawnParameters parameters)
        {
            bullet.Velocity = parameters.Velocity;
            bullet.Range = parameters.Range;
            bullet.Damage = parameters.Damage;

            if (bullet is NormalBulletBehaviour nbb)
            {
                nbb.IgnoreCollider = parameters.IgnoredCollider;
            }
            else if (bullet is PushBulletBehaviour pbb)
            {
                pbb.PushForce = parameters.PushForce;
                pbb.ExplosionRadius = parameters.ExplosionRadius;
                pbb.ExplosionCenterOffset = parameters.ExplosionCenterOffset;
                pbb.CustomGravity = parameters.CustomGravity;
                pbb.BouncingNum = parameters.BouncingNum;
                pbb.BouncingStrenght = parameters.BouncingStrenght;
                pbb.TimeToExplode = parameters.TimeToExplode;
            }
        }
    }
}