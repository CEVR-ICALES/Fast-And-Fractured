using System;
using System.Collections.Generic;
using System.Linq;
using FastAndFractured.Multiplayer.Lobby;
using NUnit.Framework;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>
    /// The map pick the room animates before a match: every peer plays the hops the server
    /// sends, and the server waits for their duration before loading the map.
    /// </summary>
    public class MapPickTests
    {
        private static readonly List<int> ThreeMaps = new List<int> { 0, 1, 2 };

        private static Dictionary<int, int> Votes(params int[] mapPerClient)
        {
            var votes = new Dictionary<int, int>();
            for (int client = 0; client < mapPerClient.Length; client++) votes[client] = mapPerClient[client];
            return votes;
        }

        [Test]
        public void Votes_WheelGoesRoundTheVotedMapsAndStopsOnTheWinner()
        {
            var pick = NetworkedLobbyManager.BuildMapPick(Votes(2, 0, 2), ThreeMaps, 2, new Random(1));

            Assert.IsFalse(pick.Random);
            CollectionAssert.AreEqual(new[] { 0, 2 }, pick.Candidates);
            CollectionAssert.AreEqual(new[] { 1, 2 }, pick.Votes);
            Assert.GreaterOrEqual(pick.Hops.Length, 12);
            for (int k = 0; k < pick.Hops.Length; k++) Assert.AreEqual(k % 2, pick.Hops[k], "a wheel, in order");
            Assert.AreEqual(2, pick.Candidates[pick.Hops.Last()]);
        }

        [Test]
        public void OneVotedMap_IsShownStraightAway()
        {
            var pick = NetworkedLobbyManager.BuildMapPick(Votes(1, 1), ThreeMaps, 1, new Random(1));

            CollectionAssert.AreEqual(new[] { 1 }, pick.Candidates);
            CollectionAssert.AreEqual(new[] { 0 }, pick.Hops);
        }

        [Test]
        public void NoVotes_RandomHopsOverEveryMap_NeverTheSameTwiceInARow()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var pick = NetworkedLobbyManager.BuildMapPick(Votes(), ThreeMaps, 1, new Random(seed));

                Assert.IsTrue(pick.Random);
                CollectionAssert.AreEqual(ThreeMaps, pick.Candidates);
                Assert.IsEmpty(pick.Votes);
                Assert.AreEqual(1, pick.Candidates[pick.Hops.Last()]);
                for (int k = 1; k < pick.Hops.Length; k++) Assert.AreNotEqual(pick.Hops[k - 1], pick.Hops[k]);
            }
        }

        [Test]
        public void VotesForUnknownMaps_CountAsNoVotes()
        {
            var pick = NetworkedLobbyManager.BuildMapPick(Votes(7, -1), ThreeMaps, 0, new Random(3));

            Assert.IsTrue(pick.Random);
            Assert.AreEqual(0, pick.Candidates[pick.Hops.Last()]);
        }

        [Test]
        public void Duration_CoversEveryHopAndTheReveal()
        {
            float hops = 0f;
            for (int i = 0; i < 12; i++) hops += MapVoteAnimationUI.HopInterval(i, 12);

            Assert.Greater(MapVoteAnimationUI.DurationFor(12), hops);
            Assert.Less(MapVoteAnimationUI.HopInterval(0, 12), MapVoteAnimationUI.HopInterval(11, 12), "it slows down");
        }
    }
}
