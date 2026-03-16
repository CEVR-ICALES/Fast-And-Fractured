// RUTA: Assets/FastAndFractured/Multiplayer/NetworkedProjectile.cs

using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;
using FastAndFractured.Utilities;
using FishNet;
using FastAndFractured;
using System.Reflection;
using Enums;

namespace FastAndFractured.Multiplayer
{
    [RequireComponent(typeof(ICustomRigidbody), typeof(Collider))]
    public class NetworkedProjectile : NetworkBehaviour
    {
        // ----------------- DATOS DE PREDICCIÓN -----------------
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

        /// <summary>
        /// Cached reflection field for StatsController._isPlayer.
        /// Used to suppress HUD updates when applying damage to non-owner vehicles.
        /// </summary>
        private static FieldInfo _isPlayerField;

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

        // Mine frozen state: bullet sits on ground waiting to explode
        private bool _isFrozenMine;
        private uint _mineExplodeTick;
        #endregion

        // ... (El Awake, OnStartNetwork y OnStopNetwork no cambian) ...
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
            
            // DestroyImmediate LocalEventRunner — NetworkedProjectile handles all physics.
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

            // Disable NetworkTickSmoother — its TargetTransform is null on projectile prefabs.
            // We keep the component alive (don't DestroyImmediate) because removing it from
            // FishNet's NetworkBehaviours list shifts RPC link indices, causing
            // ArgumentOutOfRangeException in ParseRpcLink on the client side.
            var tickSmoother = GetComponent<FishNet.Component.Transforming.Beta.NetworkTickSmoother>();
            if (tickSmoother != null)
            {
                tickSmoother.enabled = false;
            }
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            base.TimeManager.OnTick += TimeManager_OnTick;
            base.TimeManager.OnPostTick += TimeManager_OnPostTick;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (base.TimeManager != null)
            {
                base.TimeManager.OnTick -= TimeManager_OnTick;
                base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
            }
        }

        // ---------- LA CORRECCIÓN ESTÁ AQUÍ ----------

        [ObserversRpc]
        public void RpcInitialize(NetworkShootParams data, NetworkObject ignorer, Vector3 spawnPosition, Quaternion spawnRotation)
        {
            if (_isInitialized) return;
            _isInitialized = true;
            _shooter = ignorer;

            // Set spawn position/rotation FIRST (critical for client-side)
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            if (_customRigidbody != null)
            {
                _customRigidbody.position = spawnPosition;
                _customRigidbody.rotation = spawnRotation;
            }

            gameObject.name = $"{gameObject.name}_Owner({(ignorer != null ? ignorer.OwnerId : -1)})_Tick({base.TimeManager.Tick})";

            _customRigidbody.linearVelocity = data.InitialVelocity;
            _damage = data.Damage;
            float lifetime = (data.InitialVelocity.magnitude > 0.1f) ? data.Range / data.InitialVelocity.magnitude : 5f;

            _collisionMask = LayerMask.GetMask("Default", "Characters", "Ground", "Obstacles");
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

            _despawnTick = base.TimeManager.Tick + base.TimeManager.TimeToTicks(lifetime);
        }

        // ---------- FIN DE LA CORRECCIÓN PRINCIPAL ----------

        private void TimeManager_OnTick()
        {
            if (base.IsOwner)
            {
                Replicate(default, ReplicateState.Ticked, Channel.Unreliable);
            }
            else if (base.IsServer)
            {
                Replicate(default, ReplicateState.Ticked, Channel.Unreliable);
            }
            else
            {
                // Observers simulate locally
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
                _customRigidbody.position,
                _customRigidbody.rotation,
                _customRigidbody.linearVelocity,
                _customRigidbody.angularVelocity,
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

        #region SIMULATION_LOGIC
        [Replicate]
        private void Replicate(MoveData md, ReplicateState state, Channel channel)
        {
            if (!_isInitialized) return;

            bool replaying = state.ContainsReplayed();
            SimulateProjectile((float)base.TimeManager.TickDelta, replaying);
        }

        private void SimulateProjectile(float delta, bool replaying)
        {
            if (!_isInitialized) return;

            // Frozen mine: waiting to explode, skip physics
            if (_isFrozenMine)
            {
                if ((base.IsServer || base.IsOwner) && base.TimeManager.Tick >= _mineExplodeTick)
                {
                    _isFrozenMine = false;
                    HandleImpact(transform.position, Vector3.up, null, replaying);
                }
                return;
            }

            if (_useCustomGravity)
            {
                _customRigidbody.linearVelocity += _customGravity * delta;
            }

            _customRigidbody.Simulate();

            if (!HandleCollisions(replaying))
            {
                if ((base.IsServer || base.IsOwner) && base.TimeManager.Tick >= _despawnTick)
                {
                    HandleImpact(transform.position, Vector3.up, null, replaying);
                }
            }
        }

        [Reconcile]
        private void Reconcile(ReconcileData rd, Channel channel)
        {
            if (!rd.IsActive) return;

            _customRigidbody.position = rd.Position;
            _customRigidbody.rotation = rd.Rotation;
            if (!_customRigidbody.isKinematic)
            {
                _customRigidbody.linearVelocity = rd.Velocity;
                _customRigidbody.angularVelocity = rd.AngularVelocity;
            }
            _currentBounces = rd.Bounces;
            _isFrozenMine = rd.IsFrozenMine;
        }

        private bool HandleCollisions(bool replaying)
        {
            float tickDelta = (float)base.TimeManager.TickDelta;
            float distance = _customRigidbody.linearVelocity.magnitude * tickDelta;
            if (distance < 0.01f) return false;

            if (Physics.SphereCast(transform.position, 0.15f, _customRigidbody.linearVelocity.normalized, out RaycastHit hit, distance, _collisionMask))
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

                Debug.Log($"[NetworkedProjectile] Hit: {hit.collider.name} on Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");

                // Hit a character → explode immediately (mirrors singleplayer PushBulletBehaviour)
                bool hitCharacter = hit.collider.gameObject.layer == LayerMask.NameToLayer("Characters");

                if (!hitCharacter && _currentBounces < _maxBounces)
                {
                    _currentBounces++;
                    Vector3 reflection = Vector3.Reflect(_customRigidbody.linearVelocity, hit.normal);
                    // Decay bounce strength per bounce (mirrors singleplayer formula)
                    float decay = 1f - ((float)_currentBounces / _maxBounces);
                    float loss = _bounceStrength > 0 ? _bounceStrength * Mathf.Max(decay, 0.1f) : 0.8f;
                    _customRigidbody.linearVelocity = reflection * loss;
                    _customRigidbody.position = hit.point + hit.normal * 0.15f;
                }
                else if (!hitCharacter && _currentBounces >= _maxBounces && _timeToExplode > 0f && base.IsServer)
                {
                    // Mine: freeze in place, wait _timeToExplode ticks, then explode
                    _customRigidbody.linearVelocity = Vector3.zero;
                    _customRigidbody.isKinematic = true;
                    _useCustomGravity = false;
                    _isFrozenMine = true;
                    _mineExplodeTick = base.TimeManager.Tick + base.TimeManager.TimeToTicks(_timeToExplode);
                }
                else
                {
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
                if (_customRigidbody != null)
                {
                    _customRigidbody.linearVelocity = Vector3.zero;
                    _customRigidbody.isKinematic = true;
                }
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
            int vehicleLayer = LayerMask.NameToLayer("Characters");
            int defaultLayer = LayerMask.NameToLayer("Default");
            LayerMask explosionMask = (1 << vehicleLayer) | (1 << defaultLayer);

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
                    if (direction.sqrMagnitude < 0.001f) direction = Vector3.up;

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
                        if (direction.sqrMagnitude < 0.001f) direction = Vector3.up;
                        rb.AddForceAtPosition(direction * _pushForce * 0.5f, closestPoint, ForceMode.Impulse);
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
            if (networkedStats != null && base.Owner.IsValid)
            {
                networkedStats.DealDamageServerRpc(_damage, base.Owner);
            }
            else
            {
                var stats = hitObject.GetComponentInParent<StatsController>();
                if (stats != null)
                {
                    GameObject attackerObject = base.Owner.IsValid && base.Owner.FirstObject != null
                        ? base.Owner.FirstObject.gameObject
                        : gameObject;

                    var hitNob = hitObject.GetComponentInParent<FishNet.Object.NetworkObject>(true);
                    bool isOwnerVehicle = hitNob != null && hitNob.IsOwner;

                    if (_isPlayerField == null)
                        _isPlayerField = typeof(StatsController).GetField("_isPlayer", BindingFlags.NonPublic | BindingFlags.Instance);

                    bool needsSuppress = !isOwnerVehicle && _isPlayerField != null;
                    if (needsSuppress) _isPlayerField.SetValue(stats, false);

                    stats.TakeEndurance(_damage, false, attackerObject);

                    if (needsSuppress) _isPlayerField.SetValue(stats, true);
                }
            }
        }
        #endregion

        #region VFX_RPCS
        [ObserversRpc]
        private void RpcSpawnExplosionVFX(Vector3 point, Vector3 normal)
        {
            if (_explosionPrefab != null)
            {
                var vfx = Instantiate(_explosionPrefab, point, Quaternion.LookRotation(normal));
                vfx.transform.localScale = Vector3.one * _explosionRadius;
            }
        }

        [ObserversRpc]
        private void RpcSpawnImpactVFX(Vector3 point, Vector3 normal)
        {
            if (_impactParticlesPrefab != null)
                Instantiate(_impactParticlesPrefab, point, Quaternion.LookRotation(normal));
        }
        #endregion
    }
}