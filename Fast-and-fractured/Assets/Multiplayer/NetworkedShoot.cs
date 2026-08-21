using FishNet.Object;
using UnityEngine;
using Utilities;
using Enums;
using FastAndFractured.Abstractions;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Connection;
using FastAndFractured;

namespace FastAndFractured.Multiplayer
{
    public struct NetworkShootParams
    {
        public Pooltype Pooltype;
        public Vector3 InitialVelocity;
        public float Range;
        public float Damage;

        public float PushForce;
        public float ExplosionRadius;
        public Vector3 ExplosionCenterOffset;
        public Vector3 CustomGravity;
        public int BouncingNum;
        public float BouncingStrenght;
        public float TimeToExplode;
        public bool IsMine;
    }

    public class NetworkedShoot : NetworkBehaviour, IShootController
    {
        #region PREDICTION_STRUCT
        public struct ShootData : IReplicateData
        {
            public bool NormalShoot;
            public bool PushShoot;
            public bool PushShootMode;
            public bool MineShoot;
            public Vector3 AimDirection;

            private uint _tick;
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        public struct ReconcileData : IReconcileData
        {
            private uint _tick;
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }
        #endregion

        #region REFERENCES & FIELDS
        private NormalShootHandle _normalShootHandle;
        private PushShootHandle _pushShootHandle;
        private NetworkInputProvider _networkInputProvider;
        private PlayerInputProvider _playerInputProvider;
        private AimPushShootTrace _pushShootAimTrace;
        private TurretRotationMovement _turretRotation;
        private global::Utilities.CameraHolder _cameraHolder;
        private bool _loggedMissingShootHandles;
        private bool _serverShootEventsSubscribed;
        private bool _ownerPushShootEventsSubscribed;
        private bool _ownerPredictedShootEventsSubscribed;
        private bool _capturePredictedValidation;
        private bool _capturedPredictedShot;
        private BulletSpawnParameters _capturedPredictedParams;
        private bool _loggedMissingInputProvider;

        [Header("Cosmetic Projectile Types")]
        [SerializeField] private Pooltype _cosmeticNormalBulletType;
        [SerializeField] private Pooltype _cosmeticPushBulletType;
        [SerializeField] private Pooltype _cosmeticMineType;

        [Header("Networked Projectile Prefabs")]
        [SerializeField] private GameObject _networkedNormalBulletPrefab;
        [SerializeField] private GameObject _networkedPushBulletPrefab;
        [SerializeField] private GameObject _networkedMinePrefab;

        [Header("Remote Aim")]
        [SerializeField] private bool _showPushAimOnRemoteClients = true;
        #endregion

        #region FISHNET_LIFECYCLE
        private void Awake()
        {
            _normalShootHandle = GetComponentInChildren<NormalShootHandle>(true);
            _pushShootHandle = GetComponentInChildren<PushShootHandle>(true);
            _pushShootAimTrace = GetComponentInParent<AimPushShootTrace>();
            if (_pushShootAimTrace == null)
            {
                _pushShootAimTrace = transform.root != null
                    ? transform.root.GetComponentInChildren<AimPushShootTrace>(true)
                    : null;
            }

            var localShoot = GetComponentInChildren<LocalShootController>(true);
            if (localShoot != null)
            {
                // The SP state-machine resolves IShootController with GetComponentInChildren.
                // A merely-disabled LocalShootController is still returned by that lookup and
                // its Try* methods intentionally no-op while disabled. Remove the local adapter
                // from multiplayer instances so this NetworkedShoot is the unambiguous target.
                Destroy(localShoot);
            }

            Transform searchRoot = transform.root != null ? transform.root : transform;
            _turretRotation = searchRoot.GetComponentInChildren<TurretRotationMovement>(true);
            _cameraHolder = searchRoot.GetComponentInChildren<global::Utilities.CameraHolder>(true);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // CameraHolder drives TurretRotationMovement.TargetDirection from Camera.main every
            // frame. On non-owned cars that camera belongs to the LOCAL player, so every remote
            // turret aims wherever the local player looks. Disable it and drive the turret from
            // the replicated aim instead (see Replicate).
            if (!base.IsOwner && _cameraHolder != null)
                _cameraHolder.enabled = false;

            if (base.IsOwner)
            {
                TrySubscribeOwnerPushShootEvents();
                TrySubscribeOwnerPredictedShootEvents();
            }
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);

            if (base.IsOwner)
            {
                TrySubscribeOwnerPushShootEvents();
                TrySubscribeOwnerPredictedShootEvents();
            }
            else
            {
                UnsubscribeOwnerPushShootEvents();
                UnsubscribeOwnerPredictedShootEvents();
            }
        }

        private NetworkInputProvider GetNetworkInputProvider()
        {
            if (_networkInputProvider == null)
                _networkInputProvider = GetComponentInParent<NetworkInputProvider>();
            return _networkInputProvider;
        }

        private PlayerInputProvider GetPlayerInputProvider()
        {
            if (_playerInputProvider == null)
                _playerInputProvider = GetComponentInParent<PlayerInputProvider>();
            return _playerInputProvider;
        }

        private bool _subscribedToTimeManager = false;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!_subscribedToTimeManager)
            {
                base.TimeManager.OnTick += TimeManager_OnTick;
                _subscribedToTimeManager = true;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            TrySubscribeServerShootEvents();
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (base.TimeManager != null && _subscribedToTimeManager)
            {
                TimeManager.OnTick -= TimeManager_OnTick;
                _subscribedToTimeManager = false;
            }

            UnsubscribeServerShootEvents();
            UnsubscribeOwnerPushShootEvents();
            UnsubscribeOwnerPredictedShootEvents();

            if (!base.IsOwner && _pushShootAimTrace != null)
            {
                _pushShootAimTrace.DrawTrayectory(false);
            }
        }

        private void TimeManager_OnTick()
        {
            if (base.IsServerInitialized && !_serverShootEventsSubscribed)
            {
                TrySubscribeServerShootEvents();
            }

            if (base.IsOwner)
            {
                BuildShootData(out ShootData sd);
                Replicate(sd, ReplicateState.Ticked);
            }
            else if (base.IsServer)
            {
                Replicate(default, ReplicateState.Ticked);
            }
        }

        public override void CreateReconcile()
        {
            if (base.IsServer)
            {
                var rd = new ReconcileData();
                Reconcile(rd, Channel.Reliable);
            }
        }
        #endregion

        #region SIMULATION
        private void BuildShootData(out ShootData sd)
        {
            sd = default;
            var net = GetNetworkInputProvider();
            if (net != null)
            {
                FillShootData(ref sd, net.IsNormalShooting, net.FirePushAction, net.IsPushShooting, net.IsThrowingMine, net.AimDirection);
                return;
            }

            var fallback = GetPlayerInputProvider();
            if (fallback == null)
            {
                if (!_loggedMissingInputProvider)
                {
                    _loggedMissingInputProvider = true;
                    Debug.LogWarning($"[NetworkedShoot] No input provider found on {name}. Shooting input replication is disabled until an input provider is available.");
                }
                return;
            }

            FillShootData(ref sd, fallback.IsNormalShooting, fallback.FirePushAction, fallback.IsPushShooting, fallback.IsThrowingMine, fallback.AimDirection);
        }

        private static void FillShootData(ref ShootData sd, bool normal, bool firePush, bool pushMode, bool mine, Vector3 aim)
        {
            sd.NormalShoot = normal;
            sd.PushShoot = firePush;
            sd.PushShootMode = pushMode;
            sd.MineShoot = mine;
            sd.AimDirection = aim;
        }

        [Replicate]
        private void Replicate(ShootData sd, ReplicateState state, Channel channel = Channel.Unreliable)
        {
            if (!EnsureShootHandles())
            {
                return;
            }

            if (!base.IsOwner)
            {
                var provider = GetNetworkInputProvider();
                provider?.SetNetworkShootInput(sd.NormalShoot, sd.PushShootMode, sd.PushShoot, sd.MineShoot, sd.AimDirection);
                UpdateRemotePushAim(sd.PushShootMode);
            }

            if (sd.AimDirection != Vector3.zero)
            {
                _normalShootHandle.CurrentShootDirection = sd.AimDirection;
                _pushShootHandle.CurrentShootDirection = sd.AimDirection;

                // Owner turrets are driven by CameraHolder (same as singleplayer); non-owned
                // turrets follow the replicated aim so remote peers see where this car aims.
                if (!base.IsOwner && _turretRotation != null)
                    _turretRotation.TargetDirection = sd.AimDirection;
            }

            // Remote owners execute their local handles and predicted-spawn the real projectile.
            // The server validates that spawn by re-running its own handle when the FishNet spawn
            // payload arrives. It must not also fire from Replicate or it would consume cooldown
            // and create a duplicate projectile. Hosts keep the direct authoritative path.
            bool mayExecuteShootHandle = !base.IsServerInitialized || base.IsOwner;
            if (mayExecuteShootHandle && sd.NormalShoot) _normalShootHandle.NormalShooting();

            // Push and mine are initiated by the same state-machine actions as single-player.
            // Those actions call IShootController.TryPushShoot/TryMineShoot on this component.
        }

        [Reconcile]
        private void Reconcile(ReconcileData rd, Channel channel = Channel.Unreliable)
        {
        }
        #endregion

        #region EVENT_HANDLERS_&_SPAWNING
        public void TryNormalShoot()
        {
            if (!base.IsOwner || !EnsureShootHandles()) return;
            _normalShootHandle.NormalShooting();
        }

        public void TryPushShoot()
        {
            if (!base.IsOwner || !EnsureShootHandles()) return;
            _pushShootHandle.PushShooting();
        }

        public void TryMineShoot()
        {
            if (!base.IsOwner || !EnsureShootHandles()) return;
            _pushShootHandle.MineShoot();
        }

        private void HandleServerAuthSpawn(BulletSpawnParameters spawnParams)
        {
            if (!base.IsServer) return;

            if (_capturePredictedValidation)
            {
                _capturedPredictedParams = spawnParams;
                _capturedPredictedShot = true;
                return;
            }

            GameObject prefab = GetNetworkedPrefabFromSpawnParams(spawnParams);
            NetworkedBulletSpawner.SpawnServerAuthoritative(
                prefab, spawnParams, base.NetworkObject, base.ServerManager, base.Owner);
        }

        /// <summary>
        /// Relays an owner-side push or mine request to the authoritative server. Unlike the
        /// normal shot (a held input), these actions are single-frame pulses and cannot safely
        /// depend on prediction tick sampling. The server re-runs PushShootHandle using its own
        /// stats/cooldown state; no client-provided damage or force values are trusted.
        /// </summary>
        private void HandleOwnerPushShootRequest(BulletSpawnParameters spawnParams)
        {
            // Predicted spawning is itself a reliable owner->server request. Keep this legacy
            // relay inert when the Pro predicted-spawn path is installed to avoid double shots.
            if (!base.IsOwner || base.IsServerInitialized ||
                GetNetworkedPrefabFromSpawnParams(spawnParams)?.GetComponent<NetworkedPredictedProjectileSpawn>() != null)
                return;

            RequestPushShootServerRpc(spawnParams.IsMine, _pushShootHandle.CurrentShootDirection);
        }

        [ServerRpc]
        private void RequestPushShootServerRpc(bool isMine, Vector3 aimDirection)
        {
            if (!EnsureShootHandles() || !_pushShootHandle.CanShoot)
                return;

            if (!IsFinite(aimDirection) || aimDirection.sqrMagnitude < 0.0001f)
                aimDirection = transform.forward;

            _pushShootHandle.CurrentShootDirection = aimDirection.normalized;
            if (isMine)
                _pushShootHandle.MineShoot();
            else
                _pushShootHandle.PushShooting();
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private GameObject GetNetworkedPrefabFromSpawnParams(BulletSpawnParameters spawnParams)
        {
            if (spawnParams.IsMine) return _networkedMinePrefab;
            if (spawnParams.Pooltype == _normalShootHandle.Pooltype) return _networkedNormalBulletPrefab;
            return _networkedPushBulletPrefab;
        }

        private void HandleOwnerPredictedSpawn(BulletSpawnParameters spawnParams)
        {
            if (!base.IsOwner || base.IsServerInitialized) return;
            NetworkedBulletSpawner.SpawnPredictedClient(GetNetworkedPrefabFromSpawnParams(spawnParams),
                spawnParams, base.NetworkObject, base.Owner);
        }

        internal bool TryValidatePredictedProjectile(NetworkObject spawnedProjectile, NetworkShootParams requested,
            out NetworkShootParams authoritative, out Vector3 spawnPosition, out Quaternion spawnRotation)
        {
            authoritative = default;
            spawnPosition = default;
            spawnRotation = default;
            if (!base.IsServerInitialized || spawnedProjectile == null || !EnsureShootHandles()) return false;

            GameObject expected;
            if (requested.IsMine) expected = _networkedMinePrefab;
            else if (requested.Pooltype == _normalShootHandle.Pooltype) expected = _networkedNormalBulletPrefab;
            else expected = _networkedPushBulletPrefab;
            var expectedNob = expected != null ? expected.GetComponent<NetworkObject>() : null;
            if (expectedNob == null || expectedNob.PrefabId != spawnedProjectile.PrefabId) return false;

            Vector3 aim = requested.InitialVelocity.sqrMagnitude > 0.0001f
                ? requested.InitialVelocity.normalized : transform.forward;
            _capturedPredictedShot = false;
            _capturePredictedValidation = true;
            try
            {
                if (requested.IsMine)
                {
                    _pushShootHandle.CurrentShootDirection = aim;
                    _pushShootHandle.MineShoot();
                }
                else if (expected == _networkedNormalBulletPrefab)
                {
                    _normalShootHandle.CurrentShootDirection = aim;
                    _normalShootHandle.NormalShooting();
                }
                else
                {
                    _pushShootHandle.CurrentShootDirection = aim;
                    _pushShootHandle.PushShooting();
                }
            }
            finally { _capturePredictedValidation = false; }

            if (!_capturedPredictedShot || _capturedPredictedParams.ShootPoint == null) return false;
            authoritative = NetworkedBulletSpawner.ToNetworkParams(_capturedPredictedParams);
            spawnPosition = _capturedPredictedParams.ShootPoint.position;
            spawnRotation = _capturedPredictedParams.ShootPoint.rotation;
            return true;
        }

        /// <summary>
        /// Gives the server-only AI shoot component the projectile variants already configured
        /// for this character. This avoids trying to recover them from the single-player pool.
        /// </summary>
        internal void ConfigureAiShoot(NetworkedAIShoot aiShoot)
        {
            if (aiShoot == null) return;
            aiShoot.ConfigureProjectilePrefabs(
                _networkedNormalBulletPrefab,
                _networkedPushBulletPrefab,
                _networkedMinePrefab);
        }

        private void SpawnCosmeticProjectile(BulletSpawnParameters spawnParams)
        {
            if (!base.IsOwner) return;
            if (ObjectPoolManager.Instance == null) return;
            if (spawnParams.ShootPoint == null) return;

            Pooltype cosmeticType;
            if (spawnParams.IsMine)
                cosmeticType = _cosmeticMineType;
            else if (spawnParams.Pooltype == _normalShootHandle.Pooltype)
                cosmeticType = _cosmeticNormalBulletType;
            else
                cosmeticType = _cosmeticPushBulletType;

            GameObject cosmeticBulletGO = ObjectPoolManager.Instance.GivePooledObject(cosmeticType);
            if (cosmeticBulletGO == null) return;

            cosmeticBulletGO.transform.SetPositionAndRotation(spawnParams.ShootPoint.position, spawnParams.ShootPoint.rotation);

            var bulletBehaviour = cosmeticBulletGO.GetComponent<BulletBehaviour>();
            if (bulletBehaviour != null)
            {
                ConfigureBullet(bulletBehaviour, spawnParams, true);
                bulletBehaviour.InitBulletTrayectory();
            }
        }

        private static int _bulletDefaultLayer = -1;
        private static int _bulletIgnoreRaycastLayer = -1;

        private void ConfigureBullet(BulletBehaviour bullet, BulletSpawnParameters parameters, bool isCosmetic)
        {
            bullet.Velocity = parameters.Velocity;
            bullet.Range = parameters.Range;
            bullet.Damage = isCosmetic ? 0 : parameters.Damage;

            if (_bulletDefaultLayer < 0) _bulletDefaultLayer = LayerMask.NameToLayer("Default");
            if (_bulletIgnoreRaycastLayer < 0) _bulletIgnoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");

            bullet.gameObject.layer = isCosmetic ? _bulletIgnoreRaycastLayer : _bulletDefaultLayer;

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

        private bool EnsureShootHandles()
        {
            if (_normalShootHandle == null)
                _normalShootHandle = GetComponentInChildren<NormalShootHandle>(true);

            if (_pushShootHandle == null)
                _pushShootHandle = GetComponentInChildren<PushShootHandle>(true);

            if (_normalShootHandle != null && _pushShootHandle != null)
                return true;

            if (!_loggedMissingShootHandles)
            {
                _loggedMissingShootHandles = true;
                Debug.LogWarning($"[NetworkedShoot] Missing shoot handles on {name}. Networked shooting is disabled for this object.");
            }

            return false;
        }

        private void TrySubscribeServerShootEvents()
        {
            if (!base.IsServerInitialized || _serverShootEventsSubscribed)
                return;

            if (!EnsureShootHandles())
                return;

            _normalShootHandle.OnShootRequest -= HandleServerAuthSpawn;
            _pushShootHandle.OnShootRequest -= HandleServerAuthSpawn;
            _normalShootHandle.OnShootRequest += HandleServerAuthSpawn;
            _pushShootHandle.OnShootRequest += HandleServerAuthSpawn;
            _serverShootEventsSubscribed = true;
        }

        private void TrySubscribeOwnerPushShootEvents()
        {
            if (_ownerPushShootEventsSubscribed || !base.IsOwner || !EnsureShootHandles())
                return;

            _pushShootHandle.OnShootRequest -= HandleOwnerPushShootRequest;
            _pushShootHandle.OnShootRequest += HandleOwnerPushShootRequest;
            _ownerPushShootEventsSubscribed = true;
        }

        private void TrySubscribeOwnerPredictedShootEvents()
        {
            if (_ownerPredictedShootEventsSubscribed || !base.IsOwner || !EnsureShootHandles()) return;
            _normalShootHandle.OnShootRequest -= HandleOwnerPredictedSpawn;
            _pushShootHandle.OnShootRequest -= HandleOwnerPredictedSpawn;
            _normalShootHandle.OnShootRequest += HandleOwnerPredictedSpawn;
            _pushShootHandle.OnShootRequest += HandleOwnerPredictedSpawn;
            _ownerPredictedShootEventsSubscribed = true;
        }

        private void UnsubscribeOwnerPredictedShootEvents()
        {
            if (!_ownerPredictedShootEventsSubscribed) return;
            if (_normalShootHandle != null) _normalShootHandle.OnShootRequest -= HandleOwnerPredictedSpawn;
            if (_pushShootHandle != null) _pushShootHandle.OnShootRequest -= HandleOwnerPredictedSpawn;
            _ownerPredictedShootEventsSubscribed = false;
        }

        private void UnsubscribeOwnerPushShootEvents()
        {
            if (!_ownerPushShootEventsSubscribed)
                return;

            if (_pushShootHandle != null)
                _pushShootHandle.OnShootRequest -= HandleOwnerPushShootRequest;

            _ownerPushShootEventsSubscribed = false;
        }

        private void UnsubscribeServerShootEvents()
        {
            if (!_serverShootEventsSubscribed)
                return;

            if (_normalShootHandle != null)
                _normalShootHandle.OnShootRequest -= HandleServerAuthSpawn;

            if (_pushShootHandle != null)
                _pushShootHandle.OnShootRequest -= HandleServerAuthSpawn;

            _serverShootEventsSubscribed = false;
        }

        private void UpdateRemotePushAim(bool pushShootMode)
        {
            if (base.IsOwner || _pushShootAimTrace == null)
                return;

            bool shouldDraw = _showPushAimOnRemoteClients && pushShootMode;
            _pushShootAimTrace.DrawTrayectory(shouldDraw);
        }
        #endregion
    }
}
