using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Draws the car's graphical children (Visuals, Turret) from an interpolation buffer so tick-sized body steps
    /// stop showing as judder. Samples are taken in FixedUpdate, not on FishNet's tick: that is a separate clock
    /// and produced empty steps followed by double ones. Purely local and render-side.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class VehicleVisualSmoother : MonoBehaviour
    {
        private const float RENDER_LAG_SLACK_STEPS = 1f;

        [Tooltip("How far in the past to draw, in physics steps. Must exceed one rendered frame or " +
                 "there is nothing to interpolate between; 2 steps (40 ms at the default 0.02s fixed " +
                 "timestep) covers frame rates down to 25 fps. Raise it if the car still steps, " +
                 "lower it if the car feels detached.")]
        [SerializeField] private float interpolationSteps = 2f;

        [Tooltip("Jumps larger than this are a teleport (spawn, respawn, reconcile correction) and " +
                 "snap instead of sliding the model across the level.")]
        [SerializeField] private float teleportDistance = 5f;

        [Tooltip("Seconds over which a network correction of the body is blended out of the drawn " +
                 "car instead of showing as a jump (SmoothDamp time: longer is softer and lets the " +
                 "drawn car sit further from the body while it blends).")]
        [SerializeField] private float correctionSmoothingTime = 0.1f;

        [Tooltip("Corrections larger than this are a real teleport (respawn, desync) and snap.")]
        [SerializeField] private float maxSmoothedCorrection = 8f;

        private readonly struct Sample
        {
            /// <summary>Local physics-step counter, not FishNet's Tick.</summary>
            public readonly long Step;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;

            public Sample(long step, Vector3 position, Quaternion rotation)
            {
                Step = step;
                Position = position;
                Rotation = rotation;
            }
        }

        private const int BufferSize = 24;

        private readonly List<Sample> _samples = new(BufferSize);
        private readonly List<Transform> _children = new();
        private readonly List<Vector3> _authoredLocalPositions = new();
        private readonly List<Quaternion> _authoredLocalRotations = new();

        private long _stepCounter;
        private Rigidbody _body;

        // What is still left to blend out of the drawn pose from reconcile corrections.
        private Vector3 _positionOffset;
        private Vector3 _positionOffsetVelocity;
        private Quaternion _rotationOffset = Quaternion.identity;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            CacheChildren();
        }

        // Sampled from the Rigidbody: with autoSyncTransforms off a reconcile only reaches the Transform next step.
        private Vector3 BodyPosition => _body != null ? _body.position : transform.position;
        private Quaternion BodyRotation => _body != null ? _body.rotation : transform.rotation;
        private float BodySpeed => _body != null ? _body.linearVelocity.magnitude : 0f;

        /// <summary>
        /// After a reconcile moved the body: shifts the samples with it and eases the drawn car into the corrected
        /// pose over correctionSmoothingTime instead of snapping.
        /// </summary>
        public void AbsorbCorrection(Vector3 positionDelta, Quaternion rotationDelta)
        {
            if (positionDelta.sqrMagnitude > maxSmoothedCorrection * maxSmoothedCorrection)
            {
                _samples.Clear();
                ClearCorrection();
                return;
            }

            for (int i = 0; i < _samples.Count; i++)
            {
                Sample sample = _samples[i];
                _samples[i] = new Sample(sample.Step, sample.Position + positionDelta, rotationDelta * sample.Rotation);
            }

            _positionOffset -= positionDelta;
            _rotationOffset = _rotationOffset * Quaternion.Inverse(rotationDelta);
        }

        private void ClearCorrection()
        {
            _positionOffset = Vector3.zero;
            _positionOffsetVelocity = Vector3.zero;
            _rotationOffset = Quaternion.identity;
        }

        /// <summary>Captures the authored local pose of Visuals and Turret (restored, not zeroed: the turret is offset).</summary>
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

        private void OnDisable()
        {
            _samples.Clear();
            ClearCorrection();
            RestoreAuthoredPose();
        }

        private void FixedUpdate()
        {
            if (_samples.Count == BufferSize) _samples.RemoveAt(0);
            _samples.Add(new Sample(_stepCounter, BodyPosition, BodyRotation));
            _stepCounter++;
        }

        // Update, not LateUpdate: the camera's PositionConstraint is evaluated before LateUpdate, so composing
        // later made the camera trail the drawn car by a frame.
        private void Update()
        {
            if (_children.Count == 0 || _samples.Count < 2) return;

            // Unity's fixed-timestep interpolation fraction.
            float frac = Mathf.Clamp01((float)((Time.timeAsDouble - Time.fixedTimeAsDouble) / Time.fixedDeltaTime));
            float renderTime = _samples[_samples.Count - 1].Step + frac - interpolationSteps;

            if (!TryEvaluate(renderTime, out Vector3 position, out Quaternion rotation)) return;

            // The drawn pose lags interpolationSteps (+1) steps behind, metres at dash speed: only the excess is a teleport.
            float allowedGap = teleportDistance + BodySpeed * (interpolationSteps + RENDER_LAG_SLACK_STEPS) * Time.fixedDeltaTime;
            if ((position - BodyPosition).sqrMagnitude > allowedGap * allowedGap)
            {
                _samples.Clear();
                ClearCorrection();
                RestoreAuthoredPose();
                return;
            }

            // Critically damped: an exponential decay starts at full rate and steps the drawn car's velocity.
            if (correctionSmoothingTime > 0f)
            {
                _positionOffset = Vector3.SmoothDamp(_positionOffset, Vector3.zero, ref _positionOffsetVelocity,
                                                     correctionSmoothingTime, Mathf.Infinity, Time.deltaTime);
                _rotationOffset = Quaternion.Slerp(_rotationOffset, Quaternion.identity,
                                                   1f - Mathf.Exp(-Time.deltaTime / correctionSmoothingTime));
            }
            else
            {
                ClearCorrection();
            }
            position += _positionOffset;
            rotation = _rotationOffset * rotation;

            // Composed from the smoothed body pose, not the live one, so resimulation jumps are absorbed too.
            for (int i = 0; i < _children.Count; i++)
            {
                Transform child = _children[i];
                if (child == null) continue;

                child.SetPositionAndRotation(
                    position + rotation * _authoredLocalPositions[i],
                    rotation * _authoredLocalRotations[i]);
            }
        }

        /// <summary>Interpolates the samples around <paramref name="renderTime"/>; false when the buffer is too short.</summary>
        private bool TryEvaluate(float renderTime, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;

            Sample newest = _samples[_samples.Count - 1];
            if (renderTime >= newest.Step)
            {
                // Rendering fell behind the buffer: the newest sample is all there is.
                position = newest.Position;
                rotation = newest.Rotation;
                return true;
            }

            if (renderTime <= _samples[0].Step) return false;

            for (int i = _samples.Count - 1; i > 0; i--)
            {
                Sample to = _samples[i];
                Sample from = _samples[i - 1];
                if (renderTime < from.Step) continue;

                float span = to.Step - from.Step;
                float t = span > Mathf.Epsilon ? Mathf.Clamp01((renderTime - from.Step) / span) : 1f;
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
