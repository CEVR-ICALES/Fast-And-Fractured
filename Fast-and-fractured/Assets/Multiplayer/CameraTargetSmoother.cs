using Unity.Cinemachine;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Makes the camera follow a point that is smoothed per frame instead of the car's physics
    /// transform, so the view stops trembling.
    ///
    /// Why it is needed. The camera rig aims at CameraLookAtPoint, which a PositionConstraint pins
    /// rigidly to the car's Visuals. Visuals is supposed to be smoothed between ticks by FishNet's
    /// NetworkTickSmoother, but measured live on the client both smoothers (Visuals and Turret) sit
    /// at enabled=false, and Visuals moves exactly in step with the Rigidbody - which, with
    /// interpolation deliberately left at None, only moves on a tick boundary. The simulation runs
    /// at 50 Hz and the client renders at ~100 fps, so the point the camera follows stands still for
    /// one frame and then jumps two ticks' worth on the next. The whole world judders around a car
    /// that is perfectly still on screen, which is what reads as "the camera trembles".
    ///
    /// Measured on the client, same session, car barely moving:
    ///   raw CameraLookAtPoint : 18% of frames with ZERO movement, p95 step 0.0087 m, max 0.0647 m
    ///   smoothed proxy        :  0% of frames with ZERO movement, p95 step 0.0032 m, max 0.0188 m
    ///   cost: proxy trails the real point by 3 mm on average, 47 mm at worst
    ///
    /// This is deliberately a local, per-frame, render-side fix. Nothing about the camera is sent
    /// over the network and nothing here participates in prediction or reconciliation - aiming and
    /// the turret are the only camera-adjacent things that need to be authoritative, and they stay
    /// exactly as they were.
    ///
    /// It does not fix the car model itself still stepping at tick rate; that is the disabled
    /// NetworkTickSmoother, and it is a separate problem.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(CinemachineFreeLook))]
    public class CameraTargetSmoother : MonoBehaviour
    {
        [Tooltip("Seconds for the smoothed point to catch up with the real one. Higher is smoother " +
                 "and laggier; 0.05 removed the stepping while trailing by ~3 mm.")]
        [SerializeField] private float smoothTime = 0.05f;

        [Tooltip("Jumps larger than this are treated as a teleport (spawn, respawn, map change) and " +
                 "snap instead of sliding the camera across the level.")]
        [SerializeField] private float teleportDistance = 5f;

        private CinemachineFreeLook _freeLook;
        private Transform _rawTarget;
        private Transform _proxy;
        private Vector3 _velocity;

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

            // A root object, so nothing in the player hierarchy can move it behind our back.
            _proxy = new GameObject($"[CameraTarget] {name}").transform;
            _proxy.position = _rawTarget.position;
            _velocity = Vector3.zero;

            // Setting these on the FreeLook propagates to its three rigs, so TopRig, MiddleRig and
            // BottomRig all follow the smoothed point too.
            _freeLook.Follow = _proxy;
            _freeLook.LookAt = _proxy;
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

        /// <summary>
        /// LateUpdate, not Update: Unity evaluates the PositionConstraint that drives the real point
        /// in PreLateUpdate, so reading it in Update would sample the previous frame. The execution
        /// order above puts this ahead of CinemachineBrain's own LateUpdate, so the brain always
        /// reads a proxy that was updated this frame.
        /// </summary>
        private void LateUpdate()
        {
            if (_proxy == null || _rawTarget == null) return;

            Vector3 target = _rawTarget.position;

            if ((target - _proxy.position).sqrMagnitude > teleportDistance * teleportDistance)
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
