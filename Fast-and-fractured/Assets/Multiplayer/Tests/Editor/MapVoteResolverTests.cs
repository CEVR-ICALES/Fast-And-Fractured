using System;
using System.Collections.Generic;
using NUnit.Framework;
using FastAndFractured.Multiplayer.Lobby;

namespace FastAndFractured.Multiplayer.Tests
{
    [TestFixture]
    public class MapVoteResolverTests
    {
        private static readonly List<int> ThreeMaps = new List<int> { 0, 1, 2 };
        private static readonly Random SeededRng = new Random(42);

        [Test]
        public void SingleVote_WinsOutright()
        {
            var votes = new Dictionary<int, int> { { 0, 1 } }; // clientId 0 voted for map 1
            int result = MapVoteResolver.Resolve(votes, ThreeMaps, new Random(0));
            Assert.AreEqual(1, result);
        }

        [Test]
        public void MajorityVote_WinsOutright()
        {
            var votes = new Dictionary<int, int>
            {
                { 0, 2 }, { 1, 2 }, { 2, 0 }
            };
            int result = MapVoteResolver.Resolve(votes, ThreeMaps, new Random(0));
            Assert.AreEqual(2, result);
        }

        [Test]
        public void TieBreak_ReturnsOneOfTheTiedMaps()
        {
            var votes = new Dictionary<int, int>
            {
                { 0, 0 }, { 1, 1 }
            };
            int result = MapVoteResolver.Resolve(votes, ThreeMaps, new Random(0));
            Assert.IsTrue(result == 0 || result == 1, $"Expected 0 or 1, got {result}");
        }

        [Test]
        public void TieBreak_IsDeterministicForSameSeed()
        {
            var votes = new Dictionary<int, int> { { 0, 0 }, { 1, 1 } };
            int r1 = MapVoteResolver.Resolve(votes, ThreeMaps, new Random(42));
            int r2 = MapVoteResolver.Resolve(votes, ThreeMaps, new Random(42));
            Assert.AreEqual(r1, r2);
        }

        [Test]
        public void NoVotes_ReturnsAnyValidMap()
        {
            var votes  = new Dictionary<int, int>();
            int result = MapVoteResolver.Resolve(votes, ThreeMaps, new Random(0));
            Assert.IsTrue(ThreeMaps.Contains(result));
        }

        [Test]
        public void VoteForInvalidMapId_IsIgnored()
        {
            var votes = new Dictionary<int, int>
            {
                { 0, 99 },  // 99 not in valid list
                { 1, 1  }
            };
            int result = MapVoteResolver.Resolve(votes, ThreeMaps, new Random(0));
            Assert.AreEqual(1, result, "Only valid vote (map 1) should win.");
        }

        [Test]
        public void AllVotesSameMap_WinsByMajority()
        {
            var votes = new Dictionary<int, int>
            {
                { 0, 2 }, { 1, 2 }, { 2, 2 }, { 3, 2 }
            };
            int result = MapVoteResolver.Resolve(votes, ThreeMaps, new Random(0));
            Assert.AreEqual(2, result);
        }

        [Test]
        public void AllDifferentVotes_TieResolvesToOneOfThem()
        {
            var votes = new Dictionary<int, int> { { 0, 0 }, { 1, 1 }, { 2, 2 } };
            int result = MapVoteResolver.Resolve(votes, ThreeMaps, new Random(0));
            Assert.IsTrue(ThreeMaps.Contains(result));
        }

        [Test]
        public void EmptyValidIds_ThrowsArgumentException()
        {
            var votes = new Dictionary<int, int>();
            Assert.Throws<ArgumentException>(() =>
                MapVoteResolver.Resolve(votes, new List<int>(), new Random(0)));
        }

        [Test]
        public void NullRng_ThrowsArgumentNullException()
        {
            var votes = new Dictionary<int, int>();
            Assert.Throws<ArgumentNullException>(() =>
                MapVoteResolver.Resolve(votes, ThreeMaps, null));
        }
    }
}
