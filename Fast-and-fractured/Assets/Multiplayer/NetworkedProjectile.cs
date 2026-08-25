using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Connection;
using UnityEngine;
using FastAndFractured.Utilities;
using FishNet;
using FastAndFractured;
using System.Reflection;
using Enums;
using FishNet.Component.Transforming.Beta;

namespace FastAndFractured.Multiplayer
{
    [RequireComponent(typeof(ICustomRigidbody), typeof(Collider))]
    public class NetworkedProjectile : NetworkBehaviour
    {
        #region PREDICTION_DATA_STRUCTS
        public struct MoveData : IReplicateData
        {
            private uint _tick;
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        public struct ReconcileData : IReconcileData
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Velocity;
            public Vector3 AngularVelocity;
            public int Bounces;
            public bool IsActive;
            public bool IsFrozenMine;

            public ReconcileData(Vector3 pos, Quaternion rot, Vector3 vel, Vector3 angVel, int bounces, bool isActive, bool isFrozenMine)
            {
                Position = pos;
                Rotation = rot;
                Velocity = vel;
                AngularVelocity = angVel;
                Bounces = bounces;
                IsActive = isActive;
                IsFrozenMine = isFrozenMine;
                _tick = 0;
            }

            private uint _tick;
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }
        #endregion

        #region REFERENCES_AND_STATE
        private ICustomRigidbody _customRigidbody;
        private Collider _ownCollider;
        private FastAndFractured.Utilities.LocalEventRunner _localEventRunner;

        // FishNet-internal reflection (not centralized in SingleplayerReflectionCache because
        // these are FishNet types, not singleplayer types).
        private static FieldInfo _tickSmootherInitSettingsField;
        private static FieldInfo _networkObjectGraphicalObjectField;
        private static FieldInfo _networkObjectPredictionTypeField;
        private static FieldInfo _networkObjectLocalReconcileCorrectionTypeField;
        private static FieldInfo _networkObjectEnableStateForwardingField;

        [Header("Efectos")]
        [SerializeField] private GameObject _visuals;
        [SerializeField] private GameObject _impactParticlesPrefab;
        [SerializeField] private GameObject _explosionPrefab;

        private uint _despawnTick;

        private bool _isInitialized = false;
        private NetworkObject _shooter; // Reference to the shooter for robust collision ignoring
        private float _damage;
        private LayerMask _collisionMask;
        private Collider[] _ignoredColliders;
        private NetworkConnection _attackerConnection;

        private float _pushForce;
        private float _explosionRadius;
        private Vector3 _explosionCenterOffset;
        private float _bounceStrength;
        private int _maxBounces;
        private int _currentBounces;
        private Vector3 _customGravity;
        private bool _useCustomGravity;
        private float _timeToExplode;
        private bool _isMine;
        private Transform _runtimeSmoothingTarget;

        private const int PositionHistoryCapacity = 10;
        private readonly Vector3[] _positionHistory = new Vector3[PositionHistoryCapacity];
        private int _positionHistoryCount;
        private int _positionHistoryNextIndex;

        // Tunables previously hard-coded inline
        private const float DEFAULT_LIFETIME_SECONDS = 5f;
        private const float SPHERE_CAST_RADIUS = 0.15f;
        private const float BOUNCE_OFFSET_FROM_SURFACE = 0.15f;
        private const float MIN_VELOCITY_FOR_RAYCAST = 0.01f;
        private const float MIN_BOUNCE_DECAY = 0.1f;
        private const float DEFAULT_BOUNCE_LOSS_FACTOR = 0.8f;
        private const float NON_VEHICLE_PUSH_FORCE_MULTIPLIER = 0.5f;
        private const float MIN_DIRECTION_SQR_MAG = 0.001f;
        private const float MIN_VELOCITY_FOR_DIRECTION = 0.0001f;
        private const float LATE_UPDATE_RESYNC_THRESHOLD_SQR = 0.0001f;

        // One mask per projectile layer, mirroring the Unity layer collision matrix. Cached because
        // the matrix cannot change at runtime.
        private static readonly int[] _collisionMaskByLayer = new int[32];
        private static readonly bool[] _collisionMaskByLayerCached = new bool[32];
        private static int _cachedCharactersLayer = -1;
        private static int _cachedDefaultLayer = -1;

        private static int CharactersLayer
        {
            get
            {
                if (_cachedCharactersLayer < 0) _cachedCharactersLayer = LayerMask.NameToLayer("Characters");
                return _cachedCharactersLayer;
            }
        }

        /// <summary>
        /// The set of layers a collider on <paramref name="layer"/> would actually collide with,
        /// taken from the project's layer collision matrix. This sweep stands in for a real
        /// Rigidbody collision, so it has to agree with the matrix or the networked bullet behaves
        /// differently from the singleplayer one. Naming the layers by hand does not work:
        /// LayerMask.GetMask contributes nothing, silently, for a name that does not exist, which is
        /// how "Obstacles" (never a layer in this project) hid the fact that Static was missing -
        /// and Static carries most of the map, so bullets and mines fell straight through the world.
        /// </summary>
        private static int GetCollisionMaskForLayer(int layer)
        {
            if (layer < 0 || layer > 31) layer = 0;
            if (!_collisionMaskByLayerCached[layer])
            {
                int mask = 0;
                for (int other = 0; other < 32; other++)
                {
                    if (!Physics.GetIgnoreLayerCollision(layer, other))
                        mask |= 1 << other;
                }
                _collisionMaskByLayer[layer] = mask;
                _collisionMaskByLayerCached[layer] = true;
            }
            return _collisionMaskByLayer[layer];
        }

        private static int DefaultLayer
        {
            get
            {
                if (_cachedDefaultLayer < 0) _cachedDefaultLayer = LayerMask.NameToLayer("Default");
                return _cachedDefaultLayer;
            }
        }

        // Mine frozen state: bullet sits on ground waiting to explode
        private bool _isFrozenMine;
        private uint _mineExplodeTick;
        private uint _lastSimulatedTick;
        private Vector3 _simulatedPosition;
        private Vector3 _simulatedVelocity;
        private bool _simulationStateValid;
        #endregion

        #region FISHNET_LIFECYCLE
        private void Awake()
        {
            _customRigidbody = GetComponent<ICustomRigidbody>();
            _ownCollider = GetComponent<Collider>();
            _localEventRunner = GetComponent<FastAndFractured.Utilities.LocalEventRunner>();
            
            if (_customRigidbody is MultiplayerRigidbodyAdapter adapter)
            {
                adapter.InitializeAdapter();
            }
            
            // DestroyImmediate LocalEventRunner  ENetworkedProjectile handles all physics.
            // Must be Immediate because FishNet's Physics.Simulate() can fire OnTriggerEnter
            // within the SAME FRAME before deferred Destroy() takes effect.
            if (_localEventRunner != null)
            {
                DestroyImmediate(_localEventRunner);
                _localEventRunner = null;
            }

            // Also DestroyImmediate the original BulletBehaviour to prevent NullRef
            // from uninitialized _ignoreCollider during physics replay
            var bulletBehaviour = GetComponent<BulletBehaviour>();
            if (bulletBehaviour != null)
            {
                DestroyImmediate(bulletBehaviour);
            }

            // Destroy singleplayer ExplosionForce components (on ExplosionHitbox child).
            // Their OnTriggerEnter fires during FishNet Physics.Simulate() with null _explosionCollider.
            // NetworkedProjectile handles explosion via ApplyExplosionForce() using OverlapSphere instead.
            foreach (var ef in GetComponentsInChildren<ExplosionForce>(true))
            {
                // Deactivate the entire ExplosionHitbox GameObject.
                // In singleplayer, PushBulletBehaviour.InitBulletTrayectory() calls DeactivateExplosionHitbox()
                // but since BulletBehaviour was destroyed, it never runs.
                // The child has VFX + SphereCollider trigger that would fire during Physics.Simulate.
                ef.gameObject.SetActive(false);
                DestroyImmediate(ef);
            }

            NormalizeRuntimePredictionSettings();

            // Keep NetworkTickSmoother active for better remote projectile visibility.
            // We keep the component alive (don't DestroyImmediate) because removing it from
            // FishNet's NetworkBehaviours list shifts RPC link indices, causing
            // ArgumentOutOfRangeException in ParseRpcLink on the client side.
            var tickSmoother = GetComponent<FishNet.Component.Transforming.Beta.NetworkTickSmoother>();
            if (tickSmoother != null)
            {
                EnsureTickSmootherTargetTransform(tickSmoother);
                tickSmoother.enabled = false;
            }

            EnsureNetworkObjectGraphicalBinding();
        }

        private bool _subscribedToTimeManager = false;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            EnsureNetworkObjectGraphicalBinding();
            if (!_subscribedToTimeManager)
            {
                base.TimeManager.OnTick += TimeManager_OnTick;
                base.TimeManager.OnPostTick += TimeManager_OnPostTick;
                _subscribedToTimeManager = true;
            }
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (base.TimeManager != null && _subscribedToTimeManager)
            {
                base.TimeManager.OnTick -= TimeManager_OnTick;
                base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
                _subscribedToTimeManager = false;
            }
        }

        // RunLocally is required so the server initializes authoritative projectile simulation too.
        [ObserversRpc(RunLocally = true)]
        public void RpcInitialize(NetworkShootParams data, NetworkObject ignorer, Vector3 spawnPosition, Quaternion spawnRotation)
        {
            InitializeProjectile(data, ignorer, spawnPosition, spawnRotation);
        }

        internal void InitializeProjectile(NetworkShootParams data, NetworkObject ignorer, Vector3 spawnPosition, Quaternion spawnRotation)
        {
            if (_isInitialized) return;
            _isInitialized = true;
            _shooter = ignorer;
            _attackerConnection = ignorer != null ? ignorer.Owner : null;
            _lastSimulatedTick = uint.MaxValue;

            // Set spawn position/rotation FIRST (critical for client-side)
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            if (_customRigidbody != null)
            {
                // Zero velocities BEFORE marking kinematic. Setting linear/angular velocity on
                // an already-kinematic body logs a Unity warning and is silently dropped.
                _customRigidbody.linearVelocity = Vector3.zero;
                _customRigidbody.angularVelocity = Vector3.zero;
                _customRigidbody.position = spawnPosition;
                _customRigidbody.rotation = spawnRotation;
                _customRigidbody.isKinematic = true;
                _customRigidbody.useGravity = false;
            }

            gameObject.name = $"{gameObject.name}_Owner({(_attackerConnection != null ? _attackerConnection.ClientId : -1)})_Tick({base.TimeManager.Tick})";

            _simulatedPosition = spawnPosition;
            _simulatedVelocity = data.InitialVelocity;
            _simulationStateValid = true;
            _damage = data.Damage;
            float lifetime = (data.InitialVelocity.magnitude > 0.1f) ? data.Range / data.InitialVelocity.magnitude : DEFAULT_LIFETIME_SECONDS;

            _collisionMask = GetCollisionMaskForLayer(gameObject.layer);
            _customGravity = data.CustomGravity;
            _useCustomGravity = (data.BouncingNum > 0 || data.IsMine);

            if (ignorer != null)
            {
                // Ignore ALL colliders on the shooter's vehicle hierarchy (vehicles have multiple colliders)
                var shooterColliders = ignorer.GetComponentsInChildren<Collider>(true);
                if (shooterColliders.Length > 0)
                {
                    _ignoredColliders = shooterColliders;
                    if (_ownCollider != null)
                    {
                        foreach (var c in _ignoredColliders)
                            Physics.IgnoreCollision(_ownCollider, c, true);
                    }
                }
            }
            _pushForce = data.PushForce;
            _explosionRadius = data.ExplosionRadius;
            _explosionCenterOffset = data.ExplosionCenterOffset;
            _bounceStrength = data.BouncingStrenght;
            _maxBounces = data.BouncingNum;
            _timeToExplode = data.TimeToExplode;
            _isMine = data.IsMine;
            _isFrozenMine = false;

            EnsureNetworkObjectGraphicalBinding();
            _despawnTick = base.TimeManager.Tick + base.TimeManager.TimeToTicks(lifetime);
            _positionHistoryCount = 0;
            _positionHistoryNextIndex = 0;
            RecordPositionSample(GetSimulationPosition());

        }

        private void TimeManager_OnTick()
        {
            if (base.IsOwner)
            {
                Replicate(default, ReplicateState.Ticked, Channel.Unreliable);
            }
            else if (base.IsServerInitialized)
            {
                Replicate(default, ReplicateState.Ticked, Channel.Unreliable);
            }
            else
            {
                // Observers (non-owner, non-server) simulate locally for smooth visuals
                SimulateProjectile((float)base.TimeManager.TickDelta, false);
            }
        }

        private void TimeManager_OnPostTick()
        {
            if (base.IsServerInitialized)
            {
                CreateReconcile();
            }
        }

        public override void CreateReconcile()
        {
            if (!base.IsServer) return;

            var rd = new ReconcileData(
                GetSimulationPosition(),
                _customRigidbody != null ? _customRigidbody.rotation : transform.rotation,
                _simulatedVelocity,
                Vector3.zero,
                _currentBounces,
                gameObject.activeSelf,
                _isFrozenMine
            );

            // Send reconcile to owner if they exist, or broadcast for ownerless objects (AI bullets).
            // FishNet sends reconcile data to the owner; for ownerless objects we still call it
            // so the server can track state. Spectators receive state via ObserversRpc (RpcInitialize).
            Reconcile(rd, Channel.Reliable);
        }
        #endregion

        /// <summary>
        /// FishNet NetworkTickSmoother requires InitializationSettings.TargetTransform.
        /// Some converted projectile prefabs have this unset, which causes runtime errors on clients.
        /// </summary>
        private void EnsureTickSmootherTargetTransform(NetworkTickSmoother tickSmoother)
        {
            if (tickSmoother == null) return;

            if (_tickSmootherInitSettingsField == null)
            {
                _tickSmootherInitSettingsField = typeof(NetworkTickSmoother).GetField("_initializationSettings",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            if (_tickSmootherInitSettingsField == null) return;

            object boxedSettings = _tickSmootherInitSettingsField.GetValue(tickSmoother);
            if (!(boxedSettings is InitializationSettings))
                return;

            InitializationSettings settings = (InitializationSettings)boxedSettings;

            Transform targetTransform = ResolveSmootherTargetTransform();
            if (settings.TargetTransform == null)
            {
                settings.TargetTransform = targetTransform;
                _tickSmootherInitSettingsField.SetValue(tickSmoother, settings);
            }
            else if (settings.TargetTransform == transform && targetTransform != transform)
            {
                settings.TargetTransform = targetTransform;
                _tickSmootherInitSettingsField.SetValue(tickSmoother, settings);
            }
        }

        private Transform ResolveSmootherTargetTransform()
        {
            if (_visuals != null && _visuals.transform != transform)
            {
                return _visuals.transform;
            }

            foreach (Transform child in transform)
            {
                if (child != null)
                {
                    return child;
                }
            }

            if (_runtimeSmoothingTarget == null)
            {
                var go = new GameObject("SmoothingTarget");
                _runtimeSmoothingTarget = go.transform;
                _runtimeSmoothingTarget.SetParent(transform, false);
                _runtimeSmoothingTarget.localPosition = Vector3.zero;
                _runtimeSmoothingTarget.localRotation = Quaternion.identity;
                _runtimeSmoothingTarget.localScale = Vector3.one;
            }

            return _runtimeSmoothingTarget;
        }

        private void EnsureNetworkObjectGraphicalBinding()
        {
            NetworkObject nob = GetComponent<NetworkObject>();
            if (nob == null) return;

            if (_networkObjectGraphicalObjectField == null)
            {
                _networkObjectGraphicalObjectField = typeof(NetworkObject).GetField("_graphicalObject",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            if (_networkObjectGraphicalObjectField == null) return;

            Transform current = _networkObjectGraphicalObjectField.GetValue(nob) as Transform;
            if (current != null) return;

            Transform target = ResolveSmootherTargetTransform();
            if (target != null && target != transform)
            {
                _networkObjectGraphicalObjectField.SetValue(nob, target);
            }
        }

        private void NormalizeRuntimePredictionSettings()
        {
            NetworkObject nob = GetComponent<NetworkObject>();
            if (nob == null) return;

            if (_networkObjectPredictionTypeField == null)
            {
                _networkObjectPredictionTypeField = typeof(NetworkObject).GetField("_predictionType",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }
            if (_networkObjectLocalReconcileCorrectionTypeField == null)
            {
                _networkObjectLocalReconcileCorrectionTypeField = typeof(NetworkObject).GetField("_localReconcileCorrectionType",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }
            if (_networkObjectEnableStateForwardingField == null)
            {
                _networkObjectEnableStateForwardingField = typeof(NetworkObject).GetField("_enableStateForwarding",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            // Force "Other" prediction type for projectile wrappers to avoid host-side rigidbody correction warping.
            if (_networkObjectPredictionTypeField != null)
            {
                object enumValue = System.Enum.ToObject(_networkObjectPredictionTypeField.FieldType, 0);
                _networkObjectPredictionTypeField.SetValue(nob, enumValue);
            }

            // Disable local rigidbody correction for projectiles; server authority decides lifecycle.
            if (_networkObjectLocalReconcileCorrectionTypeField != null)
            {
                object enumValue = System.Enum.ToObject(_networkObjectLocalReconcileCorrectionTypeField.FieldType, 0);
                _networkObjectLocalReconcileCorrectionTypeField.SetValue(nob, enumValue);
            }

            // Do not forward prediction states for projectile wrappers; non-server peers rely on local simulation visuals.
            if (_networkObjectEnableStateForwardingField != null)
            {
                _networkObjectEnableStateForwardingField.SetValue(nob, false);
            }
        }

        #region SIMULATION_LOGIC
        [Replicate]
        private void Replicate(MoveData md, ReplicateState state, Channel channel)
        {
            if (!_isInitialized) return;
            if (base.IsServerInitialized && state.ContainsReplayed()) return;
            if (!TryBeginTickSimulation()) return;

            bool replaying = state.ContainsReplayed();
            SimulateProjectile((float)base.TimeManager.TickDelta, replaying);
        }

        private void SimulateProjectile(float delta, bool replaying)
        {
            if (!_isInitialized) return;
            Vector3 simulationPosition = GetSimulationPosition();
            RecordPositionSample(simulationPosition);

            // Frozen mine: waiting to explode, skip physics
            if (_isFrozenMine)
            {
                if (base.IsServer && base.TimeManager.Tick >= _mineExplodeTick)
                {
                    _isFrozenMine = false;
                    HandleImpact(simulationPosition, Vector3.up, null, replaying);
                }
                return;
            }

            if (_useCustomGravity)
            {
                _simulatedVelocity += _customGravity * delta;
            }

            if (!HandleCollisions(replaying, simulationPosition, delta))
            {
                _simulatedPosition = simulationPosition + (_simulatedVelocity * delta);
                ApplySimulationPose();

                if (base.IsServer && base.TimeManager.Tick >= _despawnTick)
                {
                    HandleImpact(_simulatedPosition, Vector3.up, null, replaying);
                }
            }

            simulationPosition = GetSimulationPosition();
            RecordPositionSample(simulationPosition);
        }

        [Reconcile]
        private void Reconcile(ReconcileData rd, Channel channel)
        {
            if (base.IsServerInitialized) return;
            if (!rd.IsActive) return;

            _simulatedPosition = rd.Position;
            _simulatedVelocity = rd.Velocity;
            _simulationStateValid = true;
            if (_customRigidbody != null)
            {
                _customRigidbody.rotation = rd.Rotation;
            }
            ApplySimulationPose();
            _currentBounces = rd.Bounces;
            _isFrozenMine = rd.IsFrozenMine;
        }

        private bool HandleCollisions(bool replaying, Vector3 simulationPosition, float delta)
        {
            float distance = _simulatedVelocity.magnitude * delta;
            if (distance < MIN_VELOCITY_FOR_RAYCAST) return false;

            // QueryTriggerInteraction.Ignore because this sweep stands in for OnCollisionEnter, which
            // never fires against a trigger. Without it the projectile stops dead inside invisible
            // volumes (interactable pickups, safe zones, the shield bubble).
            if (Physics.SphereCast(simulationPosition, SPHERE_CAST_RADIUS, _simulatedVelocity.normalized, out RaycastHit hit, distance, _collisionMask, QueryTriggerInteraction.Ignore))
            {
                // Ignore self-collider
                if (_ownCollider != null && (hit.collider == _ownCollider || hit.collider.transform == transform))
                    return false;

                // Robust check: Ignore ALL colliders in the shooter's hierarchy (even if GetComponentsInChildren missed them)
                if (_shooter != null && hit.collider.transform.root == _shooter.transform.root)
                    return false;

                if (_ignoredColliders != null)
                {
                    foreach (var ignored in _ignoredColliders)
                    {
                        if (ignored != null && (hit.collider == ignored || hit.collider.transform.IsChildOf(ignored.transform)))
                            return false;
                    }
                }

                // Hit a character ->explode immediately (mirrors singleplayer PushBulletBehaviour)
                bool hitCharacter = hit.collider.gameObject.layer == CharactersLayer;

                if (!hitCharacter && _currentBounces < _maxBounces)
                {
                    _currentBounces++;
                    Vector3 reflection = Vector3.Reflect(_simulatedVelocity, hit.normal);
                    // Decay bounce strength per bounce (mirrors singleplayer formula)
                    float decay = 1f - ((float)_currentBounces / _maxBounces);
                    float loss = _bounceStrength > 0 ? _bounceStrength * Mathf.Max(decay, MIN_BOUNCE_DECAY) : DEFAULT_BOUNCE_LOSS_FACTOR;
                    _simulatedVelocity = reflection * loss;
                    _simulatedPosition = hit.point + hit.normal * BOUNCE_OFFSET_FROM_SURFACE;
                    ApplySimulationPose();
                }
                else if (!hitCharacter && _currentBounces >= _maxBounces && _timeToExplode > 0f)
                {
                    // Mine: rest on the surface it hit, wait _timeToExplode, then explode.
                    // The position has to be written explicitly. Returning true only tells the caller
                    // to skip the integration step, so without this the mine freezes at wherever the
                    // previous tick left it instead of on the ground it just hit.
                    _simulatedPosition = hit.point + hit.normal * BOUNCE_OFFSET_FROM_SURFACE;
                    _simulatedVelocity = Vector3.zero;
                    _useCustomGravity = false;
                    _isFrozenMine = true;
                    // Every peer freezes the mine so it stays visible and predicts correctly, but
                    // only the server owns when it goes off. A client that fell through to the impact
                    // branch instead used to hide the visuals and disable the collider permanently,
                    // so the mine simply vanished the moment it landed.
                    if (base.IsServer)
                        _mineExplodeTick = base.TimeManager.Tick + base.TimeManager.TimeToTicks(_timeToExplode);
                    ApplySimulationPose();
                }
                else
                {
                    _simulatedPosition = hit.point;
                    ApplySimulationPose();
                    HandleImpact(hit.point, hit.normal, hit.collider.gameObject, replaying);
                }
                return true;
            }
            return false;
        }

        private void HandleImpact(Vector3 point, Vector3 normal, GameObject hitObject, bool replaying)
        {
            if (!replaying)
            {
                if (_visuals != null) _visuals.SetActive(false);
                _ownCollider.enabled = false;
                _simulatedVelocity = Vector3.zero;
                ApplySimulationPose();
            }

            if (base.IsServer)
            {
                if (_explosionRadius > 0f)
                {
                    // Push bullet / mine: area-of-effect push force
                    ApplyExplosionForce(point);
                    RpcSpawnExplosionVFX(point, normal);
                }
                else
                {
                    // Normal bullet: point damage to single target
                    if (hitObject != null) ApplyPointDamage(hitObject);
                    RpcSpawnImpactVFX(point, normal);
                }

                InstanceFinder.ServerManager.Despawn(base.NetworkObject, DespawnType.Destroy);
            }
        }
        #endregion

        #region EXPLOSION_AND_DAMAGE
        /// <summary>
        /// Server-side: find all vehicles in explosion radius and apply push force.
        /// Mirrors singleplayer ExplosionForce.OnTriggerEnter() logic.
        /// </summary>
        private void ApplyExplosionForce(Vector3 center)
        {
            Vector3 explosionCenter = center + _explosionCenterOffset;
            LayerMask explosionMask = (1 << CharactersLayer) | (1 << DefaultLayer);

            Collider[] hits = Physics.OverlapSphere(explosionCenter, _explosionRadius, explosionMask);
            var pushedSet = new System.Collections.Generic.HashSet<PhysicsBehaviour>();

            foreach (var hit in hits)
            {
                // Skip shooter's own colliders
                if (_ignoredColliders != null)
                {
                    bool skip = false;
                    foreach (var ignored in _ignoredColliders)
                    {
                        if (ignored != null && (hit == ignored || hit.transform.IsChildOf(ignored.transform)))
                        { skip = true; break; }
                    }
                    if (skip) continue;
                }

                var physicsBehaviour = hit.GetComponentInParent<PhysicsBehaviour>();
                if (physicsBehaviour != null && pushedSet.Add(physicsBehaviour))
                {
                    var carImpact = physicsBehaviour.CarImpactHandler;
                    if (carImpact != null && carImpact.CheckForModifiedCarState() == ModifiedCarState.JOSEFINO_INVULNERABLE)
                    {
                        carImpact.OnHasBeenPushed(physicsBehaviour);
                        continue;
                    }

                    physicsBehaviour.CancelDash();

                    var stats = physicsBehaviour.StatsController;
                    float enduranceFactor = stats.Endurance / stats.MaxEndurance;
                    float weight = stats.Weight;
                    float enduranceImportance = stats.EnduranceImportanceWhenColliding;

                    Vector3 closestPoint = hit.ClosestPointOnBounds(explosionCenter);
                    Vector3 direction = (closestPoint - explosionCenter).normalized;
                    if (direction.sqrMagnitude < MIN_DIRECTION_SQR_MAG) direction = Vector3.up;

                    float forceToApply = physicsBehaviour.CalculateForceToApplyToOtherCar(
                        enduranceFactor, weight, enduranceImportance, _pushForce);

                    if (!physicsBehaviour.HasBeenPushed)
                    {
                        physicsBehaviour.ApplyForce(direction, closestPoint, forceToApply, ForceMode.Impulse);
                        if (carImpact != null) carImpact.OnHasBeenPushed(physicsBehaviour);
                    }
                }
                else if (physicsBehaviour == null)
                {
                    // Non-vehicle rigidbody: generic push
                    var rb = hit.GetComponentInParent<Rigidbody>();
                    if (rb != null)
                    {
                        Vector3 closestPoint = hit.ClosestPointOnBounds(explosionCenter);
                        Vector3 direction = (closestPoint - explosionCenter).normalized;
                        if (direction.sqrMagnitude < MIN_DIRECTION_SQR_MAG) direction = Vector3.up;
                        rb.AddForceAtPosition(direction * _pushForce * NON_VEHICLE_PUSH_FORCE_MULTIPLIER, closestPoint, ForceMode.Impulse);
                    }
                }
            }
        }

        /// <summary>
        /// Server-side: apply point damage to a single hit target (normal bullets).
        /// </summary>
        private void ApplyPointDamage(GameObject hitObject)
        {
            var networkedStats = hitObject.GetComponentInParent<NetworkedStatsController>();
            if (networkedStats != null && _attackerConnection != null && _attackerConnection.IsValid)
            {
                networkedStats.DealDamageServerRpc(_damage, _attackerConnection);
            }
            else
            {
                var stats = hitObject.GetComponentInParent<StatsController>();
                if (stats != null)
                {
                    GameObject attackerObject = _attackerConnection != null && _attackerConnection.IsValid && _attackerConnection.FirstObject != null
                        ? _attackerConnection.FirstObject.gameObject
                        : gameObject;

                    var hitNob = hitObject.GetComponentInParent<FishNet.Object.NetworkObject>(true);
                    bool isOwnerVehicle = hitNob != null && hitNob.IsOwner;

                    if (!isOwnerVehicle)
                    {
                        SingleplayerReflectionCache.RunWithoutPlayerFlag(stats,
                            () => stats.TakeEndurance(_damage, false, attackerObject));
                    }
                    else
                    {
                        stats.TakeEndurance(_damage, false, attackerObject);
                    }
                }
            }
        }
        #endregion

        #region VFX_RPCS
        private const float VFX_AUTO_DESTROY_TIME = 5f;

        [ObserversRpc]
        private void RpcSpawnExplosionVFX(Vector3 point, Vector3 normal)
        {
            // Screen shake for everyone near the blast. The singleplayer ExplosionForce (which is
            // what normally triggers the Explosion profile) is stripped from networked projectiles,
            // so without this no peer — not even the shooter — feels an explosion.
            NetworkedScreenShake.PlayAtPosition(ScreenShakeProfileType.Explosion, point);

            if (_explosionPrefab != null)
            {
                var vfx = Instantiate(_explosionPrefab, point, Quaternion.LookRotation(normal));
                vfx.transform.localScale = Vector3.one * _explosionRadius;
                Destroy(vfx, VFX_AUTO_DESTROY_TIME);
            }
        }

        [ObserversRpc]
        private void RpcSpawnImpactVFX(Vector3 point, Vector3 normal)
        {
            if (_impactParticlesPrefab != null)
            {
                var vfx = Instantiate(_impactParticlesPrefab, point, Quaternion.LookRotation(normal));
                Destroy(vfx, VFX_AUTO_DESTROY_TIME);
            }
        }
        #endregion

        private void RecordPositionSample(Vector3 position)
        {
            _positionHistory[_positionHistoryNextIndex] = position;
            _positionHistoryNextIndex = (_positionHistoryNextIndex + 1) % PositionHistoryCapacity;
            if (_positionHistoryCount < PositionHistoryCapacity)
            {
                _positionHistoryCount++;
            }
        }

        private Vector3 GetMovementDirection()
        {
            Vector3 velocity = _simulatedVelocity;
            return velocity.sqrMagnitude > MIN_VELOCITY_FOR_DIRECTION ? velocity.normalized : Vector3.zero;
        }

        private Vector3 GetSimulationPosition()
        {
            if (_simulationStateValid)
            {
                return _simulatedPosition;
            }
            return _customRigidbody != null ? _customRigidbody.position : transform.position;
        }

        private void ApplySimulationPose()
        {
            if (!_simulationStateValid)
            {
                return;
            }

            if (_customRigidbody != null)
            {
                _customRigidbody.position = _simulatedPosition;
            }

            if (transform.position != _simulatedPosition)
            {
                transform.SetPositionAndRotation(_simulatedPosition, transform.rotation);
            }
        }

        private void LateUpdate()
        {
            if (!_isInitialized || !_simulationStateValid)
            {
                return;
            }

            if (Vector3.SqrMagnitude(transform.position - _simulatedPosition) > LATE_UPDATE_RESYNC_THRESHOLD_SQR)
            {
                transform.SetPositionAndRotation(_simulatedPosition, transform.rotation);
            }
        }

        private bool TryBeginTickSimulation()
        {
            if (!_isInitialized || base.TimeManager == null)
            {
                return false;
            }

            uint tick = base.TimeManager.Tick;
            if (_lastSimulatedTick == tick)
            {
                return false;
            }

            _lastSimulatedTick = tick;
            return true;
        }

    }
}
