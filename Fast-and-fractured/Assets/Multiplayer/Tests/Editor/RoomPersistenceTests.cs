using System.Collections.Generic;
using System.Net;
using System.Text;
using FastAndFractured.Multiplayer.Abstractions.Tugboat;
using FastAndFractured.Multiplayer.HostMigration;
using NUnit.Framework;
using UnityEngine.SceneManagement;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>The room outliving the match: who inherits it, the "in a match" advert, who sees the match.</summary>
    public class RoomPersistenceTests
    {
        [Test]
        public void Succession_MatchPlayersFirstThenByConnectionOrder()
        {
            var order = RoomSuccession.Order(new[] { 5, 2, 7, 3 }, new[] { 7, 3 });
            CollectionAssert.AreEqual(new[] { 3, 7, 2, 5 }, order);
        }

        [Test]
        public void Succession_BetweenMatches_ByConnectionOrder()
        {
            var order = RoomSuccession.Order(new[] { 4, 1, 2 }, null);
            CollectionAssert.AreEqual(new[] { 1, 2, 4 }, order);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void LanAdvert_CarriesInMatch(bool inMatch)
        {
            string payload = TugboatLobbyTransport.BuildPayload("Sala", 7777, 3, "1.69", 8, LinkKind.Wifi, inMatch);
            var listing = TugboatLobbyTransport.ParsePayload(Encoding.UTF8.GetBytes(payload), new IPEndPoint(IPAddress.Loopback, 47777));
            Assert.IsNotNull(listing);
            Assert.AreEqual(inMatch, listing.InMatch);
            Assert.AreEqual("Sala", listing.RoomName);
            Assert.AreEqual(LinkKind.Wifi, listing.HostLink);
        }

        [Test]
        public void LanAdvert_FromOlderHost_IsNotInMatch()
        {
            var listing = TugboatLobbyTransport.ParsePayload(Encoding.UTF8.GetBytes("Sala|7777|2|1.68|8|1"), new IPEndPoint(IPAddress.Loopback, 47777));
            Assert.IsNotNull(listing);
            Assert.IsFalse(listing.InMatch);
        }

        [Test]
        public void Visibility_ObjectsOutsideALoadedScene_AreSeenByEveryone() =>
            Assert.IsTrue(MatchSceneVisibility.IsVisible(default(Scene), new HashSet<Scene>()));
    }
}
