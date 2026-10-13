using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Golf balls (dev, 2026-10) are plain rigidbodies that cars knock around and that push cars
    /// back. Left alone, every peer would simulate its own copy and the balls would end up in
    /// different places on each screen.
    ///
    /// The server simulates them (GolfBallBehaviour, GolfBallSpawnHandle and the dead walls all
    /// run there) and streams position, rotation and velocity while a ball moves. Clients show a
    /// smoothed copy: no physics and no colliders of their own, so a predicted car does not
    /// bounce off a ball that is somewhere else on the server - the car's collision with the
    /// real ball arrives through the server's correction of the car.
    /// Added to the ball by the scene migration tool (HazardDef GolfBallBehaviour).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkedGolfBall : NetworkBehaviour
    {
        private const float SEND_INTERVAL = 0.05f;          // 20 Hz while moving
        private const float MOVING_SPEED = 0.05f;           // m/s
        private const float TELEPORT_DISTANCE = 8f;         // respawned: snap instead of gliding
        private const float MAX_EXTRAPOLATION = 0.15f;      // seconds
        private const float SMOOTHING = 18f;

        private Rigidbody _rb;
        private float _nextSend;
        private bool _restSent = true;
        private Vector3 _lastSentPosition;

        private bool _hasTarget;
        private Vector3 _targetPosition, _targetVelocity;
        private Quaternion _targetRotation;
        private float _targetTime;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (base.IsServerInitialized) return;

            // Client: display only.
            if (_rb != null)
            {
                _rb.isKinematic = true;
                _rb.interpolation = RigidbodyInterpolation.None;
            }
            foreach (var collider in GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (var ball in GetComponentsInChildren<GolfBallBehaviour>(true))
                Destroy(ball);
        }

        private void Update()
        {
            if (base.IsServerInitialized) ServerStream();
            else if (base.IsClientInitialized) ClientFollow();
        }

        // ── Server ─────────────────────────────────────────────────────────────────────
        private void ServerStream()
        {
            if (_rb == null || Time.time < _nextSend) return;
            _nextSend = Time.time + SEND_INTERVAL;

            bool moving = _rb.linearVelocity.sqrMagnitude > MOVING_SPEED * MOVING_SPEED
                          || (_rb.position - _lastSentPosition).sqrMagnitude > 0.0001f;
            if (moving)
            {
                _restSent = false;
                _lastSentPosition = _rb.position;
                RpcState(_rb.position, _rb.rotation, _rb.linearVelocity, Channel.Unreliable);
            }
            else if (!_restSent)
            {
                // One reliable "stopped here" so a lost packet cannot leave it slightly off.
                _restSent = true;
                _lastSentPosition = _rb.position;
                RpcState(_rb.position, _rb.rotation, Vector3.zero, Channel.Reliable);
            }
        }

        [ObserversRpc(BufferLast = true)]
        private void RpcState(Vector3 position, Quaternion rotation, Vector3 velocity, Channel channel = Channel.Unreliable)
        {
            if (base.IsServerInitialized) return;
            bool teleported = !_hasTarget || (position - transform.position).sqrMagnitude > TELEPORT_DISTANCE * TELEPORT_DISTANCE;
            _targetPosition = position;
            _targetRotation = rotation;
            _targetVelocity = velocity;
            _targetTime = Time.time;
            _hasTarget = true;
            if (teleported) transform.SetPositionAndRotation(position, rotation);
        }

        // ── Client ─────────────────────────────────────────────────────────────────────
        private void ClientFollow()
        {
            if (!_hasTarget) return;
            float ahead = Mathf.Min(Time.time - _targetTime, MAX_EXTRAPOLATION);
            Vector3 predicted = _targetPosition + _targetVelocity * ahead;
            float t = 1f - Mathf.Exp(-SMOOTHING * Time.deltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, predicted, t),
                Quaternion.Slerp(transform.rotation, _targetRotation, t));
        }

        // ── Host migration ─────────────────────────────────────────────────────────────
        private static System.Collections.Generic.List<NetworkedGolfBall> AllInHierarchyOrder()
        {
            var list = new System.Collections.Generic.List<NetworkedGolfBall>(FindObjectsByType<NetworkedGolfBall>(FindObjectsSortMode.None));
            var keys = new System.Collections.Generic.Dictionary<NetworkedGolfBall, string>();
            foreach (var b in list) keys[b] = NetworkedWorldBreakables.PathKey(b.transform);
            list.Sort((a, b) => string.CompareOrdinal(keys[a], keys[b]));
            return list;
        }

        public static Vector3[] DescribePositions()
        {
            var list = AllInHierarchyOrder();
            var positions = new Vector3[list.Count];
            for (int i = 0; i < list.Count; i++) positions[i] = list[i].ServerPosition;
            return positions;
        }

        public static void ServerRestorePositions(Vector3[] positions)
        {
            if (positions == null) return;
            var list = AllInHierarchyOrder();
            if (list.Count != positions.Length) return;
            for (int i = 0; i < list.Count; i++)
                if (list[i].IsServerInitialized) list[i].ServerRestore(positions[i]);
        }

        /// <summary>Server: where the ball is now (snapshot).</summary>
        public Vector3 ServerPosition => _rb != null ? _rb.position : transform.position;

        /// <summary>Server, migrated match: put the ball back where it was.</summary>
        public void ServerRestore(Vector3 position)
        {
            if (_rb != null)
            {
                _rb.position = position;
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            transform.position = position;
            _restSent = false;
        }
    }
}
