using FastAndFractured.Multiplayer.Progression;
using NUnit.Framework;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>The XP curve and the XP a match is worth.</summary>
    public class ProgressionMathTests
    {
        [TestCase(0, 1)]
        [TestCase(199, 1)]
        [TestCase(200, 2)]
        [TestCase(449, 2)]
        [TestCase(450, 3)]
        [TestCase(-10, 1)]
        public void LevelForTotalXp(int xp, int level) => Assert.AreEqual(level, ProgressionMath.LevelForTotalXp(xp));

        [Test]
        public void TotalXpForLevel_RoundTrips()
        {
            for (int level = 1; level <= ProgressionMath.MAX_LEVEL; level++)
                Assert.AreEqual(level, ProgressionMath.LevelForTotalXp(ProgressionMath.TotalXpForLevel(level)));
        }

        [Test]
        public void Level_CapsAtMax() => Assert.AreEqual(ProgressionMath.MAX_LEVEL, ProgressionMath.LevelForTotalXp(int.MaxValue / 2));

        [Test]
        public void MatchXp_WinnerBeatsLast()
        {
            int first = ProgressionMath.MatchXp(1, 8, 0, 0);
            int last = ProgressionMath.MatchXp(8, 8, 0, 0);
            Assert.AreEqual(50 + 120 + 150, first);
            Assert.AreEqual(50, last);
        }

        [Test]
        public void MatchXp_KillsAndCappedDamage()
        {
            Assert.AreEqual(50 + 2 * 40 + 30, ProgressionMath.MatchXp(4, 4, 2, 350));
            Assert.AreEqual(50 + 100, ProgressionMath.MatchXp(4, 4, 0, 100000));
        }

        [Test]
        public void NextReward_IsTheLowestAboveTheLevel()
        {
            var rewards = new[]
            {
                new ProgressionReward { id = "a", level = 5 },
                null,
                new ProgressionReward { id = "b", level = 3 },
                new ProgressionReward { id = "c", level = 2 },
            };
            Assert.AreEqual("b", XpGainBar.NextReward(rewards, 2).id);
            Assert.AreEqual("c", XpGainBar.NextReward(rewards, 1).id);
            Assert.IsNull(XpGainBar.NextReward(rewards, 5));
        }

        [Test]
        public void MatchXp_Alone() => Assert.AreEqual(50 + 150, ProgressionMath.MatchXp(1, 1, 0, 0));
    }
}
