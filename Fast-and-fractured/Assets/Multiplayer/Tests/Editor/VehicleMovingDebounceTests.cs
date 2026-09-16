using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>
    /// Pins PhysicsBehaviour.IsVehicleMoving()'s hysteresis + cooldown, found and fixed 2026-09-16
    /// after the user reported live, in a real match, that the car's state machine was flapping
    /// between PlayerIdleState and GroundedState every 20-100ms (mp_canary.log showed it happening
    /// continuously while they drove). A speed dwelling right on the hysteresis band's edge - not
    /// just spiking across it once - still chatters with a dead band alone.
    ///
    /// The first attempt at a fix used a CONFIRMATION filter (require MovingDebounceTicks
    /// consecutive agreeing calls before flipping). This test file's first version caught, the same
    /// day, that a confirmation filter delays every transition by the whole window - including a
    /// clean one-time "starts moving from a dead stop" - adding real input lag to normal driving.
    /// The shipped design is a COOLDOWN instead: a flip happens immediately, then blocks any
    /// further flip for MovingDebounceTicks calls. These tests pin that distinction directly, so a
    /// future "improvement" back to a confirmation filter fails loudly instead of shipping.
    ///
    /// EditMode test: PhysicsBehaviour.Start() is never invoked (no player loop here), so
    /// _currentSpeed/_wasMoving/_ticksSinceLastFlip can be driven directly via reflection without
    /// dragging in StatsController/CarMovementController/rigidbody setup this test does not need.
    /// </summary>
    public class VehicleMovingDebounceTests
    {
        private GameObject _go;
        private PhysicsBehaviour _pb;
        private FieldInfo _currentSpeedField;
        private FieldInfo _wasMovingField;
        private FieldInfo _ticksSinceLastFlipField;
        private const int DebounceTicks = 5; // must match PhysicsBehaviour.MovingDebounceTicks

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("PhysicsBehaviour under test");
            _pb = _go.AddComponent<PhysicsBehaviour>();

            _currentSpeedField = typeof(PhysicsBehaviour).GetField("_currentSpeed", BindingFlags.Instance | BindingFlags.NonPublic);
            _wasMovingField = typeof(PhysicsBehaviour).GetField("_wasMoving", BindingFlags.Instance | BindingFlags.NonPublic);
            _ticksSinceLastFlipField = typeof(PhysicsBehaviour).GetField("_ticksSinceLastFlip", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(_currentSpeedField, "_currentSpeed renamed? update the test with it");
            Assert.IsNotNull(_wasMovingField, "_wasMoving renamed? update the test with it");
            Assert.IsNotNull(_ticksSinceLastFlipField, "_ticksSinceLastFlip renamed? update the test with it");

            typeof(PhysicsBehaviour)
                .GetField("isMovingThreshold", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(_pb, 0.5f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        private void SetSpeed(float speed) => _currentSpeedField.SetValue(_pb, speed);
        private bool WasMoving() => (bool)_wasMovingField.GetValue(_pb);

        [Test]
        public void GenuineTransitionFromRest_FlipsOnTheVeryNextCall()
        {
            // The whole point of a cooldown over a confirmation filter: a clean, one-time
            // transition must not cost any input lag. Not "within a few calls" - the next one.
            SetSpeed(2f);
            Assert.IsTrue(_pb.IsVehicleMoving(),
                "A car accelerating from a dead stop must be recognised as moving immediately, " +
                "not after a multi-call confirmation window - that would add perceptible input lag " +
                "to ordinary driving, not just reject noise.");
        }

        [Test]
        public void GenuineStop_FlipsOnTheVeryNextCallOnceCooldownHasElapsed()
        {
            SetSpeed(2f);
            Assert.IsTrue(_pb.IsVehicleMoving());

            // Let the post-flip cooldown fully elapse while genuinely still moving, matching how a
            // real drive looks: sustained speed, no edge-dwelling.
            for (int i = 0; i < DebounceTicks; i++)
            {
                SetSpeed(2f);
                Assert.IsTrue(_pb.IsVehicleMoving());
            }

            // A real, sudden stop must be recognised on the next call, same as the start case.
            SetSpeed(0f);
            Assert.IsFalse(_pb.IsVehicleMoving(),
                "A genuine stop, once outside the post-flip cooldown, must flip immediately");
        }

        [Test]
        public void SpeedOscillatingOnBandEdge_FlipRateIsBoundedByTheCooldown()
        {
            SetSpeed(2f);
            Assert.IsTrue(_pb.IsVehicleMoving());

            // The live bug: speed dwells right around the moving/idle line, so nearly every call
            // disagrees with the current result - worst case for a naive check, every call flips.
            // Alternate clearly past BOTH sides of the hysteresis band (not just a narrow wobble)
            // so every single call would flip without the cooldown; the cooldown must still cap it
            // to roughly one flip per DebounceTicks calls, matching the live-reported Grounded<->Idle
            // flapping every 20-100ms being reduced to a bounded rate instead of every tick.
            const int totalCalls = 40;
            int flips = 0;
            bool last = WasMoving();
            for (int i = 0; i < totalCalls; i++)
            {
                SetSpeed(i % 2 == 0 ? 0f : 2f);
                bool now = _pb.IsVehicleMoving();
                if (now != last) flips++;
                last = now;
            }

            int maxAllowedFlips = (totalCalls / DebounceTicks) + 1;
            Assert.LessOrEqual(flips, maxAllowedFlips,
                $"Edge-dwelling noise flipped {flips} times in {totalCalls} calls - the cooldown must " +
                $"cap this to roughly one flip per {DebounceTicks} calls (this is the live-reported " +
                "Grounded<->Idle flapping every 20-100ms), not let every call flip like the pre-fix code.");
        }

        [Test]
        public void FlipFollowedByOppositeNoise_DoesNotReverseWithinTheCooldownWindow()
        {
            SetSpeed(2f);
            Assert.IsTrue(_pb.IsVehicleMoving(), "Sets up the initial flip to moving");

            // Immediately after flipping, noise pointing the other way must not reverse it before
            // the cooldown elapses - this is what stops a boundary value from chattering right back
            // on the very next call.
            for (int i = 0; i < DebounceTicks - 1; i++)
            {
                SetSpeed(0f);
                Assert.IsTrue(_pb.IsVehicleMoving(),
                    $"Reversed on call {i + 1} after the flip, inside the {DebounceTicks}-call cooldown window");
            }
        }
    }
}
