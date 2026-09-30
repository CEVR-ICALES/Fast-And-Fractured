using NUnit.Framework;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>
    /// Cars must not act before the synced "Start" of the countdown (playtest feedback 2026-09-30:
    /// on the host the cars and the AI moved during the 3-2-1).
    /// </summary>
    public class MatchStartGateTests
    {
        [TearDown]
        public void TearDown() => MatchStartGate.Clear();

        [Test]
        public void Cars_are_held_until_the_start_tick()
        {
            MatchStartGate.LockUntil(1000u);
            Assert.IsTrue(MatchStartGate.IsLocked(999u));
            Assert.IsFalse(MatchStartGate.IsLocked(1000u));
            Assert.IsFalse(MatchStartGate.IsLocked(1500u));
        }

        [Test]
        public void Nothing_is_held_outside_a_match()
        {
            MatchStartGate.Clear();
            Assert.IsFalse(MatchStartGate.IsLocked(0u));
            Assert.IsFalse(MatchStartGate.IsLocked(5u));
        }
    }
}
