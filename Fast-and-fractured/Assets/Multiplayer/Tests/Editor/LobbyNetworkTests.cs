using System.Net;
using System.Text;
using FastAndFractured.Multiplayer.Abstractions;
using FastAndFractured.Multiplayer.Abstractions.Tugboat;
using FastAndFractured.Multiplayer.Lobby;
using NUnit.Framework;

namespace FastAndFractured.Multiplayer.Tests
{
    public class LobbyNetworkTests
    {
        private const string VERSION = "1.71.8";

        [Test]
        public void Network_FallsBackToLanWhenSteamIsNotRunning()
        {
            Assert.AreEqual(LobbyNetworkMode.Steam, LobbyNetwork.Resolve(LobbyNetworkMode.Steam, steamAvailable: true));
            Assert.AreEqual(LobbyNetworkMode.Lan, LobbyNetwork.Resolve(LobbyNetworkMode.Steam, steamAvailable: false),
                "A Steam Deck outside Steam still has to create and find rooms.");
        }

        [Test]
        public void Network_KeepsLanWhenThePlayerPickedItWithSteamRunning()
        {
            Assert.AreEqual(LobbyNetworkMode.Lan, LobbyNetwork.Resolve(LobbyNetworkMode.Lan, steamAvailable: true));
            Assert.AreEqual(LobbyNetworkMode.Lan, LobbyNetwork.Other(LobbyNetworkMode.Steam));
            Assert.AreEqual(LobbyNetworkMode.Steam, LobbyNetwork.Other(LobbyNetworkMode.Lan));
        }

        [Test]
        public void Hub_SaysWhyLanIsTheOnlyChoice()
        {
            Assert.AreEqual(MultiplayerHubController.KEY_STEAM, MultiplayerHubController.NetworkLabelKey(LobbyNetworkMode.Steam, true));
            Assert.AreEqual(MultiplayerHubController.KEY_LAN, MultiplayerHubController.NetworkLabelKey(LobbyNetworkMode.Lan, true));
            Assert.AreEqual(MultiplayerHubController.KEY_LAN_ONLY, MultiplayerHubController.NetworkLabelKey(LobbyNetworkMode.Lan, false));
        }

        [Test]
        public void LanRooms_OfAnotherVersionAreNotListed()
        {
            LobbyListing room = Broadcast(VERSION);
            Assert.IsTrue(TugboatLobbyTransport.IsSameVersion(room, new LobbyFilter { GameVersion = VERSION }));
            Assert.IsFalse(TugboatLobbyTransport.IsSameVersion(room, new LobbyFilter { GameVersion = "1.71.7" }));
            Assert.IsTrue(TugboatLobbyTransport.IsSameVersion(room, null), "No filter, no version check.");
        }

        [Test]
        public void LanNames_UseTheSteamNameThenTheAccountThenATaggedPlayer()
        {
            Assert.AreEqual("Hugo", TugboatPlayerIdentityProvider.LocalName("Hugo", "evil", "ab12cd34"));
            Assert.AreEqual("evil", TugboatPlayerIdentityProvider.LocalName(null, "evil", "ab12cd34"));
            Assert.AreEqual("Player-ab12", TugboatPlayerIdentityProvider.LocalName(null, "steamuser", "ab12cd34"),
                "Every Deck logs in as steamuser: the name has to tell them apart.");
            Assert.AreEqual("Player-ab12", TugboatPlayerIdentityProvider.LocalName(string.Empty, "DECK", "ab12cd34"));
        }

        [Test]
        public void LanRoom_ShowsTheHostAddressForDirectJoin()
        {
            Assert.AreEqual("Sala (192.168.1.20:7777)", LobbyReadyUI.RoomTitle("Sala", "192.168.1.20:7777"));
            Assert.AreEqual("Sala", LobbyReadyUI.RoomTitle("Sala", null), "Through Steam nobody needs an address.");
        }

        private static LobbyListing Broadcast(string version)
        {
            string payload = TugboatLobbyTransport.BuildPayload("Room", 7777, 2, version, 8, LinkKind.Unknown, false);
            return TugboatLobbyTransport.ParsePayload(Encoding.UTF8.GetBytes(payload), new IPEndPoint(IPAddress.Loopback, 47777));
        }
    }
}
