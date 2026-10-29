using FishNet.Object;
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
    /// <summary>
    /// A networked shot (bullet, push shot or mine). Every peer flies it from the same launch state, one step
    /// per tick, drawn interpolated; only the server decides what it did, and its despawn removes it everywhere.
    /// No input to predict, so no Replicate/Reconcile.
    /// </summary>
    [RequireComponent(typeof(ICustomRigidbody), typeof(Collider))]
    public class NetworkedProjectile : NetworkBehaviour
    {
        #region REFERENCES_AND_STATE
        private ICustomRigidbody _customRigidbody;
        private Collider _ownCollider;

        private static FieldInfo _networkObjectEnablePredictionField;
        private static FieldInfo _networkObjectGraphicalObjectField;
        private static FieldInfo _networkObjectPredictionTypeField;
        private static FieldInfo _networkObjectEnableStateForwardingField;
        private static FieldInfo _tickSmootherInitSettingsField;

        [Header("Efectos")]
        [SerializeField] private GameObject _visuals;
        [SerializeField] private GameObject _impactParticlesPrefab;
        [SerializeField] private GameObject _explosionPrefab;
        private GameObject _pushExplosionVfx;

        private bool _isInitialized;
        private NetworkObject _shooter;  
        private float _damage;
        private LayerMask _collisionMask;
        private Collider[] _ignoredColliders;
        private NetworkConnection _attackerConnection;

        private float _pushForce;
        private float _explosionRadius;

        // Read from the prefab's ExplosionForce before it is destroyed; defaults are ExplosionForce's own.
        private ForceMode _impulseForceMode = ForceMode.Impulse;
        private bool _impulseLimitRbSpeed;
        private float _impulseTime = 1f;
        private bool _impulseStopMomentum;
        private float _impulseSpeedLimit = 1000f;
        private Vector3 _explosionCenterOffset;
        private float _bounceStrength;
        private int _maxBounces;
        private int _currentBounces;
        private Vector3 _customGravity;
        private bool _useCustomGravity;
        private float _timeToExplode;
        private bool _isMine;

        // One step per tick; the drawn position is interpolated from the previous tick's (see LateUpdate).
        private Vector3 _simulatedPosition;
        private Vector3 _previousTickPosition;
        private Vector3 _simulatedVelocity;
        private bool _hasLanded;       // mine resting on a surface
        private bool _hasEnded;        // impact reached on this peer; waiting for the server despawn
        private bool _serverEndPending; // server: damage/push/despawn to apply on the next tick
        private Vector3 _endPoint, _endNormal;
        private GameObject _endHitObject;
        private uint _spawnTick;
        private uint _despawnTick;
        private uint _mineExplodeTick;

        private float _sweepRadius = DEFAULT_SWEEP_RADIUS;
        private Vector3 _sweepLocalCenter;

        private bool _tickSmoothersStopped;
        private Vector3 _visualsLocalPosition;
        private Quaternion _visualsLocalRotation;

        private const float DEFAULT_LIFETIME_SECONDS = 5f;
        private const float MIN_FLIGHT_SPEED = 0.1f;
        // Lobbed shots (push, mine) have no range; this only catches one that never hits anything.
        private const float MAX_LOBBED_FLIGHT_SECONDS = 8f;
        private const float DEFAULT_SWEEP_RADIUS = 0.15f;
        private const float CONTACT_SKIN = 0.02f;
        private const int MAX_SWEEP_HITS = 16;
        private static readonly RaycastHit[] _sweepHits = new RaycastHit[MAX_SWEEP_HITS];
        private const float MIN_VELOCITY_FOR_RAYCAST = 0.01f;
        private const float NON_VEHICLE_PUSH_FORCE_MULTIPLIER = 0.5f;
        private const float MIN_DIRECTION_SQR_MAG = 0.001f;

        // One mask per layer, from the layer collision matrix (fixed at runtime).
        private const int LAYER_COUNT = 32;
        private static readonly int[] _collisionMaskByLayer = new int[LAYER_COUNT];
        private static readonly bool[] _collisionMaskByLayerCached = new bool[LAYER_COUNT];
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

        private static int DefaultLayer
        {
            get
            {
                if (_cachedDefaultLayer < 0) _cachedDefaultLayer = LayerMask.NameToLayer("Default");
                return _cachedDefaultLayer;
            }
        }

        /// <summary>The layers <paramref name="layer"/> collides with, from the collision matrix (hand-written masks missed Static).</summary>
        public static int GetCollisionMaskForLayer(int layer)
        {
            if (layer < 0 || layer >= LAYER_COUNT) layer = 0;
            if (!_collisionMaskByLayerCached[layer])
            {
                int mask = 0;
                for (int other = 0; other < LAYER_COUNT; other++)
                {
                    if (!Physics.GetIgnoreLayerCollision(layer, other))
                        mask |= 1 << other;
                }
                _collisionMaskByLayer[layer] = mask;
                _collisionMaskByLayerCached[layer] = true;
            }
            return _collisionMaskByLayer[layer];
        }
        #endregion

        #region FLIGHT_RULES
        internal enum SurfaceHitOutcome { Bounce, Rest, Explode }

        /// <summary>A non-character contact spends a bounce; the last one ends the flight (a mine rests, anything else explodes).</summary>
        internal static SurfaceHitOutcome ResolveSurfaceHit(ref int bouncesUsed, int maxBounces, float timeToExplode)
        {
            if (bouncesUsed < maxBounces) bouncesUsed++;
            if (bouncesUsed < maxBounces) return SurfaceHitOutcome.Bounce;
            return timeToExplode > 0f ? SurfaceHitOutcome.Rest : SurfaceHitOutcome.Explode;
        }

        /// <summary>Velocity after a bounce: stop the motion into the surface and add normal * strength, as PushBulletBehaviour does.</summary>
        internal static Vector3 BounceVelocity(Vector3 velocity, Vector3 surfaceNormal, float bounceStrength)
        {
            return Vector3.ProjectOnPlane(velocity, surfaceNormal) + surfaceNormal * bounceStrength;
        }

        /// <summary>Flight time: a straight bullet ends at its range; a lobbed one only when it hits.</summary>
        internal static float FlightLifetimeSeconds(Vector3 initialVelocity, float range, bool lobbed)
        {
            if (lobbed) return MAX_LOBBED_FLIGHT_SECONDS;
            float speed = initialVelocity.magnitude;
            return speed > MIN_FLIGHT_SPEED && range > 0f ? range / speed : DEFAULT_LIFETIME_SECONDS;
        }
        #endregion

        #region FISHNET_LIFECYCLE
        private void Awake()
        {
            _customRigidbody = GetComponent<ICustomRigidbody>();
            _ownCollider = GetComponent<Collider>();
            if (_ownCollider is SphereCollider sphere)
            {
                Vector3 scale = transform.lossyScale;
                _sweepRadius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                _sweepLocalCenter = Vector3.Scale(sphere.center, scale);
            }

            if (_customRigidbody is MultiplayerRigidbodyAdapter adapter)
            {
                adapter.InitializeAdapter();
            }

            // Immediate: Physics.Simulate can fire OnTriggerEnter before a deferred Destroy takes effect.
            var localEventRunner = GetComponent<FastAndFractured.Utilities.LocalEventRunner>();
            if (localEventRunner != null)
                DestroyImmediate(localEventRunner);

            var bulletBehaviour = GetComponent<BulletBehaviour>();
            if (bulletBehaviour != null)
            {
                var impactParticles = typeof(BulletBehaviour).GetField("particles", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(bulletBehaviour) as GameObject;
                if (impactParticles != null) impactParticles.SetActive(false);
                DestroyImmediate(bulletBehaviour);
            }

            // ApplyExplosionForce does ExplosionForce's job with an OverlapSphere.
            foreach (var ef in GetComponentsInChildren<ExplosionForce>(true))
            {
                ReadImpulseSettings(ef);
                if (_pushExplosionVfx == null)
                    _pushExplosionVfx = (typeof(ExplosionForce).GetField("_explosionVFX", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.GetValue(ef) as Transform)?.gameObject;
                ef.gameObject.SetActive(false);
                DestroyImmediate(ef);
            }

            NormalizeRuntimeNetworkSettings();
            if (_visuals != null)
            {
                _visualsLocalPosition = _visuals.transform.localPosition;
                _visualsLocalRotation = _visuals.transform.localRotation;
            }
            PrepareTickSmoothers();
        }

        private bool _subscribedToTimeManager;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!_subscribedToTimeManager)
            {
                base.TimeManager.OnTick += TimeManager_OnTick;
                _subscribedToTimeManager = true;
            }
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (base.TimeManager != null && _subscribedToTimeManager)
            {
                base.TimeManager.OnTick -= TimeManager_OnTick;
                _subscribedToTimeManager = false;
            }
        }

        // Older call sites; shots normally get their launch state from the spawn payload.
        [ObserversRpc(RunLocally = true)]
        public void RpcInitialize(NetworkShootParams data, NetworkObject ignorer, Vector3 spawnPosition, Quaternion spawnRotation)
        {
            InitializeProjectile(data, ignorer, spawnPosition, spawnRotation);
        }

        internal void InitializeProjectile(NetworkShootParams data, NetworkObject ignorer, Vector3 spawnPosition,
            Quaternion spawnRotation, uint ticksAlreadyFlown = 0)
        {
            if (_isInitialized) return;
            _isInitialized = true;
            _shooter = ignorer;
            _attackerConnection = ignorer != null ? ignorer.Owner : null;

            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            if (_customRigidbody != null)
            {
                // Before marking kinematic: velocities set on a kinematic body are dropped with a warning.
                _customRigidbody.linearVelocity = Vector3.zero;
                _customRigidbody.angularVelocity = Vector3.zero;
                _customRigidbody.position = spawnPosition;
                _customRigidbody.rotation = spawnRotation;
                _customRigidbody.isKinematic = true;
                _customRigidbody.useGravity = false;
            }

            gameObject.name = $"{gameObject.name}_Owner({(_attackerConnection != null ? _attackerConnection.ClientId : -1)})_Tick({base.TimeManager.Tick})";

            _simulatedPosition = spawnPosition;
            _previousTickPosition = spawnPosition;
            _simulatedVelocity = data.InitialVelocity;
            _damage = data.Damage;

            _collisionMask = GetCollisionMaskForLayer(gameObject.layer);
            _customGravity = data.CustomGravity;
            _useCustomGravity = data.CustomGravity.sqrMagnitude > 0f;

            if (ignorer != null)
            {
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
            _currentBounces = 0;
            _timeToExplode = data.TimeToExplode;
            _isMine = data.IsMine;
            _hasLanded = false;
            _hasEnded = false;
            _serverEndPending = false;

            _spawnTick = base.TimeManager.Tick;
            float lifetime = FlightLifetimeSeconds(data.InitialVelocity, data.Range, _useCustomGravity);
            _despawnTick = _spawnTick + base.TimeManager.TimeToTicks(lifetime);

            // A late shot catches up to where the shooter already sees it.
            float tickDelta = (float)base.TimeManager.TickDelta;
            for (uint i = 0; i < ticksAlreadyFlown && _isInitialized && !_hasEnded; i++)
            {
                _previousTickPosition = _simulatedPosition;
                SimulateStep(tickDelta);
            }
            _previousTickPosition = _simulatedPosition;
            ApplyDrawnPose(_simulatedPosition);
        }

        private void TimeManager_OnTick()
        {
            if (!_tickSmoothersStopped) StopTickSmoothers();
            if (_serverEndPending)
            {
                _serverEndPending = false;
                if (base.IsServerInitialized) ApplyServerEnd();
                return;
            }
            if (!_isInitialized || _hasEnded) return;
            _previousTickPosition = _simulatedPosition;
            SimulateStep((float)base.TimeManager.TickDelta);
        }
        #endregion

        #region SIMULATION
        private void SimulateStep(float delta)
        {
            if (_hasLanded)
            {
                // A resting mine: only the server decides when it goes off.
                if (base.IsServerInitialized && base.TimeManager.Tick >= _mineExplodeTick)
                    EndFlight(_simulatedPosition, Vector3.up, null);
                return;
            }

            if (_useCustomGravity)
                _simulatedVelocity += _customGravity * delta;

            if (!SweepAndResolveContact(delta))
            {
                _simulatedPosition += _simulatedVelocity * delta;
                if (base.IsServerInitialized && base.TimeManager.Tick >= _despawnTick)
                    EndFlight(_simulatedPosition, Vector3.up, null);
            }

            if (_customRigidbody != null)
                _customRigidbody.position = _simulatedPosition;
        }

        /// <summary>Returns true when this step ended at a contact (the position is already set).</summary>
        private bool SweepAndResolveContact(float delta)
        {
            float distance = _simulatedVelocity.magnitude * delta;
            if (distance < MIN_VELOCITY_FOR_RAYCAST) return false;

            Vector3 direction = _simulatedVelocity.normalized;
            Vector3 centerOffset = transform.rotation * _sweepLocalCenter;
            Vector3 origin = _simulatedPosition + centerOffset;
            // Ignore triggers: this sweep stands in for OnCollisionEnter, which never fires against one.
            int count = Physics.SphereCastNonAlloc(origin, _sweepRadius, direction, _sweepHits, distance, _collisionMask,
                QueryTriggerInteraction.Ignore);
            if (!TryGetFirstContact(count, out RaycastHit hit)) return false;

            Vector3 contactPosition = origin + direction * hit.distance - centerOffset;

            if (hit.collider.gameObject.layer == CharactersLayer)
            {
                _simulatedPosition = contactPosition;
                EndFlight(contactPosition, hit.normal, hit.collider.gameObject);
                return true;
            }

            switch (ResolveSurfaceHit(ref _currentBounces, _maxBounces, _timeToExplode))
            {
                case SurfaceHitOutcome.Bounce:
                    _simulatedVelocity = BounceVelocity(_simulatedVelocity, hit.normal, _bounceStrength);
                    _simulatedPosition = contactPosition + hit.normal * CONTACT_SKIN;
                    return true;

                case SurfaceHitOutcome.Rest:
                    // Every peer rests the mine where it landed; only the server owns its timer.
                    _simulatedPosition = contactPosition + hit.normal * CONTACT_SKIN;
                    _simulatedVelocity = Vector3.zero;
                    _useCustomGravity = false;
                    _hasLanded = true;
                    if (base.IsServerInitialized)
                        _mineExplodeTick = base.TimeManager.Tick + base.TimeManager.TimeToTicks(_timeToExplode);
                    return true;

                default:
                    _simulatedPosition = contactPosition;
                    EndFlight(contactPosition, hit.normal, hit.collider.gameObject);
                    return true;
            }
        }

        /// <summary>The nearest hit that is not the shooter, nor something the sphere already overlapped when the step began.</summary>
        private bool TryGetFirstContact(int count, out RaycastHit first)
        {
            first = default;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = _sweepHits[i];
                if (candidate.distance <= 0f && candidate.point == Vector3.zero) continue;
                if (IsIgnored(candidate.collider)) continue;
                if (found && candidate.distance >= first.distance) continue;
                first = candidate;
                found = true;
            }
            return found;
        }

        private bool IsIgnored(Collider other)
        {
            if (_ownCollider != null && (other == _ownCollider || other.transform == transform))
                return true;

            // All colliders in the shooter's hierarchy (even ones GetComponentsInChildren missed).
            if (_shooter != null && other.transform.root == _shooter.transform.root)
                return true;

            if (_ignoredColliders != null)
            {
                foreach (var ignored in _ignoredColliders)
                {
                    if (ignored != null && (other == ignored || other.transform.IsChildOf(ignored.transform)))
                        return true;
                }
            }
            return false;
        }

        /// <summary>Clients stop drawing the shot; the server applies its effect and despawns it.</summary>
        private void EndFlight(Vector3 point, Vector3 normal, GameObject hitObject)
        {
            if (_hasEnded) return;
            _hasEnded = true;
            _simulatedVelocity = Vector3.zero;
            _simulatedPosition = point;
            _previousTickPosition = point;
            ApplyDrawnPose(point);
            if (_visuals != null) _visuals.SetActive(false);
            if (_ownCollider != null) _ownCollider.enabled = false;

            // Next tick: the end can happen during spawn (catching up), where despawning is not safe.
            _endPoint = point;
            _endNormal = normal;
            _endHitObject = hitObject;
            _serverEndPending = true;
        }

        private void ApplyServerEnd()
        {
            MpCanary.Mark($"Projectile end {(_isMine ? "mine" : _explosionRadius > 0f ? "push" : "normal")} " +
                          $"after {base.TimeManager.Tick - _spawnTick} ticks, hit='{(_endHitObject != null ? _endHitObject.name : "none")}' " +
                          $"shooter='{(_shooter != null ? _shooter.name : "none")}'");

            if (_explosionRadius > 0f)
            {
                ApplyExplosionForce(_endPoint);
                RpcSpawnExplosionVFX(_endPoint, _endNormal);
            }
            else
            {
                if (_endHitObject != null) ApplyPointDamage(_endHitObject);
                RpcSpawnImpactVFX(_endPoint, _endNormal);
            }

            InstanceFinder.ServerManager.Despawn(base.NetworkObject, DespawnType.Destroy);
        }
        #endregion

        #region RENDERING
        /// <summary>Draws the shot between its last two simulated positions; the simulation stays on the tick grid.</summary>
        private void LateUpdate()
        {
            if (!_isInitialized || _hasEnded || base.TimeManager == null) return;
            float alpha = Mathf.Clamp01((float)base.TimeManager.GetTickPercentAsDouble());
            ApplyDrawnPose(Vector3.LerpUnclamped(_previousTickPosition, _simulatedPosition, alpha));
        }

        private void ApplyDrawnPose(Vector3 position)
        {
            if (transform.position != position)
                transform.position = position;
        }

        /// <summary>Turns FishNet prediction off on every projectile prefab: every peer runs the same simulation.</summary>
        private void NormalizeRuntimeNetworkSettings()
        {
            NetworkObject nob = GetComponent<NetworkObject>();
            if (nob == null) return;

            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            _networkObjectEnablePredictionField ??= typeof(NetworkObject).GetField("_enablePrediction", flags);
            _networkObjectPredictionTypeField ??= typeof(NetworkObject).GetField("_predictionType", flags);
            _networkObjectEnableStateForwardingField ??= typeof(NetworkObject).GetField("_enableStateForwarding", flags);
            _networkObjectGraphicalObjectField ??= typeof(NetworkObject).GetField("_graphicalObject", flags);

            _networkObjectEnablePredictionField?.SetValue(nob, false);
            _networkObjectPredictionTypeField?.SetValue(nob,
                System.Enum.ToObject(_networkObjectPredictionTypeField.FieldType, 0)); // Other
            _networkObjectEnableStateForwardingField?.SetValue(nob, false);
            _networkObjectGraphicalObjectField?.SetValue(nob, null);
        }

        /// <summary>Gives the prefab's NetworkTickSmoother a valid target so it starts quietly; StopTickSmoothers stops it.</summary>
        private void PrepareTickSmoothers()
        {
            _tickSmootherInitSettingsField ??= typeof(NetworkTickSmoother).GetField("_initializationSettings",
                BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var smoother in GetComponentsInChildren<NetworkTickSmoother>(true))
            {
                if (smoother == null) continue;
                smoother.enabled = false;
                if (_tickSmootherInitSettingsField == null || _visuals == null) continue;
                if (!(_tickSmootherInitSettingsField.GetValue(smoother) is InitializationSettings settings)) continue;
                if (settings.TargetTransform != null && settings.TargetTransform != smoother.transform) continue;
                settings.TargetTransform = _visuals.transform;
                _tickSmootherInitSettingsField.SetValue(smoother, settings);
            }
        }

        /// <summary>Stops the smoother's controller (it subscribes to the TimeManager itself). Not removed: that shifts RPC indices.</summary>
        private void StopTickSmoothers()
        {
            bool allStopped = true;
            foreach (var smoother in GetComponentsInChildren<NetworkTickSmoother>(true))
            {
                if (smoother == null) continue;
                smoother.enabled = false;
                if (smoother.SmootherController != null) smoother.SmootherController.StopSmoother();
                else allStopped = false;
            }
            if (!allStopped && base.TimeManager != null && base.TimeManager.Tick - _spawnTick < 5) return;

            _tickSmoothersStopped = true;
            if (_visuals != null)
                _visuals.transform.SetLocalPositionAndRotation(_visualsLocalPosition, _visualsLocalRotation);
        }
        #endregion

        #region EXPLOSION_AND_DAMAGE
        private void ReadImpulseSettings(ExplosionForce source)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(ExplosionForce);
            if (type.GetField("forceMode", flags)?.GetValue(source) is ForceMode mode) _impulseForceMode = mode;
            if (type.GetField("limitRbSpeed", flags)?.GetValue(source) is bool limit) _impulseLimitRbSpeed = limit;
            if (type.GetField("explosionImpulseTime", flags)?.GetValue(source) is float time) _impulseTime = time;
            if (type.GetField("stopSpeedOnHit", flags)?.GetValue(source) is bool stop) _impulseStopMomentum = stop;
            if (type.GetField("limitRbSpeedLimit", flags)?.GetValue(source) is float speedLimit) _impulseSpeedLimit = speedLimit;
        }

        /// <summary>Singleplayer's push direction: towards the car's upper corner, clamped to the blast box.</summary>
        public static Vector3 PushDirection(Vector3 impactPoint, Vector3 blastCenter, float blastRadius, Bounds carBounds)
        {
            var blast = new Bounds(blastCenter, Vector3.one * (blastRadius * 2f));
            Vector3 direction = blast.ClosestPoint(carBounds.max) - impactPoint;
            if (direction.sqrMagnitude < MIN_DIRECTION_SQR_MAG) direction = carBounds.center - impactPoint;
            if (direction.sqrMagnitude < MIN_DIRECTION_SQR_MAG) return Vector3.up;
            return direction.normalized;
        }

        /// <summary>Server: pushes every vehicle in the blast, as ExplosionForce.OnTriggerEnter does.</summary>
        private void ApplyExplosionForce(Vector3 center)
        {
            Vector3 explosionCenter = center + _explosionCenterOffset;
            LayerMask explosionMask = (1 << CharactersLayer) | (1 << DefaultLayer);

            Collider[] hits = Physics.OverlapSphere(explosionCenter, _explosionRadius, explosionMask);
            var pushedSet = new System.Collections.Generic.HashSet<PhysicsBehaviour>();

            foreach (var hit in hits)
            {
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

                    // The car body's collider, not whichever child collider the overlap returned first.
                    Collider body = physicsBehaviour.GetComponent<Collider>();
                    Bounds carBounds = body != null ? body.bounds : hit.bounds;
                    var blast = new Bounds(explosionCenter, Vector3.one * (_explosionRadius * 2f));
                    Vector3 closestPoint = blast.ClosestPoint(carBounds.max);
                    Vector3 direction = PushDirection(center, explosionCenter, _explosionRadius, carBounds);

                    float forceToApply = physicsBehaviour.CalculateForceToApplyToOtherCar(
                        enduranceFactor, weight, enduranceImportance, _pushForce);

                    if (!physicsBehaviour.HasBeenPushed)
                    {
                        // ApplyImpulse, as singleplayer: it lifts the speed cap, which would otherwise cut the sideways push.
                        physicsBehaviour.ApplyImpulse(direction, closestPoint, forceToApply, _impulseForceMode,
                            _impulseLimitRbSpeed, _impulseTime, _impulseStopMomentum, _impulseSpeedLimit);
                        MpCanary.Mark($"Blast pushed '{physicsBehaviour.name}' force={forceToApply:F0} base={_pushForce:F0} mode={_impulseForceMode} " +
                                      $"mass={physicsBehaviour.Rb.mass:F0} dir={direction:F2}");
                        if (carImpact != null) carImpact.OnHasBeenPushed(physicsBehaviour);
                        // Pushed into a pit or the storm: the kill goes to the shooter.
                        var pushedStats = physicsBehaviour.GetComponentInParent<NetworkedStatsController>();
                        if (pushedStats == null) pushedStats = physicsBehaviour.GetComponentInChildren<NetworkedStatsController>();
                        bool ownBlast = _shooter != null && physicsBehaviour.transform.root == _shooter.transform.root;
                        if (pushedStats != null && base.IsServerInitialized && !ownBlast) pushedStats.ServerNoteAttacker(_shooter);
                    }
                }
                else if (physicsBehaviour == null)
                {
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

        private void ApplyPointDamage(GameObject hitObject)
        {
            var networkedStats = hitObject.GetComponentInParent<NetworkedStatsController>();
            if (networkedStats != null && _attackerConnection != null && _attackerConnection.IsValid)
            {
                networkedStats.DealDamageServerRpc(_damage, _attackerConnection);
            }
            else if (networkedStats != null && _shooter != null && base.IsServerInitialized)
            {
                // An AI's bullet: no connection, but the car is known - networked damage with credit.
                networkedStats.DealDamageFromCarServer(_damage, _shooter);
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
            NetworkedScreenShake.PlayAtPosition(ScreenShakeProfileType.ImpactCollision, point);

            if (_pushExplosionVfx != null)
            {
                var vfx = Instantiate(_pushExplosionVfx, point, Quaternion.identity);
                vfx.SetActive(true);
                vfx.transform.localScale = Vector3.one * _explosionRadius;
                Destroy(vfx, VFX_AUTO_DESTROY_TIME);
            }
            else if (_explosionPrefab != null)
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
                vfx.SetActive(true);    
                Destroy(vfx, VFX_AUTO_DESTROY_TIME);
            }
        }
        #endregion
    }
}
