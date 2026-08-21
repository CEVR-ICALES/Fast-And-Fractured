using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Object.Prediction;
using FishNet.Connection;
using UnityEngine;

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
        #endregion

        private void Awake()
        {
            _mcChicken = GetComponent<McChicken>();
            _movement = GetComponent<McChickenMovement>();
            _physics = GetComponent<McChickenPhysicsHandler>();
            _rb = GetComponent<Rigidbody>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Initialize synced state
            SyncedPosition.Value = transform.position;
            SyncedRotation.Value = transform.rotation;
            SyncedVelocity.Value = Vector3.zero;
            SyncedHasLanded.Value = false;
            SyncedIsClimbing.Value = false;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (!IsServerInitialized && !IsOwner)
            {
                // Client-only: disable physics simulation, we'll interpolate from server state
                if (_rb != null)
                {
                    _rb.isKinematic = true;
                }
                
                _targetPosition = transform.position;
                _targetRotation = transform.rotation;
                _lastSyncedPosition = transform.position;
            }

            // Subscribe to state changes
            SyncedPosition.OnChange += OnPositionChanged;
            SyncedRotation.OnChange += OnRotationChanged;
            SyncedIsClimbing.OnChange += OnClimbingChanged;
            SyncedHasLanded.OnChange += OnLandedChanged;
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
            }
            else if (IsClientInitialized && !IsOwner)
            {
                ClientUpdate();
            }
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
            if (IsServerInitialized) return;
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
        private void ClientUpdate()
        {
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
            if (IsServerInitialized) return;
            
            // On clients, we let the local McChicken play the DOJump animation
            // but physics will be overridden by server sync
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
