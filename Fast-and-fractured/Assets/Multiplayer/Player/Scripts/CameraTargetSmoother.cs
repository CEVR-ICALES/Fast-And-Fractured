using Unity.Cinemachine;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// The camera follows a proxy smoothed per frame instead of CameraLookAtPoint, which is pinned to the
    /// tick-stepped car and made the whole view judder. Local and render-side only.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(CinemachineFreeLook))]
    public class CameraTargetSmoother : MonoBehaviour
    {
        [Tooltip("Seconds for the smoothed point to catch up with the real one. Higher is smoother " +
                 "and laggier; 0.05 removed the stepping while trailing by ~3 mm.")]
        [SerializeField] private float smoothTime = 0.05f;

        [Tooltip("A jump this much beyond what the target's own motion explains is a teleport " +
                 "(spawn, respawn, map change) and snaps instead of sliding the camera across the level.")]
        [SerializeField] private float teleportDistance = 5f;

        private CinemachineFreeLook _freeLook;
        private Transform _rawTarget;
        private Transform _proxy;
        private Vector3 _velocity;

        // The real target's own motion, to tell a teleport from fast driving.
        private Vector3 _lastTarget;
        private Vector3 _targetVelocity;

        private void OnEnable()
        {
            _freeLook = GetComponent<CinemachineFreeLook>();
            if (_freeLook == null)
            {
                enabled = false;
                return;
            }

            _rawTarget = _freeLook.Follow;
            if (_rawTarget == null)
            {
                Debug.LogWarning($"[CameraTargetSmoother] '{name}' has no Follow target; nothing to smooth.");
                enabled = false;
                return;
            }

            // A root object, so nothing in the player hierarchy moves it.
            _proxy = new GameObject($"[CameraTarget] {name}").transform;
            _proxy.position = _rawTarget.position;
            _velocity = Vector3.zero;
            _lastTarget = _rawTarget.position;
            _targetVelocity = Vector3.zero;

            // Set on the FreeLook, this reaches its three rigs.
            _freeLook.Follow = _proxy;
            _freeLook.LookAt = _proxy;
        }

        /// <summary>The point the proxy chases (the car's Visuals, not the proxy itself).</summary>
        public Transform RawTarget => _rawTarget;

        /// <summary>Chases another point (the spectator), through the same smoothing; the camera jumps there.</summary>
        public void Retarget(Transform target)
        {
            if (target == null || target == _rawTarget) return;
            _rawTarget = target;
            if (_proxy == null) return;
            _proxy.position = target.position;
            _velocity = Vector3.zero;
            _lastTarget = target.position;
            _targetVelocity = Vector3.zero;
        }

        private void OnDisable()
        {
            if (_freeLook != null && _rawTarget != null && _freeLook.Follow == _proxy)
            {
                _freeLook.Follow = _rawTarget;
                _freeLook.LookAt = _rawTarget;
            }

            if (_proxy != null)
            {
                Destroy(_proxy.gameObject);
                _proxy = null;
            }
        }

        /// <summary>LateUpdate: the PositionConstraint moving the real point runs in PreLateUpdate.</summary>
        private void LateUpdate()
        {
            if (_proxy == null || _rawTarget == null) return;

            Vector3 target = _rawTarget.position;
            float deltaTime = Time.deltaTime;

            // A teleport is a jump the target's own motion does not explain; the proxy itself trails by speed * smoothTime.
            Vector3 expected = _lastTarget + _targetVelocity * deltaTime;
            bool teleported = (target - expected).sqrMagnitude > teleportDistance * teleportDistance;
            _targetVelocity = teleported || deltaTime <= 0f ? Vector3.zero : (target - _lastTarget) / deltaTime;
            _lastTarget = target;

            if (teleported)
            {
                _proxy.position = target;
                _velocity = Vector3.zero;
                return;
            }

            _proxy.position = Vector3.SmoothDamp(_proxy.position, target, ref _velocity, smoothTime,
                                                 Mathf.Infinity, Time.deltaTime);
        }
    }
}
