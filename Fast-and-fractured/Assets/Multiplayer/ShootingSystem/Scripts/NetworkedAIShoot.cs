using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;
using Enums;
using FastAndFractured;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Server-only shooting for AI cars: spawns networked projectiles from the shoot handles' requests and
    /// replicates the turret aim so clients see AI turrets rotate.
    /// </summary>
    public class NetworkedAIShoot : NetworkBehaviour
    {
        private const float AIM_SEND_INTERVAL = 0.1f;

        [Header("Networked Projectile Prefabs")]
        [SerializeField] private GameObject _networkedNormalBulletPrefab;
        [SerializeField] private GameObject _networkedPushBulletPrefab;
        [SerializeField] private GameObject _networkedMinePrefab;

        private NormalShootHandle _normalShootHandle;
        private PushShootHandle _pushShootHandle;
        private TurretRotationMovement _turretRotation;

        private readonly SyncVar<Vector3> _syncedTurretAim = new(new SyncTypeSettings(AIM_SEND_INTERVAL, Channel.Unreliable));

        private void Awake()
        {
            Transform searchRoot = transform.root != null ? transform.root : transform;
            _turretRotation = searchRoot.GetComponentInChildren<TurretRotationMovement>(true);
        }

        private void Update()
        {
            // FishNet's Is*Initialized need a manager, which can be gone while this lives on a frame.
            if (_turretRotation == null || base.NetworkManager == null) return;

            if (base.IsServerInitialized)
            {
                // EnemyAIBrain (server-only) writes TargetDirection; mirror it to observers.
                if (_turretRotation.TargetDirection != Vector3.zero)
                    _syncedTurretAim.Value = _turretRotation.TargetDirection;
            }
            else if (base.IsClientInitialized && _syncedTurretAim.Value != Vector3.zero)
            {
                _turretRotation.TargetDirection = _syncedTurretAim.Value;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            _normalShootHandle = GetComponentInChildren<NormalShootHandle>();
            _pushShootHandle = GetComponentInChildren<PushShootHandle>();

            var localShoot = GetComponentInChildren<LocalShootController>();
            if (localShoot != null)
                localShoot.enabled = false;

            if (_normalShootHandle != null)
                _normalShootHandle.OnShootRequest += HandleServerAuthSpawn;
            if (_pushShootHandle != null)
                _pushShootHandle.OnShootRequest += HandleServerAuthSpawn;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (_normalShootHandle != null)
                _normalShootHandle.OnShootRequest -= HandleServerAuthSpawn;
            if (_pushShootHandle != null)
                _pushShootHandle.OnShootRequest -= HandleServerAuthSpawn;
        }

        private void HandleServerAuthSpawn(BulletSpawnParameters spawnParams)
        {
            if (!base.IsServerInitialized) return;

            GameObject prefab = GetNetworkedPrefab(spawnParams);
            NetworkedBulletSpawner.SpawnServerAuthoritative(
                prefab, spawnParams, base.NetworkObject, base.ServerManager);
        }

        private GameObject GetNetworkedPrefab(BulletSpawnParameters spawnParams)
        {
            if (spawnParams.IsMine) return _networkedMinePrefab;
            if (_normalShootHandle != null && spawnParams.Pooltype == _normalShootHandle.Pooltype)
                return _networkedNormalBulletPrefab;
            return _networkedPushBulletPrefab;
        }

        internal void ConfigureProjectilePrefabs(GameObject normal, GameObject push, GameObject mine)
        {
            _networkedNormalBulletPrefab = normal;
            _networkedPushBulletPrefab = push;
            _networkedMinePrefab = mine != null ? mine : push;
        }
    }
}
