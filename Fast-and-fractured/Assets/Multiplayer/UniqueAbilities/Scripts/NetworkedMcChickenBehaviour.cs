using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Object.Prediction;
using FishNet.Connection;
using UnityEngine;
using DG.Tweening;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Networked wrapper for McChicken (Carme's unique ability creature).
    /// Server-authoritative with state sync to all clients.
    /// 
    /// The McChicken is a physics-based AI creature that:
    /// - Launches to a target position
    /// - Moves in a direction, bouncing off walls
    /// - Climbs surfaces
    /// - Pushes/damages players on collision
    /// 
    /// Since it's AI-controlled (not player input), we use server authority with state sync
    /// rather than client prediction.
    ///
    /// Every other peer - the owner who predicted the spawn included - plays the same launch jump
    /// locally (a deterministic tween to the landing point) and from then on only follows the
    /// server's synced pose. Its own AI movement, collision handling and colliders stay off there:
    /// McChickenPhysicsHandler damages and pushes cars in OnCollisionEnter, which Unity delivers even
    /// to a disabled component, so a client-side chicken used to push cars on that client alone. The
    /// owner also used to keep simulating its own chicken for the whole ability and never saw where
    /// the server's one actually went.
    /// </summary>
    [RequireComponent(typeof(McChicken))]
    public class NetworkedMcChickenBehaviour : NetworkBehaviour
    {
        private McChicken _mcChicken;
        private McChickenMovement _movement;
        private McChickenPhysicsHandler _physics;
        private Rigidbody _rb;

        #region SYNCED_STATE
        /// <summary>
        /// Synced position for smooth interpolation on clients.
        /// </summary>
        public readonly SyncVar<Vector3> SyncedPosition = new SyncVar<Vector3>();

        /// <summary>
        /// Synced rotation for smooth interpolation on clients.
        /// </summary>
        public readonly SyncVar<Quaternion> SyncedRotation = new SyncVar<Quaternion>();

        /// <summary>
        /// Synced velocity for physics extrapolation.
        /// </summary>
        public readonly SyncVar<Vector3> SyncedVelocity = new SyncVar<Vector3>();

        /// <summary>
        /// Current movement direction.
        /// </summary>
        public readonly SyncVar<Vector3> SyncedMoveDirection = new SyncVar<Vector3>();

        /// <summary>
        /// Whether the chicken is currently climbing.
        /// </summary>
        public readonly SyncVar<bool> SyncedIsClimbing = new SyncVar<bool>();

        /// <summary>
        /// Whether the chicken has landed after initial launch.
        /// </summary>
        public readonly SyncVar<bool> SyncedHasLanded = new SyncVar<bool>();
        #endregion

        #region SERVER_STATE
        private float _syncTimer = 0f;
        private const float SYNC_INTERVAL = 0.05f; // 20Hz sync rate

        // Interpolation on client
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private Vector3 _lastSyncedPosition;
        private float _interpolationTime;
        private const float INTERPOLATION_DURATION = 0.1f;

        // Non-server peers: the launch tween has been started here (predicted spawn or RPC).
        private bool _launched;
        #endregion

        private void Awake()
        {
            _mcChicken = GetComponent<McChicken>();
            _movement = GetComponent<McChickenMovement>();
            _physics = GetComponent<McChickenPhysicsHandler>();
            _rb = GetComponent<Rigidbody>();
            _mainCollider = GetComponent<Collider>();
        }

        private Collider _mainCollider;

        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Initialize synced state
            SyncedPosition.Value = transform.position;
            SyncedRotation.Value = transform.rotation;
            SyncedVelocity.Value = Vector3.zero;
            SyncedHasLanded.Value = false;
            SyncedIsClimbing.Value = false;

            if (_serverLaunchQueued)
            {
                _serverLaunchQueued = false;
                ServerInitializeChicken(_queuedLandPoint, _queuedDirection);
            }
        }

        private bool _serverLaunchQueued;
        private Vector3 _queuedLandPoint, _queuedDirection;

        /// <summary>
        /// A client-predicted chicken is validated before it is spawned on the server; its launch
        /// waits for OnStartServer.
        /// </summary>
        internal void QueueServerLaunch(Vector3 landPoint, Vector3 direction)
        {
            _serverLaunchQueued = true;
            _queuedLandPoint = landPoint;
            _queuedDirection = direction;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsServerInitialized)
                BecomeFollower();

            // Subscribe to state changes
            SyncedPosition.OnChange += OnPositionChanged;
            SyncedRotation.OnChange += OnRotationChanged;
            SyncedIsClimbing.OnChange += OnClimbingChanged;
            SyncedHasLanded.OnChange += OnLandedChanged;
        }

        /// <summary>
        /// Visual-only chicken: no physics, no AI, no contacts. The server's chicken is the one that
        /// moves, climbs and hits cars.
        /// </summary>
        private void BecomeFollower()
        {
            if (_rb != null) _rb.isKinematic = true;
            if (_movement != null) _movement.enabled = false;
            if (_physics != null) _physics.enabled = false;
            foreach (var collider in GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            _targetPosition = transform.position;
            _targetRotation = transform.rotation;
            _lastSyncedPosition = transform.position;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            
            SyncedPosition.OnChange -= OnPositionChanged;
            SyncedRotation.OnChange -= OnRotationChanged;
            SyncedIsClimbing.OnChange -= OnClimbingChanged;
            SyncedHasLanded.OnChange -= OnLandedChanged;
        }

        private void Update()
        {
            if (IsServerInitialized)
            {
                ServerUpdate();
                return;
            }
            if (!IsClientInitialized) return;

            // The launch jump (and the landing scale-up after it) are tweens on this transform;
            // they own it until they finish, then the synced pose takes over.
            if (_launched && DOTween.IsTweening(transform)) return;
            ClientUpdate();
        }

        #region SERVER_METHODS
        private void ServerUpdate()
        {
            _syncTimer += Time.deltaTime;
            if (_syncTimer >= SYNC_INTERVAL)
            {
                _syncTimer = 0f;
                SyncStateToClients();
            }
        }

        private void SyncStateToClients()
        {
            SyncedPosition.Value = transform.position;
            SyncedRotation.Value = transform.rotation;
            
            if (_rb != null && !_rb.isKinematic)
            {
                SyncedVelocity.Value = _rb.linearVelocity;
            }
            
            if (_movement != null)
            {
                SyncedMoveDirection.Value = _movement.MoveDirection;
                SyncedIsClimbing.Value = _movement.IsClimbing;
            }
        }

        /// <summary>
        /// Called by server to initialize the chicken launch.
        /// </summary>
        [Server]
        public void ServerInitializeChicken(Vector3 targetPosition, Vector3 direction)
        {
            _mcChicken.InitializeChicken(targetPosition, direction);
            SyncedMoveDirection.Value = direction.normalized;
            
            // Notify all clients to play the launch animation/tween
            RpcInitializeChicken(targetPosition, direction);
        }

        internal void InitializePredictedClient(Vector3 targetPosition, Vector3 direction)
        {
            if (IsServerInitialized || _launched) return;
            _launched = true;
            _mcChicken.InitializeChicken(targetPosition, direction);
        }

        /// <summary>
        /// Called when the chicken lands (after DOJump completes).
        /// </summary>
        [Server]
        public void ServerNotifyLanded()
        {
            SyncedHasLanded.Value = true;
            SyncStateToClients();
        }

        /// <summary>
        /// Server handles collision damage/push.
        /// </summary>
        [Server]
        public void ServerHandleCharacterCollision(NetworkObject victim, Vector3 pushDirection, float pushForce)
        {
            if (victim == null) return;

            var stats = victim.GetComponentInChildren<NetworkedStatsController>();
            if (stats != null)
            {
                // Apply endurance damage through networked stats
                stats.DealDamageServerRpc(1f, null); // McChicken environmental damage
            }

            var vehicle = victim.GetComponentInChildren<NetworkedVehicle>();
            if (vehicle != null)
            {
                // Apply push force through networked vehicle
                vehicle.ApplyExternalForce(pushDirection * pushForce);
            }
        }
        #endregion

        #region CLIENT_METHODS
        private bool _followingSyncedPose;

        private void ClientUpdate()
        {
            // McChicken.Land() makes the body dynamic again and switches its collider back on when
            // the jump ends. A live collider here hit the owner's own car, and the singleplayer
            // physics handler (disabled, but Unity still delivers collisions) took 1 endurance
            // every bounce on that screen only: 1000 -> 956 while the server kept 1000
            // (ShootingProbe 2026-09-30). The follower stays kinematic and without contacts.
            if (_rb != null && !_rb.isKinematic) _rb.isKinematic = true;
            if (_mainCollider != null && _mainCollider.enabled) _mainCollider.enabled = false;

            // First frame after the launch tween: blend from where this chicken landed, not from a
            // pose captured mid-jump (that was a 15 m single-frame step on Carme's screen).
            if (!_followingSyncedPose)
            {
                _followingSyncedPose = true;
                _lastSyncedPosition = transform.position;
                _interpolationTime = 0f;
            }

            // Interpolate position smoothly
            _interpolationTime += Time.deltaTime;
            float t = Mathf.Clamp01(_interpolationTime / INTERPOLATION_DURATION);
            
            transform.position = Vector3.Lerp(_lastSyncedPosition, _targetPosition, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, t);
        }

        private void OnPositionChanged(Vector3 prev, Vector3 next, bool asServer)
        {
            if (asServer) return;
            
            _lastSyncedPosition = transform.position;
            _targetPosition = next;
            _interpolationTime = 0f;
        }

        private void OnRotationChanged(Quaternion prev, Quaternion next, bool asServer)
        {
            if (asServer) return;
            _targetRotation = next;
        }

        private void OnClimbingChanged(bool prev, bool next, bool asServer)
        {
            if (asServer) return;
        }

        private void OnLandedChanged(bool prev, bool next, bool asServer)
        {
            if (asServer) return;
        }
        #endregion

        #region OBSERVER_RPCS
        /// <summary>
        /// Notifies clients to play the initial launch animation.
        /// </summary>
        [ObserversRpc]
        private void RpcInitializeChicken(Vector3 targetPosition, Vector3 direction)
        {
            // The owner (predicted spawn) and observers that got the launch in the spawn payload have
            // already started the jump; a second DOJump would restart it from mid-air.
            if (IsServerInitialized || _launched) return;
            _launched = true;
            _mcChicken.InitializeChicken(targetPosition, direction);
        }

        /// <summary>
        /// Notifies clients of a wall bounce for visual/audio feedback.
        /// </summary>
        [ObserversRpc]
        public void RpcNotifyWallBounce(Vector3 bounceDirection)
        {
            if (IsServerInitialized) return;
        }

        /// <summary>
        /// Notifies clients of a character collision for visual/audio feedback.
        /// </summary>
        [ObserversRpc]
        public void RpcNotifyCharacterHit(Vector3 hitPosition)
        {
            if (IsServerInitialized) return;
        }
        #endregion

        #region SERVER_COLLISION_HOOKS
        /// <summary>
        /// Hook into the McChicken's collision system on server.
        /// Call this from McChickenPhysicsHandler when in multiplayer mode.
        /// </summary>
        [Server]
        public void OnServerWallCollision(Vector3 bounceDirection)
        {
            RpcNotifyWallBounce(bounceDirection);
        }

        /// <summary>
        /// Hook for character collision on server.
        /// </summary>
        [Server]
        public void OnServerCharacterCollision(GameObject target, Vector3 hitPosition, Vector3 pushDirection, float pushForce)
        {
            var nob = target.GetComponentInParent<NetworkObject>();
            if (nob != null)
            {
                ServerHandleCharacterCollision(nob, pushDirection, pushForce);
            }
            
            RpcNotifyCharacterHit(hitPosition);
        }
        #endregion
    }
}
