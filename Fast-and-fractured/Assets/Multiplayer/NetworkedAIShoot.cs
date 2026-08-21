using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;
using Enums;
using FastAndFractured;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Server-only networked shooting for AI vehicles.
    /// AI runs entirely on the server - no prediction/replication needed.
    /// Subscribes to OnShootRequest on NormalShootHandle/PushShootHandle,
    /// spawns networked projectiles that replicate to all clients.
    /// Also replicates the turret aim direction (set server-side by EnemyAIBrain)
    /// so client-side AI turrets rotate instead of staying frozen.
    /// </summary>
    public class NetworkedAIShoot : NetworkBehaviour
    {
        [Header("Networked Projectile Prefabs")]
        [SerializeField] private GameObject _networkedNormalBulletPrefab;
        [SerializeField] private GameObject _networkedPushBulletPrefab;
        [SerializeField] private GameObject _networkedMinePrefab;

        private NormalShootHandle _normalShootHandle;
        private PushShootHandle _pushShootHandle;
        private TurretRotationMovement _turretRotation;

        private readonly SyncVar<Vector3> _syncedTurretAim = new(new SyncTypeSettings(0.1f, Channel.Unreliable));

        private void Awake()
        {
            Transform searchRoot = transform.root != null ? transform.root : transform;
            _turretRotation = searchRoot.GetComponentInChildren<TurretRotationMovement>(true);
        }

        private void Update()
        {
            // NetworkBehaviour may remain alive for a frame while its NetworkManager is
            // being torn down. FishNet's IsServerInitialized/IsClientInitialized accessors
            // require a valid manager, so guard that lifecycle window explicitly.
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
