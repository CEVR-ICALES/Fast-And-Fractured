using NUnit.Framework;
using FastAndFractured.Multiplayer.Abstractions;

namespace FastAndFractured.Multiplayer.Tests
{
    [TestFixture]
    public class LobbyTransportContractTests
    {
        [Test]
        public void ILobbyTransport_HasRequiredMethods()
        {
            var t = typeof(ILobbyTransport);
            Assert.IsNotNull(t.GetMethod("CreateLobbyAsync"), "CreateLobbyAsync missing");
            Assert.IsNotNull(t.GetMethod("JoinLobbyAsync"),   "JoinLobbyAsync missing");
            Assert.IsNotNull(t.GetMethod("LeaveLobbyAsync"),  "LeaveLobbyAsync missing");
            Assert.IsNotNull(t.GetMethod("ListLobbiesAsync"), "ListLobbiesAsync missing");
            Assert.IsNotNull(t.GetMethod("SetLobbyMetadata"), "SetLobbyMetadata missing");
            Assert.IsNotNull(t.GetMethod("GetLobbyMetadata"), "GetLobbyMetadata missing");
        }

        [Test]
        public void ClientTarget_SteamIdGoesThroughTheRelay_AddressesThroughTugboat()
        {
            Assert.IsTrue(MultiplayerTransport.IsSteamId("76561198195407945"));
            Assert.IsFalse(MultiplayerTransport.IsSteamId("192.168.1.141"));
            Assert.IsFalse(MultiplayerTransport.IsSteamId("localhost"));
            Assert.IsFalse(MultiplayerTransport.IsSteamId("3232235917"), "an IPv4 address written as a number");
        }

#if STEAMWORKS_NET
        [Test]
        public void SteamLobby_HiddenOnceItsHostStopsBeating()
        {
            const uint NOW = 1_000_000;
            uint stale = Abstractions.Steam.SteamLobbyTransport.STALE_HEARTBEAT_SECONDS;
            Assert.IsFalse(Abstractions.Steam.SteamLobbyTransport.IsHeartbeatStale((NOW - stale).ToString(), NOW));
            Assert.IsTrue(Abstractions.Steam.SteamLobbyTransport.IsHeartbeatStale((NOW - stale - 1).ToString(), NOW));
            Assert.IsFalse(Abstractions.Steam.SteamLobbyTransport.IsHeartbeatStale((NOW + 5).ToString(), NOW));
            Assert.IsFalse(Abstractions.Steam.SteamLobbyTransport.IsHeartbeatStale(string.Empty, NOW));
        }
#endif

        [Test]
        public void ILobbyTransport_HasRequiredEvents()
        {
            var t = typeof(ILobbyTransport);
            Assert.IsNotNull(t.GetEvent("OnLobbyMemberJoined"), "OnLobbyMemberJoined missing");
            Assert.IsNotNull(t.GetEvent("OnLobbyMemberLeft"),   "OnLobbyMemberLeft missing");
            Assert.IsNotNull(t.GetEvent("OnLobbyClosed"),       "OnLobbyClosed missing");
        }

        [Test]
        public void IPlayerIdentityProvider_HasRequiredMethods()
        {
            var t = typeof(IPlayerIdentityProvider);
            Assert.IsNotNull(t.GetMethod("GetLocalIdentityAsync"), "GetLocalIdentityAsync missing");
            Assert.IsNotNull(t.GetMethod("GetIdentityAsync"),      "GetIdentityAsync missing");
        }

        [Test]
        public void IPlayerIdentity_HasRequiredMembers()
        {
            var t = typeof(IPlayerIdentity);
            Assert.IsNotNull(t.GetProperty("DisplayName"), "DisplayName missing");
            Assert.IsNotNull(t.GetProperty("PlatformId"),  "PlatformId missing");
            Assert.IsNotNull(t.GetMethod("GetAvatarAsync"), "GetAvatarAsync missing");
        }

        [Test]
        public void LobbyHandle_FromAddress_ProducesConsistentId()
        {
            var h1 = LobbyHandle.FromAddress("192.168.1.1", 7777);
            var h2 = LobbyHandle.FromAddress("192.168.1.1", 7777);
            Assert.AreEqual(h1.PlatformId, h2.PlatformId);
            Assert.AreEqual(h1.Address,    "192.168.1.1");
            Assert.AreEqual(h1.Port,       (ushort)7777);
        }

        [Test]
        public void LobbyHandle_Equality_WorksCorrectly()
        {
            var a = LobbyHandle.FromAddress("10.0.0.1", 7777);
            var b = LobbyHandle.FromAddress("10.0.0.1", 7777);
            var c = LobbyHandle.FromAddress("10.0.0.2", 7777);
            Assert.IsTrue(a == b);
            Assert.IsFalse(a == c);
        }

        [Test]
        public void LobbyServiceLocator_ConfigureAndReset_Works()
        {
            LobbyServiceLocator.Reset();
            Assert.IsFalse(LobbyServiceLocator.IsConfigured);

            var transport = new FastAndFractured.Multiplayer.Abstractions.Tugboat.TugboatLobbyTransport();
            var identity  = new FastAndFractured.Multiplayer.Abstractions.Tugboat.TugboatPlayerIdentityProvider();
            LobbyServiceLocator.Configure(transport, identity);

            Assert.IsTrue(LobbyServiceLocator.IsConfigured);
            Assert.AreSame(transport, LobbyServiceLocator.Transport);
            Assert.AreSame(identity,  LobbyServiceLocator.IdentityProvider);

            LobbyServiceLocator.Reset();
            Assert.IsFalse(LobbyServiceLocator.IsConfigured);
        }

        [Test]
        public void LobbyServiceLocator_ConfigureWithNullTransport_Throws()
        {
            LobbyServiceLocator.Reset();
            var identity = new FastAndFractured.Multiplayer.Abstractions.Tugboat.TugboatPlayerIdentityProvider();
            Assert.Throws<System.ArgumentNullException>(() =>
                LobbyServiceLocator.Configure(null, identity));
            LobbyServiceLocator.Reset();
        }

        [Test]
        public void LobbyCreateOptions_Defaults_AreCorrect()
        {
            var opts = new LobbyCreateOptions();
            Assert.AreEqual(7777,      opts.Port);
            Assert.AreEqual(8,         opts.MaxPlayers);
            Assert.AreEqual(0,         opts.InitialAiSlots);
            Assert.IsTrue(opts.IsPublic);
        }
    }
}
