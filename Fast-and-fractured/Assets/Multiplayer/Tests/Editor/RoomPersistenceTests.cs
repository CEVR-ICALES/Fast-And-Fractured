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
        [TestCase(Enums.ScreensType.GAMEMODE_SELECTION, true, Lobby.MultiplayerMenuRouter.GuardAction.ReturnToRoom, TestName = "Backing_out_of_the_character_screen_returns_to_the_room")]
        [TestCase(Enums.ScreensType.MULTIPLAYER_BROWSER, true, Lobby.MultiplayerMenuRouter.GuardAction.ReturnToRoom, TestName = "A_connected_player_cannot_reach_the_room_browser")]
        [TestCase(Enums.ScreensType.CHARACTER_SELECTION, true, Lobby.MultiplayerMenuRouter.GuardAction.None, TestName = "Choosing_a_character_is_part_of_the_room")]
        [TestCase(Enums.ScreensType.MAP_SELECTION, true, Lobby.MultiplayerMenuRouter.GuardAction.None, TestName = "Choosing_a_map_is_part_of_the_room")]
        [TestCase(Enums.ScreensType.LOADING, true, Lobby.MultiplayerMenuRouter.GuardAction.None, TestName = "The_map_button_is_not_interrupted")]
        [TestCase(Enums.ScreensType.MAP_SELECTION, false, Lobby.MultiplayerMenuRouter.GuardAction.EndFlow, TestName = "Losing_the_room_gives_the_map_button_back_to_single_player")]
        public void A_player_in_a_room_stays_in_it_while_choosing_character_and_map(Enums.ScreensType screen, bool connected, Lobby.MultiplayerMenuRouter.GuardAction expected)
        {
            Assert.AreEqual(expected, Lobby.MultiplayerMenuRouter.GuardFor(true, connected, true, screen));
            Assert.AreEqual(Lobby.MultiplayerMenuRouter.GuardAction.None, Lobby.MultiplayerMenuRouter.GuardFor(false, connected, true, screen), "Single player is never touched.");
        }

        [Test]
        public void A_ninth_player_is_turned_away()
        {
            Assert.IsFalse(NetworkedLobbyManager.IsRoomFullFor(NetworkedLobbyManager.MAX_PLAYERS - 1, alreadyInRoom: false));
            Assert.IsTrue(NetworkedLobbyManager.IsRoomFullFor(NetworkedLobbyManager.MAX_PLAYERS, alreadyInRoom: false));
            Assert.IsFalse(NetworkedLobbyManager.IsRoomFullFor(NetworkedLobbyManager.MAX_PLAYERS, alreadyInRoom: true));
        }

        [TestCase(true, false, false, true, TestName = "Still_in_the_map_after_the_match_ended_goes_back_to_the_room")]
        [TestCase(true, true, false, false, TestName = "Already_in_the_menu_is_left_alone")]
        [TestCase(true, false, true, false, TestName = "A_new_match_already_running_is_left_alone")]
        [TestCase(false, false, false, false, TestName = "Offline_is_left_alone")]
        public void Nobody_stays_in_a_map_whose_match_is_over(bool online, bool inMenuScene, bool newMatchStarted, bool expected)
        {
            Assert.AreEqual(expected, MultiplayerRoomReturn.IsStranded(online, inMenuScene, newMatchStarted));
        }

        [TestCase(false, true, true, TestName = "A_lost_host_sends_the_player_back")]
        [TestCase(false, false, false, TestName = "A_join_that_never_connected_leaves_the_player_on_the_join_screen")]
        [TestCase(true, true, false, TestName = "Leaving_on_purpose_is_not_reported")]
        public void The_watchdog_only_reports_a_connection_that_existed(bool wasIntentional, bool wasConnected, bool expected)
        {
            Assert.AreEqual(expected, HostDisconnectWatchdog.ShouldReport(wasIntentional, wasConnected));
        }

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

        [Test]
        public void Succession_UnwillingHostsGoLast()
        {
            var order = RoomSuccession.Order(new[] { 5, 2, 7, 3 }, new[] { 7, 3 }, new[] { 3, 2 });
            CollectionAssert.AreEqual(new[] { 7, 5, 3, 2 }, order);
        }

        [Test]
        public void Succession_TheHostsChoiceGoesFirst()
        {
            var order = RoomSuccession.Order(new[] { 5, 2, 7, 3 }, new[] { 7, 3 }, null, chosen: 5);
            CollectionAssert.AreEqual(new[] { 5, 3, 7, 2 }, order);
        }

        [TestCase(false, true, false, true)]
        [TestCase(false, true, true, false)]
        [TestCase(true, true, false, false)]
        [TestCase(false, false, false, false)]
        public void Room_ShowsJoiningUntilTheHostRegistersThePlayer(bool isHost, bool isClient, bool registered, bool expected)
        {
            Assert.AreEqual(expected, LobbyReadyUI.IsJoining(isHost, isClient, registered));
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
