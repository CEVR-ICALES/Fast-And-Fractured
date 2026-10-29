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
