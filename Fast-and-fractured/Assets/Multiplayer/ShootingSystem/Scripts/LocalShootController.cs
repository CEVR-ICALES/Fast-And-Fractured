using FastAndFractured.Abstractions;
using UnityEngine;
using Utilities;  

namespace FastAndFractured
{ 
    public class LocalShootController : MonoBehaviour, IShootController
    {
        private NormalShootHandle _normalShootHandle;
        private PushShootHandle _pushShootHandle;
        private bool _eventsSubscribed;
        private bool _loggedMissingShootHandles;

        private void Awake()
        {
            EnsureHandles();
        }

        private void EnsureHandles()
        {
            if (_normalShootHandle == null)
            {
                _normalShootHandle = GetComponentInChildren<NormalShootHandle>();
            }

            if (_pushShootHandle == null)
            {
                _pushShootHandle = GetComponentInChildren<PushShootHandle>();
            }
        }

        private void OnEnable()
        {
            EnsureHandles();
            if (_eventsSubscribed) return;

            if (_normalShootHandle == null && _pushShootHandle == null)
            {
                if (!_loggedMissingShootHandles)
                {
                    _loggedMissingShootHandles = true;
                    Debug.LogWarning($"[LocalShootController] Missing shoot handles on {name}.");
                }
                return;
            }

            if (_normalShootHandle != null)
            {
                _normalShootHandle.OnShootRequest += HandleLocalSpawn;
            }

            if (_pushShootHandle != null)
            {
                _pushShootHandle.OnShootRequest += HandleLocalSpawn;
            }

            _eventsSubscribed = true;
        }

        private void OnDisable()
        {
            if (!_eventsSubscribed) return;

            if (_normalShootHandle != null)
            {
                _normalShootHandle.OnShootRequest -= HandleLocalSpawn;
            }

            if (_pushShootHandle != null)
            {
                _pushShootHandle.OnShootRequest -= HandleLocalSpawn;
            }

            _eventsSubscribed = false;
        }

        public void TryNormalShoot()
        {
            if (!isActiveAndEnabled) return;
            EnsureHandles();
            if (_normalShootHandle == null) return;
            _normalShootHandle.NormalShooting();
        }

        public void TryPushShoot()
        {
            if (!isActiveAndEnabled) return;
            EnsureHandles();
            if (_pushShootHandle == null) return;
            _pushShootHandle.PushShooting();
        }

        public void TryMineShoot()
        {
            if (!isActiveAndEnabled) return;
            EnsureHandles();
            if (_pushShootHandle == null) return;
            _pushShootHandle.MineShoot();
        }

        private void HandleLocalSpawn(BulletSpawnParameters spawnParams)
        {
            if (!isActiveAndEnabled) return;
            if (ObjectPoolManager.Instance == null) return;

            GameObject bulletGO = ObjectPoolManager.Instance.GivePooledObject(spawnParams.Pooltype);
            if (bulletGO == null) return;

            if (spawnParams.ShootPoint != null)
            {
                bulletGO.transform.SetPositionAndRotation(spawnParams.ShootPoint.position, spawnParams.ShootPoint.rotation);
            }
            else
            {
                bulletGO.transform.SetPositionAndRotation(transform.position, transform.rotation);
            }
            
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
                pbb.Author = parameters.Author;
            }
        }
    }
}
