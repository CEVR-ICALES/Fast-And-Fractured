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
                // The SP state machine finds IShootController with GetComponentInChildren, which also returns a disabled
                // LocalShootController: remove it so this component is the only target.
                Destroy(localShoot);
            }

            Transform searchRoot = transform.root != null ? transform.root : transform;
            _turretRotation = searchRoot.GetComponentInChildren<TurretRotationMovement>(true);
            _cameraHolder = searchRoot.GetComponentInChildren<global::Utilities.CameraHolder>(true);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // CameraHolder aims at Camera.main, the local player's camera: remote turrets follow the replicated aim.
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

                if (!base.IsOwner && _turretRotation != null)
                    _turretRotation.TargetDirection = sd.AimDirection;
            }

            // Non-owners fire the remote car's handle only for muzzle VFX and sound. The owner fires from its state
            // machine, as in singleplayer; the server validates its predicted spawn instead of firing.
            if (!base.IsServerInitialized && !base.IsOwner && sd.NormalShoot) _normalShootHandle.NormalShooting();

            // Push and mine come from the same state-machine actions as in singleplayer.
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
        /// Relays an owner push or mine (single-frame pulses, not held input) to the server, which re-runs the handle
        /// with its own stats and cooldown.
        /// </summary>
        private void HandleOwnerPushShootRequest(BulletSpawnParameters spawnParams)
        {
            // Inert when predicted spawning is installed: that request already reaches the server.
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

            if (!IsFinite(aimDirection) || aimDirection.sqrMagnitude < MIN_AIM_SQR_MAGNITUDE)
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

        // Server validation of predicted shots. The cadence is a rate limit on the server's real-time clock
        // (TryTakeShotCredit): the server handle's own cooldown starts a latency late, and the shooter's tick spacing
        // shrinks when FishNet drops ticks in a hitch; both rejected valid shots.
        internal const float EARLY_SHOT_ALLOWANCE_SECONDS = 0.5f;
        private const uint MAX_LAUNCH_AGE_TICKS = 50;        // a shot fired more than 1 s ago is stale
        private const uint MAX_LAUNCH_FUTURE_TICKS = 10;
        private const float MAX_LAUNCH_POSITION_ERROR = 6f;  // metres from the server's own gun
        private const float MAX_LAUNCH_SPEED_FACTOR = 1.25f;
        private const float MAX_LAUNCH_ANGLE = 45f;
        private const float LAUNCH_SPEED_SLACK = 5f;
        private const float MIN_SPEED_FOR_ANGLE_CHECK = 0.1f;
        private const float MIN_COOLDOWN_SPEED = 0.01f;
        private const float MIN_AIM_SQR_MAGNITUDE = 0.0001f;

        private static readonly System.Reflection.FieldInfo HandleCanShoot =
            typeof(ShootingHandle).GetField("canShoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        private ShotCredit _normalShotCredit, _pushShotCredit;

        internal bool TryValidatePredictedProjectile(NetworkObject spawnedProjectile, NetworkShootParams requested,
            Vector3 requestedPosition, Quaternion requestedRotation, uint launchTick, uint shooterClockNow,
            out NetworkShootParams authoritative, out Vector3 spawnPosition, out Quaternion spawnRotation, out string reason)
        {
            authoritative = default;
            spawnPosition = default;
            spawnRotation = default;
            reason = "not ready";
            if (!base.IsServerInitialized || spawnedProjectile == null || !EnsureShootHandles()) return false;

            GameObject expected;
            if (requested.IsMine) expected = _networkedMinePrefab;
            else if (requested.Pooltype == _normalShootHandle.Pooltype) expected = _networkedNormalBulletPrefab;
            else expected = _networkedPushBulletPrefab;
            var expectedNob = expected != null ? expected.GetComponent<NetworkObject>() : null;
            if (expectedNob == null || expectedNob.PrefabId != spawnedProjectile.PrefabId)
            {
                reason = "wrong prefab";
                return false;
            }

            bool isNormal = expected == _networkedNormalBulletPrefab && !requested.IsMine;
            ShootingHandle handle = isNormal ? _normalShootHandle : _pushShootHandle;
            StatsController stats = handle.characterStatsController;

            // Both on the shooter's clock: its LocalTick when it fired and the one stamped on the packet.
            if (!IsPlausibleLaunchTick(launchTick, shooterClockNow, out reason)) return false;

            float cooldownSeconds = stats == null ? 0f : isNormal
                ? stats.NormalShootCadenceTime
                : stats.PushCooldown / Mathf.Max(MIN_COOLDOWN_SPEED, stats.CooldownSpeed);
            ref ShotCredit credit = ref isNormal ? ref _normalShotCredit : ref _pushShotCredit;
            if (!TryTakeShotCredit(ref credit, Time.unscaledTimeAsDouble, cooldownSeconds))
            {
                reason = $"faster than cadence ({cooldownSeconds:F2}s, credit {credit.Credit:F2})";
                return false;
            }

            // Damage, force, bounces and gravity always come from the server's stats (the handle in capture mode).
            Vector3 aim = requested.InitialVelocity.sqrMagnitude > MIN_AIM_SQR_MAGNITUDE ? requested.InitialVelocity.normalized : transform.forward;
            _capturedPredictedShot = false;
            _capturePredictedValidation = true;
            try
            {
                HandleCanShoot?.SetValue(handle, true);
                handle.CurrentShootDirection = aim;
                if (requested.IsMine) _pushShootHandle.MineShoot();
                else if (isNormal) _normalShootHandle.NormalShooting();
                else _pushShootHandle.PushShooting();
            }
            finally { _capturePredictedValidation = false; }

            if (!_capturedPredictedShot || _capturedPredictedParams.ShootPoint == null)
            {
                credit.Credit += 1d;   // not fired: give the shot back
                reason = isNormal && _normalShootHandle.IsOverHeat ? "overheated" : "handle refused";
                return false;
            }

            authoritative = NetworkedBulletSpawner.ToNetworkParams(_capturedPredictedParams);
            spawnPosition = _capturedPredictedParams.ShootPoint.position;
            spawnRotation = _capturedPredictedParams.ShootPoint.rotation;

            // Launch position and velocity: the shooter's own, if plausible; the server's gun is a little elsewhere and
            // the shot would fly a different path on the shooter's screen.
            if (IsPlausibleLaunch(requestedPosition, requested.InitialVelocity, spawnPosition, authoritative.InitialVelocity))
            {
                spawnPosition = requestedPosition;
                spawnRotation = requestedRotation;
                authoritative.InitialVelocity = requested.InitialVelocity;
            }

            reason = null;
            return true;
        }

        /// <summary>Both ticks come from the shooter's LocalTick; a client's server-tick estimate lags far behind after a hitch.</summary>
        internal static bool IsPlausibleLaunchTick(uint launchTick, uint shooterClockNow, out string reason)
        {
            reason = null;
            if (launchTick > shooterClockNow + MAX_LAUNCH_FUTURE_TICKS) { reason = "launch tick in the future"; return false; }
            if (shooterClockNow > launchTick + MAX_LAUNCH_AGE_TICKS) { reason = "launch tick too old"; return false; }
            return true;
        }

        /// <summary>Server-side rate budget of one weapon of one shooter.</summary>
        internal struct ShotCredit
        {
            public bool HasShot;
            public double Credit;
            public double LastTime;
        }

        /// <summary>
        /// Server real-time rate limit for one weapon: credit refills at one shot per cooldown, up to one banked shot. A
        /// shot costs one credit and may arrive EARLY_SHOT_ALLOWANCE_SECONDS early, so the rate never exceeds the weapon's.
        /// </summary>
        internal static bool TryTakeShotCredit(ref ShotCredit state, double now, float cooldownSeconds)
        {
            if (cooldownSeconds <= 0f) return true;
            state.Credit = state.HasShot
                ? System.Math.Min(1d, state.Credit + System.Math.Max(0d, now - state.LastTime) / cooldownSeconds)
                : 1d;
            state.HasShot = true;
            state.LastTime = now;

            double allowance = System.Math.Min(1d, EARLY_SHOT_ALLOWANCE_SECONDS / cooldownSeconds);
            if (state.Credit < 1d - allowance - 1e-6) return false;
            state.Credit -= 1d;
            return true;
        }

        internal static bool IsPlausibleLaunch(Vector3 requestedPosition, Vector3 requestedVelocity,
            Vector3 serverPosition, Vector3 serverVelocity)
        {
            if (!IsFinite(requestedPosition) || !IsFinite(requestedVelocity)) return false;
            if ((requestedPosition - serverPosition).sqrMagnitude > MAX_LAUNCH_POSITION_ERROR * MAX_LAUNCH_POSITION_ERROR) return false;
            float serverSpeed = serverVelocity.magnitude;
            if (requestedVelocity.magnitude > serverSpeed * MAX_LAUNCH_SPEED_FACTOR + LAUNCH_SPEED_SLACK) return false;
            if (serverSpeed > MIN_SPEED_FOR_ANGLE_CHECK && Vector3.Angle(requestedVelocity, serverVelocity) > MAX_LAUNCH_ANGLE) return false;
            return true;
        }

        /// <summary>Gives the server-only AI shoot component this character's projectile variants.</summary>
        internal void ConfigureAiShoot(NetworkedAIShoot aiShoot)
        {
            if (aiShoot == null) return;
            aiShoot.ConfigureProjectilePrefabs(
                _networkedNormalBulletPrefab,
                _networkedPushBulletPrefab,
                _networkedMinePrefab);
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
