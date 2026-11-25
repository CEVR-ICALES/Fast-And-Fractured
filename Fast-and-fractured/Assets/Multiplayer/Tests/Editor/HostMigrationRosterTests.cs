using FastAndFractured.Multiplayer.HostMigration;
using NUnit.Framework;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>Who comes back after a host migration: reconnected humans keep their car, surviving AIs stay.</summary>
    public class HostMigrationRosterTests
    {
        // Original start: 3 humans (host A, B, C) + 3 AIs = 6 characters.
        private static MatchSnapshotBroadcast Snapshot(params int[] deadIndices)
        {
            var characters = new SnapshotCharacter[6];
            for (int i = 0; i < 6; i++)
                characters[i] = new SnapshotCharacter { OriginalIndex = i, Alive = System.Array.IndexOf(deadIndices, i) < 0 };
            return new MatchSnapshotBroadcast
            {
                MigrationEnabled = true,
                TotalCharacters = 6,
                HumanTokens = new[] { "A", "B", "C" },
                HumanCodes = new[] { "Pepe_0", "Carme_0", "Josefino_0" },
                SuccessorTokens = new[] { "B", "C" },
                SuccessorAddresses = new[] { "10.0.0.2", "10.0.0.3" },
                Characters = characters,
            };
        }

        [TestCase(HostMigrationNetwork.CLIENT_TIMEOUT_SECONDS + 0.5f, true, TestName = "A_crashed_host_is_still_migrated_after_the_direct_timeout")]
        [TestCase(10.5f, true, TestName = "A_crashed_host_is_still_migrated_after_the_Steam_relay_timeout")]
        [TestCase(0.2f, true, TestName = "A_host_that_closes_its_game_is_migrated_at_once")]
        [TestCase(60f, false, TestName = "A_snapshot_from_long_ago_is_not_resumed")]
        public void The_last_snapshot_outlives_the_time_it_takes_to_notice_the_host_is_gone(float snapshotAge, bool expected)
        {
            Assert.AreEqual(expected, HostMigrationNetwork.IsSnapshotUsable(snapshotAge));
        }

        [Test]
        public void A_host_that_leaves_hands_the_match_over_only_when_somebody_can_take_it()
        {
            Assert.IsTrue(HostMigrationNetwork.CanCarryOnWithout(Snapshot()));

            var alone = Snapshot();
            alone.SuccessorTokens = new string[0];
            alone.SuccessorAddresses = new string[0];
            Assert.IsFalse(HostMigrationNetwork.CanCarryOnWithout(alone), "Only bots left: the match ends as before.");

            var off = Snapshot();
            off.MigrationEnabled = false;
            Assert.IsFalse(HostMigrationNetwork.CanCarryOnWithout(off));
        }

        [Test]
        public void A_host_leaving_a_match_still_ends_it_unless_the_option_is_switched_on()
        {
            var fresh = UnityEngine.ScriptableObject.CreateInstance<HostMigrationConfig>();
            try { Assert.IsFalse(fresh.ContinueWhenHostLeaves); }
            finally { UnityEngine.Object.DestroyImmediate(fresh); }

            var shipped = UnityEngine.Resources.Load<HostMigrationConfig>(HostMigrationSettings.CONFIG_RESOURCES_PATH);
            Assert.IsNotNull(shipped);
            Assert.IsTrue(shipped.Enabled, "A lost host is migrated.");
            Assert.IsFalse(shipped.ContinueWhenHostLeaves, "Only tested between editors on a LAN: off for the playtest.");
        }

        [Test]
        public void A_server_drops_a_vanished_player_within_a_minute_or_so()
        {
            Assert.LessOrEqual(MultiplayerTransport.SERVER_TIMEOUT_SECONDS, 90, "Tugboat's default keeps the ghost for 30 minutes.");
            Assert.Greater(MultiplayerTransport.SERVER_TIMEOUT_SECONDS, 20, "A slow machine loading a map must not be dropped.");
        }

        [Test]
        public void The_first_remaining_human_takes_over()
        {
            Assert.IsTrue(HostMigrationRoster.IsSuccessor(Snapshot(), "B"));
            Assert.IsFalse(HostMigrationRoster.IsSuccessor(Snapshot(), "C"));
            Assert.IsFalse(HostMigrationRoster.IsSuccessor(Snapshot(), "A"), "the host that left never takes over");
        }

        [Test]
        public void Reconnected_humans_get_their_own_car_back()
        {
            // Connections on the new host in any order: C then B.
            var plan = HostMigrationRoster.Build(Snapshot(), new[] { "C", "B" });
            Assert.AreEqual(new[] { 2, 1 }, plan.HumanCharacterIndex);
        }

        [Test]
        public void The_departed_host_and_the_dead_are_left_out()
        {
            // AI #4 had died; the host (A, index 0) is gone.
            var plan = HostMigrationRoster.Build(Snapshot(4), new[] { "B", "C" });
            Assert.AreEqual(new[] { 1, 2 }, plan.HumanCharacterIndex);
            Assert.AreEqual(new[] { 3, 5 }, plan.AiCharacterIndices.ToArray());
        }

        [Test]
        public void A_dead_or_unknown_player_spectates()
        {
            var plan = HostMigrationRoster.Build(Snapshot(2), new[] { "B", "C", "stranger" });
            Assert.AreEqual(new[] { 1, -1, -1 }, plan.HumanCharacterIndex);
        }
    }
}
