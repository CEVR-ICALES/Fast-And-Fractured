using System.Collections.Generic;
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
    /// transform, which only moves on a physics-step boundary.
    ///
    /// Third revision (2026-09-11), and the one that actually holds up. The first two versions
    /// keyed samples to FishNet's TimeManager.Tick / GetPreciseTick(), which runs from FishNet's
    /// own Update()-driven accumulator - a SEPARATE clock from Unity's native fixed-timestep
    /// accumulator that actually steps physics under this project's PhysicsMode.Unity (deliberate;
    /// see MultiplayerTimingBootstrap - PhysicsMode.TimeManager re-simulates the whole PhysicsScene
    /// per replay tick and hard-locks both editors on the physics-heavy maps, so unifying the two
    /// clocks by moving physics onto FishNet's clock is not an option here). Two independent
    /// accumulators nominally at the same 50 Hz can still land on opposite sides of their own
    /// tick-boundary check within the same frame, entirely from normal per-frame timing jitter - no
    /// drift or precision bug required. Measured live: reading the body's transform inside
    /// FishNet's OnPostTick produced an EXACT 0.000 m step every 2-3 samples (physics had not
    /// advanced yet for that "tick" of FishNet's clock), followed by a double-size catch-up - not
    /// smoothable, because the noise is already in the recorded signal.
    ///
    /// The fix is not to reconcile the two clocks but to stop reading FishNet's clock in this file
    /// at all. A physics-driven transform only ever moves inside Unity's own FixedUpdate, so
    /// sampling from FixedUpdate cannot race it - there is no "tick with no physics behind it" to
    /// observe, because FixedUpdate IS the physics step. The render fraction is Unity's own
    /// textbook fixed-timestep interpolation math ((Time.time - Time.fixedTime) /
    /// Time.fixedDeltaTime), exactly how Unity's docs describe smoothing a fixed-timestep value for
    /// rendering - no FishNet involvement, so there is nothing left for two clocks to disagree
    /// about.
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
        [Tooltip("How far in the past to draw, in physics steps. Must exceed one rendered frame or " +
                 "there is nothing to interpolate between; 2 steps (40 ms at the default 0.02s fixed " +
                 "timestep) covers frame rates down to 25 fps. Raise it if the car still steps, " +
                 "lower it if the car feels detached.")]
        [SerializeField] private float interpolationSteps = 2f;

        [Tooltip("Jumps larger than this are a teleport (spawn, respawn, reconcile correction) and " +
                 "snap instead of sliding the model across the level.")]
        [SerializeField] private float teleportDistance = 5f;

        private readonly struct Sample
        {
            /// <summary>
            /// A local, monotonically increasing physics-step counter - not FishNet's network Tick.
            /// It only needs to number this component's own FixedUpdate calls consistently; see the
            /// class doc for why mixing in FishNet's separate clock is exactly what broke this twice.
            /// </summary>
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

        private void OnDisable()
        {
            _samples.Clear();
            RestoreAuthoredPose();
        }

        /// <summary>
        /// Runs once per REAL physics step, by definition - the whole point. Nothing here can
        /// observe a step that has not happened yet, unlike reading a network tick callback that
        /// fires from a different clock than the one that actually moves the Rigidbody.
        /// </summary>
        private void FixedUpdate()
        {
            if (_samples.Count == BufferSize) _samples.RemoveAt(0);
            _samples.Add(new Sample(_stepCounter, transform.position, transform.rotation));
            _stepCounter++;
        }

        private void LateUpdate()
        {
            if (_children.Count == 0 || _samples.Count < 2) return;

            // Unity's own fixed-timestep interpolation fraction - how far past the last FixedUpdate
            // "now" is, as a fraction of one physics step. Native to Unity, so there is no second
            // clock for it to disagree with.
            float frac = Mathf.Clamp01((float)((Time.timeAsDouble - Time.fixedTimeAsDouble) / Time.fixedDeltaTime));
            float renderTime = _samples[_samples.Count - 1].Step + frac - interpolationSteps;

            if (!TryEvaluate(renderTime, out Vector3 position, out Quaternion rotation)) return;

            if ((position - transform.position).sqrMagnitude > teleportDistance * teleportDistance)
            {
                _samples.Clear();
                RestoreAuthoredPose();
                return;
            }

            // Children are composed directly from the SMOOTHED body pose - not the live one - so
            // any sudden jump in the body's own transform gets absorbed by the interpolation delay
            // instead of passing straight through.
            //
            // The first version anchored to transform.position/rotation (read fresh every frame)
            // and only smoothed the offset from it, which is correct as long as the body's raw
            // motion is merely choppy in time, never wrong in value - true for ordinary tick
            // stepping, but not for a resimulation artifact: a real client that has to replay
            // several ticks after a reconcile queues each tick's forces via
            // PredictionRigidbody.Simulate() (AddForce/MovePosition), and under this project's
            // PhysicsMode.Unity nothing steps physics between those replayed ticks (FishNet only
            // does that under PhysicsMode.TimeManager, ruled out elsewhere - it hard-locks the
            // editor on physics-heavy maps AND stops Unity calling FixedUpdate at all, which this
            // whole component depends on - tested live, both true). So several ticks' worth of
            // force get integrated as one merged step, and the body can end up somewhere visibly
            // wrong for a frame. Anchoring to the live body position let that artifact bypass the
            // smoothing entirely; composing from the smoothed pose instead means it gets absorbed
            // like any other body-level discontinuity, whatever caused it.
            for (int i = 0; i < _children.Count; i++)
            {
                Transform child = _children[i];
                if (child == null) continue;

                child.SetPositionAndRotation(
                    position + rotation * _authoredLocalPositions[i],
                    rotation * _authoredLocalRotations[i]);
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
            if (renderTime >= newest.Step)
            {
                // Rendering has fallen behind the buffer; drawing the newest sample is the same as
                // doing nothing, which is correct - there is nothing newer to interpolate toward.
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
