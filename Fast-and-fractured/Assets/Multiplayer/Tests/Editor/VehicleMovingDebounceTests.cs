using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>
    /// Pins PhysicsBehaviour.IsVehicleMoving()'s hysteresis and cooldown: a flip lands at once and then blocks
    /// further flips, unlike a confirmation filter that delays every transition. Fields are driven by reflection.
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
            // A clean transition costs no input lag: it lands on the next call.
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

            // Let the cooldown elapse at a sustained speed.
            for (int i = 0; i < DebounceTicks; i++)
            {
                SetSpeed(2f);
                Assert.IsTrue(_pb.IsVehicleMoving());
            }

            // A sudden stop also lands on the next call.
            SetSpeed(0f);
            Assert.IsFalse(_pb.IsVehicleMoving(),
                "A genuine stop, once outside the post-flip cooldown, must flip immediately");
        }

        [Test]
        public void SpeedOscillatingOnBandEdge_FlipRateIsBoundedByTheCooldown()
        {
            SetSpeed(2f);
            Assert.IsTrue(_pb.IsVehicleMoving());

            // Speed alternating across both sides of the band would flip every call: the cooldown caps it to
            // about one flip per DebounceTicks calls.
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

            // Right after a flip, noise the other way must not reverse it before the cooldown elapses.
            for (int i = 0; i < DebounceTicks - 1; i++)
            {
                SetSpeed(0f);
                Assert.IsTrue(_pb.IsVehicleMoving(),
                    $"Reversed on call {i + 1} after the flip, inside the {DebounceTicks}-call cooldown window");
            }
        }
    }
}
