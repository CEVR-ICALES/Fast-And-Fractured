using NUnit.Framework;
using FastAndFractured.Multiplayer;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>
    /// Covers the match win-condition bookkeeping that previously lived inline in
    /// NetworkedLevelControllerManager and failed silently (issue: one player dies in a
    /// 2-player match and the other never reaches the win screen; server stays open).
    /// </summary>
    public class MatchDeathTrackerTests
    {
        private MatchDeathTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _tracker = new MatchDeathTracker();
        }

        [Test]
        public void Only_AI_left_once_every_human_is_dead()
        {
            // The host may skip the rest of the match once no human is alive (feedback 2026-09-30).
            _tracker.RegisterCharacter(10, ownerClientId: 0);
            _tracker.RegisterCharacter(20, ownerClientId: 1);
            _tracker.RegisterCharacter(30);
            _tracker.RegisterCharacter(40);

            _tracker.ReportDeath(10);
            Assert.IsTrue(_tracker.AnyPlayerAlive, "one human is still playing");

            _tracker.ReportDeath(20);
            Assert.IsFalse(_tracker.AnyPlayerAlive);
            Assert.AreEqual(2, _tracker.AliveCount, "two AIs are still fighting");
        }

        [Test]
        public void TwoPlayers_OneDies_OtherWins()
        {
            _tracker.RegisterCharacter(10, ownerClientId: 0); // host
            _tracker.RegisterCharacter(20, ownerClientId: 1); // remote client

            var result = _tracker.ReportDeath(20);

            Assert.IsTrue(result.Applied);
            Assert.AreEqual(MatchDeathTracker.Outcome.Winner, result.MatchOutcome);
            Assert.AreEqual(10, result.WinnerObjectId, "Survivor must be reported as winner");
            Assert.AreEqual(1, _tracker.AliveCount);
        }

        [Test]
        public void ThreeCharacters_FirstDeathContinues_SecondEndsMatch()
        {
            _tracker.RegisterCharacter(10, ownerClientId: 0);
            _tracker.RegisterCharacter(20, ownerClientId: 1);
            _tracker.RegisterCharacter(30); // AI

            Assert.AreEqual(MatchDeathTracker.Outcome.None, _tracker.ReportDeath(30).MatchOutcome);
            var final = _tracker.ReportDeath(20);
            Assert.AreEqual(MatchDeathTracker.Outcome.Winner, final.MatchOutcome);
            Assert.AreEqual(10, final.WinnerObjectId);
        }

        [Test]
        public void DuplicateDeath_IsNotAppliedAndDoesNotDoubleDecrement()
        {
            _tracker.RegisterCharacter(10);
            _tracker.RegisterCharacter(20);
            _tracker.RegisterCharacter(30);

            Assert.IsTrue(_tracker.ReportDeath(30).Applied);
            var duplicate = _tracker.ReportDeath(30);

            Assert.IsFalse(duplicate.Applied, "Second report of the same death must be ignored");
            Assert.AreEqual(2, _tracker.AliveCount, "Alive count must not double-decrement");
            Assert.AreEqual(MatchDeathTracker.Outcome.None, duplicate.MatchOutcome);
        }

        [Test]
        public void UnknownVictim_IsNotApplied()
        {
            _tracker.RegisterCharacter(10);
            _tracker.RegisterCharacter(20);

            var result = _tracker.ReportDeath(999);

            Assert.IsFalse(result.Applied,
                "A death reported through an untracked NetworkObject id must not mutate state " +
                "(the caller resolves parent/child/owner ids and logs an error)");
            Assert.AreEqual(2, _tracker.AliveCount);
            Assert.IsFalse(_tracker.IsKnown(999));
        }

        [Test]
        public void AllDead_IsDraw()
        {
            _tracker.RegisterCharacter(10);
            _tracker.RegisterCharacter(20);

            _tracker.ReportDeath(10);
            // AliveCount is now 1 → Winner outcome fired for 20. If 20 then also dies
            // (e.g. simultaneous kill processed after), it must resolve as a draw.
            var result = _tracker.ReportDeath(20);

            Assert.IsTrue(result.Applied);
            Assert.AreEqual(MatchDeathTracker.Outcome.Draw, result.MatchOutcome);
            Assert.AreEqual(-1, result.WinnerObjectId);
            Assert.AreEqual(0, _tracker.AliveCount);
            Assert.AreEqual(-1, _tracker.FirstAliveObjectId());
        }

        [Test]
        public void DisconnectFlow_ClientMapsToCharacter_AndDeathEndsMatch()
        {
            _tracker.RegisterCharacter(10, ownerClientId: 0);
            _tracker.RegisterCharacter(20, ownerClientId: 5);

            Assert.IsTrue(_tracker.TryGetCharacterForClient(5, out int characterId));
            Assert.AreEqual(20, characterId);

            _tracker.ForgetClient(5);
            Assert.IsFalse(_tracker.TryGetCharacterForClient(5, out _),
                "Mapping must be dropped so a reconnect can't be mis-mapped");

            // Character tracking survives the mapping removal — the death still counts.
            var result = _tracker.ReportDeath(characterId);
            Assert.AreEqual(MatchDeathTracker.Outcome.Winner, result.MatchOutcome);
            Assert.AreEqual(10, result.WinnerObjectId);
        }

        [Test]
        public void ReRegistering_SameCharacter_IsIdempotent()
        {
            _tracker.RegisterCharacter(10, ownerClientId: 0);
            _tracker.RegisterCharacter(10, ownerClientId: 0);

            Assert.AreEqual(1, _tracker.AliveCount);
            Assert.AreEqual(1, _tracker.TrackedCount);
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            _tracker.RegisterCharacter(10, ownerClientId: 0);
            _tracker.RegisterCharacter(20);
            _tracker.ReportDeath(20);

            _tracker.Reset();

            Assert.AreEqual(0, _tracker.AliveCount);
            Assert.AreEqual(0, _tracker.TrackedCount);
            Assert.IsFalse(_tracker.IsKnown(10));
            Assert.IsFalse(_tracker.TryGetCharacterForClient(0, out _));
        }
    }
}
