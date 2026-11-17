using System.Collections.Generic;
using NUnit.Framework;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>End-of-match table: placement from the order of deaths, kills, who eliminated whom.</summary>
    public class MatchResultsTests
    {
        private static MatchResults.Participant P(int id, string name, bool human = true, int damage = 0) =>
            new MatchResults.Participant { Id = id, DisplayName = name, CharacterCode = name + "_0", IsHuman = human, OwnerClientId = human ? id : -1, DamageDealt = damage };

        private static MatchResults.Death D(int victim, int killer = -1) =>
            new MatchResults.Death { VictimId = victim, KillerId = killer };

        private static readonly List<MatchResults.Participant> Players = new List<MatchResults.Participant>
        {
            P(1, "Pepe", damage: 300), P(2, "Carme", damage: 950), P(3, "Josefino", damage: 120), P(4, "Bot", human: false, damage: 40),
        };

        [Test]
        public void The_winner_is_first_and_the_first_to_die_is_last()
        {
            var table = MatchResults.Build(Players, new[] { D(4, 2), D(1, 2), D(3) }, winnerId: 2);

            Assert.AreEqual(new[] { "Carme", "Josefino", "Pepe", "Bot" }, System.Array.ConvertAll(table, e => e.DisplayName));
            Assert.AreEqual(new[] { 1, 2, 3, 4 }, System.Array.ConvertAll(table, e => e.Placement));
            Assert.IsTrue(table[0].Survived);
            Assert.IsFalse(table[1].Survived);
        }

        [Test]
        public void Kills_and_the_eliminator_are_credited()
        {
            var table = MatchResults.Build(Players, new[] { D(4, 2), D(1, 2), D(3) }, winnerId: 2);

            Assert.AreEqual(2, table[0].Kills);
            Assert.AreEqual(950, table[0].DamageDealt);
            Assert.AreEqual("Carme", table[2].EliminatedBy);
            Assert.AreEqual(string.Empty, table[1].EliminatedBy, "the storm or a fall credits nobody");
        }

        [Test]
        public void Players_still_alive_when_the_match_ends_rank_above_the_dead()
        {
            // The host skipped an AI-only ending: two AIs alive, one declared the winner.
            var cast = new List<MatchResults.Participant> { P(1, "Pepe"), P(2, "Bot A", human: false), P(3, "Bot B", human: false) };
            var table = MatchResults.Build(cast, new[] { D(1, 3) }, winnerId: 3);

            Assert.AreEqual(new[] { "Bot B", "Bot A", "Pepe" }, System.Array.ConvertAll(table, e => e.DisplayName));
        }

        [Test]
        public void The_local_player_finds_their_own_row()
        {
            MatchResults.Store(MatchResults.Build(Players, new[] { D(4, 2), D(1, 2), D(3) }, 2), localClientId: 1);
            try
            {
                Assert.IsTrue(MatchResults.TryGetLocal(out var mine));
                Assert.AreEqual(3, mine.Placement);
                Assert.AreEqual("Carme", mine.EliminatedBy);
            }
            finally { MatchResults.Clear(); }
        }

        [Test]
        public void Character_name_drops_the_skin_suffix()
        {
            Assert.AreEqual("Josefino", MatchResults.CharacterName("Josefino_0"));
            Assert.AreEqual(string.Empty, MatchResults.CharacterName(null));
        }
    }
}
