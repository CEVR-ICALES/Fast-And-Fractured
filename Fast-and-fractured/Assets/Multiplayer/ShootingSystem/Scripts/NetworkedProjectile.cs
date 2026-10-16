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
    /// A networked shot: normal bullet, push shot or mine.
    ///
    /// Every peer flies the shot itself from the same launch state (spawn payload), one step per
    /// network tick, and draws it interpolated between the last two ticks. The server alone decides
    /// what the shot did - damage, push, when a mine goes off - and its despawn removes the shot
    /// everywhere. A shot has no input to predict or correct, so it deliberately does not use
    /// Replicate/Reconcile.
    ///
    /// Measured before this design (ShootingProbe, 2026-09-30, all four characters):
    ///   * Carme's push shot and mine teleported 47 km / 12 km in one frame after their first bounce:
    ///     the bounce multiplied the reflected velocity by PushShootBounceForce (50), a value the
    ///     singleplayer PushBulletBehaviour ADDS along the surface normal.
    ///   * Push shots and mines exploded in mid-air: they were despawned after Range / speed, a
    ///     straight-bullet rule. Singleplayer push shots have no range; they fly until they hit.
    ///     Mines therefore never rested their MineExplosionTime.
    ///   * Owners of prediction-enabled prefabs (Carme, Maria, Josefino's bat) were reconciled to the
    ///     server's older state and could not re-simulate the gap, so eggs stood still and vanished.
    ///   * Every bullet was drawn in 50 Hz steps: 1.7-1.9 m off a smooth path at 120 m/s.
    /// </summary>
    [RequireComponent(typeof(ICustomRigidbody), typeof(Collider))]
    public class NetworkedProjectile : NetworkBehaviour
    {
        #region REFERENCES_AND_STATE
        private ICustomRigidbody _customRigidbody;
        private Collider _ownCollider;

        // FishNet-internal reflection (not centralized in SingleplayerReflectionCache because
        // these are FishNet types, not singleplayer types).
        private static FieldInfo _networkObjectEnablePredictionField;
        private static FieldInfo _networkObjectGraphicalObjectField;
        private static FieldInfo _networkObjectPredictionTypeField;
        private static FieldInfo _networkObjectEnableStateForwardingField;
        private static FieldInfo _tickSmootherInitSettingsField;

        [Header("Efectos")]
        [SerializeField] private GameObject _visuals;
        [SerializeField] private GameObject _impactParticlesPrefab;
        [SerializeField] private GameObject _explosionPrefab;

        private bool _isInitialized;
        private NetworkObject _shooter; // Reference to the shooter for robust collision ignoring
        private float _damage;
        private LayerMask _collisionMask;
        private Collider[] _ignoredColliders;
        private NetworkConnection _attackerConnection;

        private float _pushForce;
        private float _explosionRadius;

        // How the singleplayer ExplosionForce on this prefab hands its push to the car
        // (PhysicsBehaviour.ApplyImpulse). Read before that component is destroyed; the defaults
        // are ExplosionForce's own.
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

        // Simulation: one step per network tick. _previousTickPosition is where the shot was one
        // tick ago; the drawn position is interpolated between the two (see LateUpdate).
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

        // The prefab's NetworkTickSmoother starts itself in its own OnStartClient (see StopTickSmoothers).
        private bool _tickSmoothersStopped;
        private Vector3 _visualsLocalPosition;
        private Quaternion _visualsLocalRotation;

        // Tunables
        private const float DEFAULT_LIFETIME_SECONDS = 5f;
        // A lobbed shot (push or mine) has no range: like the singleplayer PushBulletBehaviour it flies
        // until it hits something. This is only the safety net for one that never does.
        private const float MAX_LOBBED_FLIGHT_SECONDS = 8f;
        private const float SPHERE_CAST_RADIUS = 0.15f;
        private const float BOUNCE_OFFSET_FROM_SURFACE = 0.15f;
        private const float MIN_VELOCITY_FOR_RAYCAST = 0.01f;
        private const float NON_VEHICLE_PUSH_FORCE_MULTIPLIER = 0.5f;
        private const float MIN_DIRECTION_SQR_MAG = 0.001f;

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

        private static int DefaultLayer
        {
            get
            {
                if (_cachedDefaultLayer < 0) _cachedDefaultLayer = LayerMask.NameToLayer("Default");
                return _cachedDefaultLayer;
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
        // Public for the regression test: this is the invariant that "Obstacles" broke.
        public static int GetCollisionMaskForLayer(int layer)
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
        #endregion

        #region FLIGHT_RULES
        internal enum SurfaceHitOutcome { Bounce, Rest, Explode }

        /// <summary>
        /// What a contact with anything that is not a character does to the shot, mirroring
        /// PushBulletBehaviour.BouncingHandle: each contact spends a bounce while any are left, and
        /// the contact that spends the last one (or the first one, when none are configured) ends
        /// the flight - a mine settles and waits for its timer, anything else explodes.
        /// </summary>
        internal static SurfaceHitOutcome ResolveSurfaceHit(ref int bouncesUsed, int maxBounces, float timeToExplode)
        {
            if (bouncesUsed < maxBounces) bouncesUsed++;
            if (bouncesUsed < maxBounces) return SurfaceHitOutcome.Bounce;
            return timeToExplode > 0f ? SurfaceHitOutcome.Rest : SurfaceHitOutcome.Explode;
        }

        /// <summary>
        /// Velocity after a bounce, mirroring singleplayer: the contact stops the motion into the
        /// surface and PushBulletBehaviour then ADDS normal * strength (its decay factor uses integer
        /// division, so it stays 1 on every bounce that does not end the flight). Using the strength
        /// as a multiplier instead - the old networked rule - launched shots at ~1500 m/s.
        /// </summary>
        internal static Vector3 BounceVelocity(Vector3 velocity, Vector3 surfaceNormal, float bounceStrength)
        {
            return Vector3.ProjectOnPlane(velocity, surfaceNormal) + surfaceNormal * bounceStrength;
        }

        /// <summary>
        /// How long the shot may fly. A straight bullet ends at its range, like NormalBulletBehaviour;
        /// a lobbed one (custom gravity: push shot, mine) has no range and ends when it hits.
        /// </summary>
        internal static float FlightLifetimeSeconds(Vector3 initialVelocity, float range, bool lobbed)
        {
            if (lobbed) return MAX_LOBBED_FLIGHT_SECONDS;
            float speed = initialVelocity.magnitude;
            return speed > 0.1f && range > 0f ? range / speed : DEFAULT_LIFETIME_SECONDS;
        }
        #endregion

        #region FISHNET_LIFECYCLE
        private void Awake()
        {
            _customRigidbody = GetComponent<ICustomRigidbody>();
            _ownCollider = GetComponent<Collider>();

            if (_customRigidbody is MultiplayerRigidbodyAdapter adapter)
            {
                adapter.InitializeAdapter();
            }

            // DestroyImmediate LocalEventRunner: NetworkedProjectile handles all physics.
            // Must be Immediate because Physics.Simulate() can fire OnTriggerEnter within the SAME
            // frame before a deferred Destroy() takes effect.
            var localEventRunner = GetComponent<FastAndFractured.Utilities.LocalEventRunner>();
            if (localEventRunner != null)
                DestroyImmediate(localEventRunner);

            // Also DestroyImmediate the original BulletBehaviour to prevent NullRef
            // from uninitialized _ignoreCollider during physics.
            var bulletBehaviour = GetComponent<BulletBehaviour>();
            if (bulletBehaviour != null)
                DestroyImmediate(bulletBehaviour);

            // Singleplayer ExplosionForce components (on the ExplosionHitbox child) would fire
            // OnTriggerEnter with a null _explosionCollider; ApplyExplosionForce does their job with an
            // OverlapSphere instead.
            foreach (var ef in GetComponentsInChildren<ExplosionForce>(true))
            {
                ReadImpulseSettings(ef);
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

        // Kept for older call sites; the spawn payload (NetworkedPredictedProjectileSpawn) is the
        // normal way a shot gets its launch state, atomically with the spawn itself.
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
            _previousTickPosition = spawnPosition;
            _simulatedVelocity = data.InitialVelocity;
            _damage = data.Damage;

            _collisionMask = GetCollisionMaskForLayer(gameObject.layer);
            _customGravity = data.CustomGravity;
            _useCustomGravity = data.CustomGravity.sqrMagnitude > 0f;

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
            _currentBounces = 0;
            _timeToExplode = data.TimeToExplode;
            _isMine = data.IsMine;
            _hasLanded = false;
            _hasEnded = false;
            _serverEndPending = false;

            _spawnTick = base.TimeManager.Tick;
            float lifetime = FlightLifetimeSeconds(data.InitialVelocity, data.Range, _useCustomGravity);
            _despawnTick = _spawnTick + base.TimeManager.TimeToTicks(lifetime);

            // A shot that reached this peer late (the server receiving a predicted shot, or an
            // observer) catches up to where the shooter already sees it, so all peers fly it on the
            // same timeline instead of this one running behind.
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

            // QueryTriggerInteraction.Ignore because this sweep stands in for OnCollisionEnter, which
            // never fires against a trigger. Without it the projectile stops dead inside invisible
            // volumes (interactable pickups, safe zones, the shield bubble).
            if (!Physics.SphereCast(_simulatedPosition, SPHERE_CAST_RADIUS, _simulatedVelocity.normalized, out RaycastHit hit,
                    distance, _collisionMask, QueryTriggerInteraction.Ignore))
                return false;

            if (IsIgnored(hit.collider)) return false;

            if (hit.collider.gameObject.layer == CharactersLayer)
            {
                // Hit a character -> explode / damage immediately (mirrors singleplayer).
                _simulatedPosition = hit.point;
                EndFlight(hit.point, hit.normal, hit.collider.gameObject);
                return true;
            }

            switch (ResolveSurfaceHit(ref _currentBounces, _maxBounces, _timeToExplode))
            {
                case SurfaceHitOutcome.Bounce:
                    _simulatedVelocity = BounceVelocity(_simulatedVelocity, hit.normal, _bounceStrength);
                    _simulatedPosition = hit.point + hit.normal * BOUNCE_OFFSET_FROM_SURFACE;
                    return true;

                case SurfaceHitOutcome.Rest:
                    // Mine: rest on the surface it hit, wait _timeToExplode, then explode. Every peer
                    // settles it so it stays visible where it landed; only the server owns the timer.
                    _simulatedPosition = hit.point + hit.normal * BOUNCE_OFFSET_FROM_SURFACE;
                    _simulatedVelocity = Vector3.zero;
                    _useCustomGravity = false;
                    _hasLanded = true;
                    if (base.IsServerInitialized)
                        _mineExplodeTick = base.TimeManager.Tick + base.TimeManager.TimeToTicks(_timeToExplode);
                    return true;

                default:
                    _simulatedPosition = hit.point;
                    EndFlight(hit.point, hit.normal, hit.collider.gameObject);
                    return true;
            }
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

        /// <summary>
        /// The shot reached its end on this peer. Clients stop drawing it (their prediction of the
        /// impact); the server applies the damage or push and despawns it for everyone.
        /// </summary>
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

            // The server's part runs on the next tick, never inside the caller: an end can happen
            // while the shot is still being initialised (catching up in OnStartNetwork), where
            // despawning the object that is being spawned is not safe.
            _endPoint = point;
            _endNormal = normal;
            _endHitObject = hitObject;
            _serverEndPending = true;
        }

        private void ApplyServerEnd()
        {
            MpCanary.Mark($"Projectile end {(_isMine ? "mine" : _explosionRadius > 0f ? "push" : "normal")} " +
                          $"after {base.TimeManager.Tick - _spawnTick} ticks, hit='{(_endHitObject != null ? _endHitObject.name : "none")}'");

            if (_explosionRadius > 0f)
            {
                // Push bullet / mine: area-of-effect push force
                ApplyExplosionForce(_endPoint);
                RpcSpawnExplosionVFX(_endPoint, _endNormal);
            }
            else
            {
                // Normal bullet: point damage to single target
                if (_endHitObject != null) ApplyPointDamage(_endHitObject);
                RpcSpawnImpactVFX(_endPoint, _endNormal);
            }

            InstanceFinder.ServerManager.Despawn(base.NetworkObject, DespawnType.Destroy);
        }
        #endregion

        #region RENDERING
        /// <summary>
        /// Draws the shot between its last two simulated positions, by how far the network clock is
        /// into the current tick. The simulation (and the collider) stay on the tick grid; only the
        /// drawn transform is smoothed, one tick (20 ms) behind.
        /// </summary>
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

        /// <summary>
        /// Every peer runs the same simulation, so FishNet's prediction machinery must stay out of the
        /// way on all projectile prefabs, whatever each one ships with: several had prediction enabled
        /// (one as a Rigidbody type), which registered them for reconciliation and graphical smoothing
        /// the others did not get. Applied in Awake, before network initialisation, on every peer.
        /// </summary>
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

        /// <summary>
        /// The prefabs carry a NetworkTickSmoother on the root. It starts itself in its own
        /// OnStartClient - also when disabled - and, with no target configured, logs "Target
        /// transform cannot be the same as graphical transform" for every shot. Give it a valid
        /// target so it starts quietly; StopTickSmoothers then stops it on the first tick.
        /// </summary>
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

        /// <summary>
        /// Stops the smoother once its controller exists. Its controller subscribes to the
        /// TimeManager by itself, so disabling the component is not enough (same finding as on the
        /// cars); left running it would move the shot as a second writer next to LateUpdate. The
        /// component stays on the object - removing a NetworkBehaviour at runtime shifts FishNet's RPC
        /// link indices.
        /// </summary>
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

        /// <summary>
        /// Singleplayer's push direction (ExplosionForce.OnTriggerEnter): from the explosion
        /// towards the car's upper corner, clamped to the blast's box. The old networked version
        /// aimed at the closest point of the car to the blast centre, which on a direct hit is the
        /// centre itself - a zero vector, replaced by straight up: the car shot vertically.
        /// </summary>
        public static Vector3 PushDirection(Vector3 impactPoint, Vector3 blastCenter, float blastRadius, Bounds carBounds)
        {
            var blast = new Bounds(blastCenter, Vector3.one * (blastRadius * 2f));
            Vector3 direction = blast.ClosestPoint(carBounds.max) - impactPoint;
            if (direction.sqrMagnitude < MIN_DIRECTION_SQR_MAG) direction = carBounds.center - impactPoint;
            if (direction.sqrMagnitude < MIN_DIRECTION_SQR_MAG) return Vector3.up;
            return direction.normalized;
        }

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

                    // The car body's collider, like singleplayer's trigger sees it; not whichever
                    // child collider (a wheel) the overlap happened to return first.
                    Collider body = physicsBehaviour.GetComponent<Collider>();
                    Bounds carBounds = body != null ? body.bounds : hit.bounds;
                    var blast = new Bounds(explosionCenter, Vector3.one * (_explosionRadius * 2f));
                    Vector3 closestPoint = blast.ClosestPoint(carBounds.max);
                    Vector3 direction = PushDirection(center, explosionCenter, _explosionRadius, carBounds);

                    float forceToApply = physicsBehaviour.CalculateForceToApplyToOtherCar(
                        enduranceFactor, weight, enduranceImportance, _pushForce);

                    if (!physicsBehaviour.HasBeenPushed)
                    {
                        // ApplyImpulse, as singleplayer: it also lifts the car's speed cap and marks
                        // it impulsed for a moment (both replicated in the reconcile), otherwise
                        // the cap cut the sideways part of the push and left mostly the vertical.
                        physicsBehaviour.ApplyImpulse(direction, closestPoint, forceToApply, _impulseForceMode,
                            _impulseLimitRbSpeed, _impulseTime, _impulseStopMomentum, _impulseSpeedLimit);
                        if (carImpact != null) carImpact.OnHasBeenPushed(physicsBehaviour);
                        // Pushed into a pit or the storm: the kill goes to the shooter.
                        var pushedStats = physicsBehaviour.GetComponentInParent<NetworkedStatsController>();
                        if (pushedStats == null) pushedStats = physicsBehaviour.GetComponentInChildren<NetworkedStatsController>();
                        if (pushedStats != null && base.IsServerInitialized) pushedStats.ServerNoteAttacker(_shooter);
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
            // Screen shake for everyone near the blast. The singleplayer ExplosionForce (which is
            // what normally triggers the Explosion profile) is stripped from networked projectiles,
            // so without this no peer — not even the shooter — feels an explosion.
            NetworkedScreenShake.PlayAtPosition(ScreenShakeProfileType.ImpactCollision, point);

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
    }
}
