using System.Net;
using System.Text;
using FastAndFractured.Multiplayer.Abstractions;
using FastAndFractured.Multiplayer.Abstractions.Tugboat;
using NUnit.Framework;

namespace FastAndFractured.Multiplayer.Tests
{
    public class RoomSeatsTests
    {
        [TestCase(0, 0, 3, 0, 3)]
        [TestCase(6, 1, 2, 1, 1)]
        [TestCase(7, 1, 1, 1, 0)]
        [TestCase(8, 2, 1, 0, 0)]
        public void ArrivingPlayerTakesAReservedSeatBeforeABot(int humans, int ai, int reserved, int expectedAi, int expectedReserved)
        {
            NetworkedLobbyManager.FitSeats(humans, ref ai, ref reserved);
            Assert.AreEqual(expectedAi, ai);
            Assert.AreEqual(expectedReserved, reserved);
        }

        [Test]
        public void BrowserLeavesReservedSeatsToGuests()
        {
            var listing = new LobbyListing { CurrentPlayers = 5, MaxPlayers = 8, ReservedSlots = 3 };
            Assert.AreEqual(0, listing.OpenSlots);
            listing.ReservedSlots = 1;
            Assert.AreEqual(2, listing.OpenSlots);
        }

        [Test]
        public void ReservedSeatsSurviveTheLanAnnouncement()
        {
            string payload = TugboatLobbyTransport.BuildPayload("Sala", 7777, 3, "1.71", 8, LinkKind.Wifi, false, 2);
            var listing = TugboatLobbyTransport.ParsePayload(Encoding.UTF8.GetBytes(payload), new IPEndPoint(IPAddress.Loopback, 47777));
            Assert.AreEqual(2, listing.ReservedSlots);
        }

        [Test]
        public void OlderAnnouncementHasNoReservedSeats()
        {
            var listing = TugboatLobbyTransport.ParsePayload(Encoding.UTF8.GetBytes("Sala|7777|2|1.68|8|1|0"), new IPEndPoint(IPAddress.Loopback, 47777));
            Assert.AreEqual(0, listing.ReservedSlots);
        }
    }
}
