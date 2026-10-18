using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Object.Prediction;
using FishNet.Connection;
using UnityEngine;
using DG.Tweening;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Server-authoritative McChicken (Carme's ultimate). Other peers play the same launch jump locally and then
    /// follow the synced pose, with AI, physics and colliders off: Unity delivers collisions even to a disabled
    /// McChickenPhysicsHandler, so a client-side chicken pushed cars on that client alone.
    /// </summary>
    [RequireComponent(typeof(McChicken))]
    public class NetworkedMcChickenBehaviour : NetworkBehaviour
    {
        private McChicken _mcChicken;
        private McChickenMovement _movement;
        private McChickenPhysicsHandler _physics;
        private Rigidbody _rb;
        private const float CONTACT_DAMAGE = 1f;

        #region SYNCED_STATE
        public readonly SyncVar<Vector3> SyncedPosition = new SyncVar<Vector3>();

        public readonly SyncVar<Quaternion> SyncedRotation = new SyncVar<Quaternion>();

        public readonly SyncVar<Vector3> SyncedVelocity = new SyncVar<Vector3>();

        public readonly SyncVar<Vector3> SyncedMoveDirection = new SyncVar<Vector3>();

        public readonly SyncVar<bool> SyncedIsClimbing = new SyncVar<bool>();

        public readonly SyncVar<bool> SyncedHasLanded = new SyncVar<bool>();
        #endregion

        #region SERVER_STATE
        private float _syncTimer = 0f;
        private const float SYNC_INTERVAL = 0.05f; // 20Hz sync rate

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

        /// <summary>A client-predicted chicken is validated before the server spawns it; its launch waits for OnStartServer.</summary>
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

            SyncedPosition.OnChange += OnPositionChanged;
            SyncedRotation.OnChange += OnRotationChanged;
            SyncedIsClimbing.OnChange += OnClimbingChanged;
            SyncedHasLanded.OnChange += OnLandedChanged;
        }

        /// <summary>Visual-only chicken: no physics, AI or contacts; the server's chicken is the one that moves and hits.</summary>
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

            // The launch and landing tweens own the transform until they finish.
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

        [Server]
        public void ServerInitializeChicken(Vector3 targetPosition, Vector3 direction)
        {
            _mcChicken.InitializeChicken(targetPosition, direction);
            SyncedMoveDirection.Value = direction.normalized;
            
            RpcInitializeChicken(targetPosition, direction);
        }

        internal void InitializePredictedClient(Vector3 targetPosition, Vector3 direction)
        {
            if (IsServerInitialized || _launched) return;
            _launched = true;
            _mcChicken.InitializeChicken(targetPosition, direction);
        }

        [Server]
        public void ServerNotifyLanded()
        {
            SyncedHasLanded.Value = true;
            SyncStateToClients();
        }

        [Server]
        public void ServerHandleCharacterCollision(NetworkObject victim, Vector3 pushDirection, float pushForce)
        {
            if (victim == null) return;

            var stats = victim.GetComponentInChildren<NetworkedStatsController>();
            if (stats != null)
            {
                stats.DealDamageServerRpc(CONTACT_DAMAGE, null);
            }

            var vehicle = victim.GetComponentInChildren<NetworkedVehicle>();
            if (vehicle != null)
            {
                vehicle.ApplyExternalForce(pushDirection * pushForce);
            }
        }
        #endregion

        #region CLIENT_METHODS
        private bool _followingSyncedPose;

        private void ClientUpdate()
        {
            // Land() re-enables the body and collider: the follower stays kinematic and without contacts, or it hits cars here.
            if (_rb != null && !_rb.isKinematic) _rb.isKinematic = true;
            if (_mainCollider != null && _mainCollider.enabled) _mainCollider.enabled = false;

            // Blend from where this chicken landed, not from a pose captured mid-jump.
            if (!_followingSyncedPose)
            {
                _followingSyncedPose = true;
                _lastSyncedPosition = transform.position;
                _interpolationTime = 0f;
            }

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
        [ObserversRpc]
        private void RpcInitializeChicken(Vector3 targetPosition, Vector3 direction)
        {
            // Peers that already started the jump (owner, spawn payload) skip it: a second DOJump restarts it mid-air.
            if (IsServerInitialized || _launched) return;
            _launched = true;
            _mcChicken.InitializeChicken(targetPosition, direction);
        }

        [ObserversRpc]
        public void RpcNotifyWallBounce(Vector3 bounceDirection)
        {
            if (IsServerInitialized) return;
        }

        [ObserversRpc]
        public void RpcNotifyCharacterHit(Vector3 hitPosition)
        {
            if (IsServerInitialized) return;
        }
        #endregion

        #region SERVER_COLLISION_HOOKS
        [Server]
        public void OnServerWallCollision(Vector3 bounceDirection)
        {
            RpcNotifyWallBounce(bounceDirection);
        }

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
