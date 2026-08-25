using System.Collections.Generic;
using FishNet;
using FishNet.Managing.Timing;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Draws the car's graphical children on a small interpolation buffer so the body's tick-sized
    /// steps stop showing as judder.
    ///
    /// Why not FishNet's NetworkTickSmoother: it is installed on Visuals and Turret but
    /// NetworkedVehicle.ApplyVisualSmoothingForOwnership stops it on purpose, because it did not
    /// converge here - a remote car's Visuals ended up 262 m from its body and kept drifting. See
    /// that method for the measurements. So the car has been drawn straight off the physics
    /// transform, which only moves on a tick boundary.
    ///
    /// Why a buffer and not simple smoothing: measured on the client, the editor renders at ~36 fps
    /// while the simulation ticks at 50 Hz, so Time.deltaTime (0.028 s) is LONGER than the tick
    /// (0.02 s). Each frame therefore consumes one or two ticks, alternating, and the car advances
    /// in a 1x / 2x pattern - the raw per-frame steps read 0.105, 0.233, 0.120, 0.228, 0.098, 0.201.
    /// Exponential smoothing only halves that ripple; chasing "one tick behind" cannot help at all,
    /// because with deltaTime above tickDelta the interpolant is already pinned at 1 every frame.
    /// Rendering a fixed short delay in the past and interpolating between the two tick samples that
    /// bracket it gives uniform motion whatever the frame rate costs.
    ///
    /// It cannot drift the way the FishNet smoother did: every frame the pose is rebuilt from the
    /// body's CURRENT transform plus a bounded offset, and anything past teleportDistance snaps.
    ///
    /// Purely local and render-side. Nothing here is networked, predicted or reconciled; the
    /// physics body, aiming and the turret's own rotation are untouched.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class VehicleVisualSmoother : MonoBehaviour
    {
        [Tooltip("How far in the past to draw, in ticks. Must exceed one rendered frame or there is " +
                 "nothing to interpolate between; 2 ticks (40 ms at 50 Hz) covers frame rates down " +
                 "to 25 fps. Raise it if the car still steps, lower it if the car feels detached.")]
        [SerializeField] private float interpolationTicks = 2f;

        [Tooltip("Jumps larger than this are a teleport (spawn, respawn, reconcile correction) and " +
                 "snap instead of sliding the model across the level.")]
        [SerializeField] private float teleportDistance = 5f;

        private readonly struct Sample
        {
            /// <summary>
            /// The simulation tick this pose belongs to. Deliberately NOT a Time.unscaledTime
            /// stamp: FishNet can run several ticks inside one rendered frame, and Unity's clock
            /// does not advance between them, so wall-clock stamped samples come out with
            /// identical times and the interpolation between them collapses.
            /// </summary>
            public readonly uint Tick;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;

            public Sample(uint tick, Vector3 position, Quaternion rotation)
            {
                Tick = tick;
                Position = position;
                Rotation = rotation;
            }
        }

        private const int BufferSize = 24;

        private readonly List<Sample> _samples = new(BufferSize);
        private readonly List<Transform> _children = new();
        private readonly List<Vector3> _authoredLocalPositions = new();
        private readonly List<Quaternion> _authoredLocalRotations = new();

        private TimeManager _timeManager;
        private bool _subscribed;

        /// <summary>Seconds elapsed since the most recent tick, used for the fractional part of the
        /// render position between two samples.</summary>
        private float _timeSinceTick;

        private void Awake() => CacheChildren();

        /// <summary>
        /// The graphical children are the ones the setup tools put a NetworkTickSmoother on -
        /// Visuals and Turret. Their authored local pose is captured here, in Awake, before anything
        /// has had a chance to displace them; the turret sits above the body, so zeroing instead of
        /// restoring would be wrong.
        /// </summary>
        private void CacheChildren()
        {
            foreach (var smoother in GetComponentsInChildren<FishNet.Component.Transforming.Beta.NetworkTickSmoother>(true))
            {
                if (smoother == null || smoother.transform == transform) continue;
                _children.Add(smoother.transform);
                _authoredLocalPositions.Add(smoother.transform.localPosition);
                _authoredLocalRotations.Add(smoother.transform.localRotation);
            }
        }

        private void OnEnable() => TrySubscribe();

        private void OnDisable()
        {
            if (_subscribed && _timeManager != null) _timeManager.OnPostTick -= OnPostTick;
            _subscribed = false;
            _samples.Clear();
            _timeSinceTick = 0f;
            RestoreAuthoredPose();
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;
            _timeManager = InstanceFinder.TimeManager;
            if (_timeManager == null) return;

            _timeManager.OnPostTick += OnPostTick;
            _subscribed = true;
        }

        private void OnPostTick()
        {
            if (_samples.Count == BufferSize) _samples.RemoveAt(0);
            _samples.Add(new Sample(_timeManager.Tick, transform.position, transform.rotation));
            _timeSinceTick = 0f;
        }

        private void LateUpdate()
        {
            if (!_subscribed)
            {
                TrySubscribe();
                return;
            }

            if (_children.Count == 0 || _samples.Count < 2) return;

            _timeSinceTick += Time.deltaTime;

            float tickDelta = (float)_timeManager.TickDelta;
            // Position on the tick timeline, in ticks: the newest sample, plus however far into the
            // current tick we are, minus the delay we deliberately render behind.
            float renderTime = _samples[_samples.Count - 1].Tick
                               + Mathf.Clamp01(_timeSinceTick / tickDelta)
                               - interpolationTicks;

            if (!TryEvaluate(renderTime, out Vector3 position, out Quaternion rotation)) return;

            Vector3 bodyPosition = transform.position;
            Quaternion bodyRotation = transform.rotation;

            if ((position - bodyPosition).sqrMagnitude > teleportDistance * teleportDistance)
            {
                _samples.Clear();
                _timeSinceTick = 0f;
                RestoreAuthoredPose();
                return;
            }

            // Rebuild each child from its authored local pose, then apply the body-to-render-pose
            // correction as one rigid transform about the body origin. Starting from the authored
            // pose every frame is what keeps this from accumulating error.
            Quaternion deltaRotation = rotation * Quaternion.Inverse(bodyRotation);
            Vector3 deltaPosition = position - bodyPosition;

            for (int i = 0; i < _children.Count; i++)
            {
                Transform child = _children[i];
                if (child == null) continue;

                child.SetLocalPositionAndRotation(_authoredLocalPositions[i], _authoredLocalRotations[i]);
                child.SetPositionAndRotation(
                    bodyPosition + deltaRotation * (child.position - bodyPosition) + deltaPosition,
                    deltaRotation * child.rotation);
            }
        }

        /// <summary>
        /// Interpolates between the two samples bracketing <paramref name="renderTime"/>. Returns
        /// false when the buffer does not reach that far back yet, in which case the caller leaves
        /// the children alone rather than guessing.
        /// </summary>
        private bool TryEvaluate(float renderTime, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;

            Sample newest = _samples[_samples.Count - 1];
            if (renderTime >= newest.Tick)
            {
                // Rendering has fallen behind the buffer; drawing the newest sample is the same as
                // doing nothing, which is correct - there is nothing newer to interpolate toward.
                position = newest.Position;
                rotation = newest.Rotation;
                return true;
            }

            if (renderTime <= _samples[0].Tick) return false;

            for (int i = _samples.Count - 1; i > 0; i--)
            {
                Sample to = _samples[i];
                Sample from = _samples[i - 1];
                if (renderTime < from.Tick) continue;

                float span = to.Tick - from.Tick;
                float t = span > Mathf.Epsilon ? Mathf.Clamp01((renderTime - from.Tick) / span) : 1f;
                position = Vector3.Lerp(from.Position, to.Position, t);
                rotation = Quaternion.Slerp(from.Rotation, to.Rotation, t);
                return true;
            }

            return false;
        }

        private void RestoreAuthoredPose()
        {
            for (int i = 0; i < _children.Count; i++)
            {
                if (_children[i] == null) continue;
                _children[i].SetLocalPositionAndRotation(_authoredLocalPositions[i], _authoredLocalRotations[i]);
            }
        }
    }
}
