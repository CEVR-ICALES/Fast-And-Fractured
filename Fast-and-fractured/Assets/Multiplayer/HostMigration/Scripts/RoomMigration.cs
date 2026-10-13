using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Enums;
using FastAndFractured.Multiplayer.Abstractions;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured.Multiplayer.HostMigration
{
    /// <summary>
    /// Server → clients, once a second: what the players need to rebuild the room if the host
    /// goes away - its name and AI slots, and who takes over in order, with their addresses.
    /// </summary>
    public struct RoomSnapshotBroadcast : IBroadcast
    {
        public bool Enabled;
        public string RoomName;
        public int AiSlots;
        public ushort Port;
        /// <summary>A match is running and match migration is on: the match's own migration takes over.</summary>
        public bool MatchMigrationTakesOver;
        public string[] SuccessorTokens;
        public string[] SuccessorAddresses;
    }

    /// <summary>
    /// Room host migration: when the host leaves the room (or crashes), the room carries on
    /// under another player instead of sending everyone back to the hub. The first successor
    /// opens a room with the same name, port and AI slots; the others reconnect to it and
    /// land in it again (their character choice comes back with them, it is saved locally).
    ///
    /// Also covers a host lost during a match when match migration is off: the match is over,
    /// but everyone meets again in the room under the new host.
    ///
    /// Same mechanism as the match migration (HostMigrationNetwork): Tugboat to the successor's
    /// address, players identified by their migration token. Switch: HostMigrationConfig ▸
    /// Room Migration (on by default).
    /// </summary>
    public sealed class RoomMigration : MonoBehaviour
    {
        private const float SEND_INTERVAL = 1f;
        private const float MAX_SNAPSHOT_AGE = 6f;
        private const float RECONNECT_TIMEOUT = 15f;
        private const float SUCCESSOR_HEAD_START = 0.75f;

        public static bool Migrating { get; private set; }

        private static RoomSnapshotBroadcast? _latest;
        private static float _latestTime;
        private static RoomMigration _instance;

        private NetworkManager _hooked;
        private float _nextSend;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Migrating = false;
            _latest = null;
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[RoomMigration]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RoomMigration>();
        }

        private void Update()
        {
            var nm = InstanceFinder.NetworkManager;
            if (nm == null) return;
            if (nm != _hooked)
            {
                _hooked = nm;
                nm.ClientManager.RegisterBroadcast<RoomSnapshotBroadcast>(OnSnapshotReceived);
            }

            if (!nm.IsServerStarted || Time.unscaledTime < _nextSend) return;
            _nextSend = Time.unscaledTime + SEND_INTERVAL;
            var lobby = LobbyLocator.GetLobbyManager();
            if (lobby == null || !lobby.IsServerInitialized) return;
            nm.ServerManager.Broadcast(BuildSnapshot(nm, lobby), requireAuthenticated: true);
        }

        private static RoomSnapshotBroadcast BuildSnapshot(NetworkManager nm, NetworkedLobbyManager lobby)
        {
            var remotes = new List<NetworkConnection>();
            foreach (var conn in nm.ServerManager.Clients.Values)
                if (conn != null && conn.IsActive && !conn.IsLocalClient)
                    remotes.Add(conn);
            var order = RoomSuccession.Order(remotes.ConvertAll(c => c.ClientId), lobby.MatchParticipants, HostMigrationNetwork.UnwillingToHost);

            var tokens = new List<string>(order.Count);
            var addresses = new List<string>(order.Count);
            foreach (int clientId in order)
            {
                if (!nm.ServerManager.Clients.TryGetValue(clientId, out var conn)) continue;
                string token = HostMigrationNetwork.TokenOf(conn);
                if (string.IsNullOrEmpty(token)) continue;
                tokens.Add(token);
                addresses.Add(conn.GetAddress());
            }

            return new RoomSnapshotBroadcast
            {
                Enabled = HostMigrationSettings.RoomMigrationEnabled,
                RoomName = lobby.RoomName.Value,
                AiSlots = lobby.AiSlotCount.Value,
                Port = nm.TransportManager.Transport != null ? nm.TransportManager.Transport.GetPort() : (ushort)7777,
                MatchMigrationTakesOver = lobby.GameStarted.Value && HostMigrationSettings.Enabled,
                SuccessorTokens = tokens.ToArray(),
                SuccessorAddresses = addresses.ToArray(),
            };
        }

        private static void OnSnapshotReceived(RoomSnapshotBroadcast snapshot, Channel channel)
        {
            _latest = snapshot;
            _latestTime = Time.unscaledTime;
        }

        // ── Host lost ──────────────────────────────────────────────────────────────────
        /// <summary>
        /// Called by HostDisconnectWatchdog when the connection to the host dropped without the
        /// local player asking for it. True when this peer rebuilds / rejoins the room.
        /// </summary>
        public static bool TryHandleHostLoss()
        {
            if (Migrating || _instance == null || InstanceFinder.IsServerStarted) return false;
            if (!_latest.HasValue || Time.unscaledTime - _latestTime > MAX_SNAPSHOT_AGE) return false;
            var snapshot = _latest.Value;
            if (!snapshot.Enabled || snapshot.MatchMigrationTakesOver) return false;

            int index = snapshot.SuccessorTokens != null
                ? Array.IndexOf(snapshot.SuccessorTokens, HostMigrationNetwork.LocalToken) : -1;
            if (index < 0) return false;   // joined a moment ago: not in the list yet

            Migrating = true;
            _latest = null;
            _instance.StartCoroutine(index == 0 ? _instance.BecomeHost(snapshot) : _instance.Rejoin(snapshot));
            return true;
        }

        private IEnumerator BecomeHost(RoomSnapshotBroadcast snapshot)
        {
            MpCanary.Mark($"RoomMigration: the host left, this peer opens '{snapshot.RoomName}' on port {snapshot.Port}");
            ConnectionLostNotifier.Show("The host left - you are the new host of the room.");
            yield return BackToMenu();
            yield return WaitFor(LobbyServiceLocator.Transport.LeaveLobbyAsync());

            var create = LobbyServiceLocator.Transport.CreateLobbyAsync(new LobbyCreateOptions
            {
                RoomName = snapshot.RoomName,
                Port = snapshot.Port,
                MaxPlayers = NetworkedLobbyManager.MAX_PLAYERS,
                IsPublic = true,
            });
            yield return WaitFor(create);
            if (create.IsFaulted || create.IsCanceled)
            {
                Fail($"could not open the room ({create.Exception?.GetBaseException().Message})");
                yield break;
            }

            NetworkedLobbyManager lobby = null;
            for (float waited = 0f; waited < 5f; waited += Time.unscaledDeltaTime)
            {
                lobby = LobbyLocator.GetLobbyManager();
                if (lobby != null && lobby.IsServerInitialized) break;
                yield return null;
            }
            if (lobby != null && lobby.IsServerInitialized)
            {
                lobby.ApplyRoomName(snapshot.RoomName);
                lobby.AiSlotCount.Value = Mathf.Clamp(snapshot.AiSlots, 0, NetworkedLobbyManager.MAX_PLAYERS - 1);
            }
            OpenRoom();
            MpCanary.Mark("RoomMigration: room reopened");
            Migrating = false;
        }

        private IEnumerator Rejoin(RoomSnapshotBroadcast snapshot)
        {
            string address = snapshot.SuccessorAddresses[0];
            MpCanary.Mark($"RoomMigration: the host left, rejoining the room at {address}:{snapshot.Port}");
            ConnectionLostNotifier.Show("The host left - joining the room's new host...");
            yield return BackToMenu();
            yield return WaitFor(LobbyServiceLocator.Transport.LeaveLobbyAsync());
            yield return new WaitForSecondsRealtime(SUCCESSOR_HEAD_START);

            var nm = InstanceFinder.NetworkManager;
            var tugboat = nm != null ? nm.TransportManager.Transport as Tugboat : null;
            float deadline = Time.unscaledTime + RECONNECT_TIMEOUT;
            while (nm != null && Time.unscaledTime < deadline)
            {
                if (nm.ClientManager.Started) nm.ClientManager.StopConnection();
                if (tugboat != null) { tugboat.SetClientAddress(address); tugboat.SetPort(snapshot.Port); }
                nm.ClientManager.StartConnection();
                for (float attempt = 0f; attempt < 3f && !nm.ClientManager.Started; attempt += Time.unscaledDeltaTime)
                    yield return null;
                if (nm.ClientManager.Started)
                {
                    OpenRoom();
                    MpCanary.Mark("RoomMigration: back in the room");
                    Migrating = false;
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Fail("the new host did not answer");
        }

        /// <summary>A host lost mid-match leaves the map first (the match is over without it).</summary>
        private static IEnumerator BackToMenu()
        {
            if (SceneManager.GetActiveScene().buildIndex != LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX)
            {
                MultiplayerSceneTeardown.LoadMainMenu();
                for (float waited = 0f; waited < 15f; waited += Time.unscaledDeltaTime)
                {
                    if (SceneManager.GetActiveScene().buildIndex == LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX && MainMenuManager.HasInstance)
                        break;
                    yield return null;
                }
                for (int i = 0; i < 3; i++) yield return null;
                MultiplayerRoomReturn.SkipMenuIntro();
            }
        }

        private static IEnumerator WaitFor(Task task)
        {
            while (task != null && !task.IsCompleted) yield return null;
        }

        private static void OpenRoom()
        {
            if (!MainMenuManager.HasInstance) return;
            var menu = MainMenuManager.Instance;
            if (menu.CurrentScreen == null || menu.CurrentScreen.screenType != ScreensType.LOBBY_ROOM)
                menu.TransitionBetweenScreens(ScreensType.LOBBY_ROOM, 0.25f);
        }

        private static void Fail(string reason)
        {
            MpCanary.Mark($"RoomMigration FAILED: {reason}");
            Migrating = false;
            ConnectionLostNotifier.Show("Could not reach the room's new host - returning to the multiplayer hub.");
            var nm = InstanceFinder.NetworkManager;
            if (nm != null)
            {
                HostDisconnectWatchdog.NotifyIntentionalLeave();
                if (nm.ClientManager.Started) nm.ClientManager.StopConnection();
                if (nm.ServerManager.Started) nm.ServerManager.StopConnection(true);
            }
            if (MainMenuManager.HasInstance)
                MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.MULTIPLAYER_HUB, 0.5f);
        }
    }

    /// <summary>Pure rule of who takes over a room (unit tested).</summary>
    public static class RoomSuccession
    {
        /// <summary>
        /// Remote players in take-over order: those willing to host before those who said no in
        /// their settings (they still take over if nobody else is left); among them the match's
        /// players first (a lost match host then hands the room to someone who was playing), then
        /// the rest, each by connection order.
        /// </summary>
        public static List<int> Order(IEnumerable<int> remoteClientIds, IEnumerable<int> matchParticipants,
                                      IEnumerable<int> unwillingToHost = null)
        {
            var playing = new HashSet<int>(matchParticipants ?? Array.Empty<int>());
            var unwilling = new HashSet<int>(unwillingToHost ?? Array.Empty<int>());
            var order = new List<int>(remoteClientIds);
            order.Sort((a, b) =>
            {
                bool ua = unwilling.Contains(a), ub = unwilling.Contains(b);
                if (ua != ub) return ua ? 1 : -1;
                bool pa = playing.Contains(a), pb = playing.Contains(b);
                return pa != pb ? (pa ? -1 : 1) : a.CompareTo(b);
            });
            return order;
        }
    }
}
