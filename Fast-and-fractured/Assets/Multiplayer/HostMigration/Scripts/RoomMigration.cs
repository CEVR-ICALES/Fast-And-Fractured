using Assets.SimpleLocalization.Scripts;
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
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured.Multiplayer.HostMigration
{
    /// <summary>Server to clients, once a second: the room's name, AI slots and successors with their addresses.</summary>
    public struct RoomSnapshotBroadcast : IBroadcast
    {
        public bool Enabled;
        public string RoomName;
        public int AiSlots;
        public ushort Port;
        /// <summary>A match is running and match migration is on: the match's own migration takes over.</summary>
        public bool MatchMigrationTakesOver;
        public bool Handover;
        public string[] SuccessorTokens;
        public string[] SuccessorAddresses;
    }

    /// <summary>
    /// Room host migration (HostMigrationConfig > Room Migration): when the host is lost the first successor opens
    /// a room with the same name, port and AI slots and the others reconnect to it. A match lost without match
    /// migration also ends up there.
    /// </summary>
    public sealed class RoomMigration : MonoBehaviour
    {
        private const float SEND_INTERVAL = 1f;
        private const float MAX_SNAPSHOT_AGE = 6f;
        private const float RECONNECT_TIMEOUT = 15f;
        private const float SUCCESSOR_HEAD_START = 0.75f;
        private const ushort DEFAULT_PORT = 7777;
        private const float LOBBY_WAIT_TIMEOUT = 5f;
        private const float CLIENT_START_TIMEOUT = 3f;
        private const float RECONNECT_RETRY_INTERVAL = 0.5f;
        private const float MENU_LOAD_TIMEOUT = 15f;
        private const float ROOM_TRANSITION_SECONDS = 0.25f;
        private const float HUB_TRANSITION_SECONDS = 0.5f;
        private const float HANDOVER_FLUSH_SECONDS = 0.3f;

        public static bool Migrating { get; private set; }

        private static RoomSnapshotBroadcast? _latest;
        private static float _latestTime;
        private static RoomMigration _instance;
        private static int _chosenSuccessor = -1;

        private NetworkManager _hooked;
        private float _nextSend;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Migrating = false;
            _latest = null;
            _instance = null;
            _chosenSuccessor = -1;
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
                nm.ClientManager.OnClientConnectionState += ForgetOnNewConnection;
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
            var order = RoomSuccession.Order(remotes.ConvertAll(c => c.ClientId), lobby.MatchParticipants, HostMigrationNetwork.UnwillingToHost, _chosenSuccessor);

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
                Enabled = HostMigrationSettings.RoomMigrationEnabled || _chosenSuccessor >= 0,
                RoomName = lobby.RoomName.Value,
                AiSlots = lobby.AiSlotCount.Value,
                Port = MultiplayerTransport.Port != 0 ? MultiplayerTransport.Port : DEFAULT_PORT,
                MatchMigrationTakesOver = lobby.GameStarted.Value && HostMigrationSettings.Enabled,
                Handover = _chosenSuccessor >= 0,
                SuccessorTokens = tokens.ToArray(),
                SuccessorAddresses = addresses.ToArray(),
            };
        }

        // Joining another room right after leaving one: the old room's snapshot must not be migrated.
        private static void ForgetOnNewConnection(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started && !Migrating) _latest = null;
        }

        private static void OnSnapshotReceived(RoomSnapshotBroadcast snapshot, Channel channel)
        {
            _latest = snapshot;
            _latestTime = Time.unscaledTime;
        }

        private const string DEFAULT_ROOM_NAME = "Room";

        public static string LastRoomName =>
            _latest.HasValue && !string.IsNullOrEmpty(_latest.Value.RoomName) ? _latest.Value.RoomName : DEFAULT_ROOM_NAME;

        public static bool CanHandOverTo(int clientId)
        {
            var nm = InstanceFinder.NetworkManager;
            var lobby = LobbyLocator.GetLobbyManager();
            if (Migrating || _instance == null || nm == null || !nm.IsServerStarted || lobby == null || lobby.GameStarted.Value)
                return false;
            if (HostMigrationNetwork.UnwillingToHost.Contains(clientId)) return false;
            return nm.ServerManager.Clients.TryGetValue(clientId, out var conn) && !conn.IsLocalClient &&
                   !string.IsNullOrEmpty(HostMigrationNetwork.TokenOf(conn));
        }

        public static bool HandOverTo(int clientId)
        {
            if (!CanHandOverTo(clientId)) return false;
            var nm = InstanceFinder.NetworkManager;
            _chosenSuccessor = clientId;
            var snapshot = BuildSnapshot(nm, LobbyLocator.GetLobbyManager());
            if (snapshot.SuccessorTokens.Length == 0)
            {
                _chosenSuccessor = -1;
                return false;
            }
            nm.ServerManager.Broadcast(snapshot, requireAuthenticated: true);
            Migrating = true;
            MpCanary.Mark($"RoomMigration: handing the room over to client {clientId}");
            _instance.StartCoroutine(_instance.HandOver(snapshot));
            return true;
        }

        private IEnumerator HandOver(RoomSnapshotBroadcast snapshot)
        {
            ConnectionLostNotifier.Show(LocalizationManager.Localize("MP.RoomMigration.HandingOver"));
            yield return new WaitForSecondsRealtime(HANDOVER_FLUSH_SECONDS);
            _chosenSuccessor = -1;
            HostDisconnectWatchdog.NotifyIntentionalLeave();
            yield return Rejoin(snapshot);
        }

        /// <summary>HostDisconnectWatchdog, unexpected host loss: true when this peer rebuilds or rejoins the room.</summary>
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
            ConnectionLostNotifier.Show(LocalizationManager.Localize(snapshot.Handover ? "MP.RoomMigration.HandedToYou" : "MP.RoomMigration.NewHost"));
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
            for (float waited = 0f; waited < LOBBY_WAIT_TIMEOUT; waited += Time.unscaledDeltaTime)
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
            ConnectionLostNotifier.Show(LocalizationManager.Localize(snapshot.Handover ? "MP.RoomMigration.HandingOver" : "MP.RoomMigration.Joining"));
            yield return BackToMenu();
            yield return WaitFor(LobbyServiceLocator.Transport.LeaveLobbyAsync());
            yield return new WaitForSecondsRealtime(SUCCESSOR_HEAD_START);

            var nm = InstanceFinder.NetworkManager;
            float deadline = Time.unscaledTime + RECONNECT_TIMEOUT;
            while (nm != null && Time.unscaledTime < deadline)
            {
                if (nm.ClientManager.Started) nm.ClientManager.StopConnection();
                MultiplayerTransport.SetClientTarget(address, snapshot.Port);
                nm.ClientManager.StartConnection();
                for (float attempt = 0f; attempt < CLIENT_START_TIMEOUT && !nm.ClientManager.Started; attempt += Time.unscaledDeltaTime)
                    yield return null;
                if (nm.ClientManager.Started)
                {
                    OpenRoom();
                    MpCanary.Mark("RoomMigration: back in the room");
                    Migrating = false;
                    yield break;
                }
                yield return new WaitForSecondsRealtime(RECONNECT_RETRY_INTERVAL);
            }
            Fail("the new host did not answer");
        }

        /// <summary>A host lost mid-match leaves the map first (the match is over without it).</summary>
        private static IEnumerator BackToMenu()
        {
            if (SceneManager.GetActiveScene().buildIndex != LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX)
            {
                MultiplayerSceneTeardown.LoadMainMenu();
                for (float waited = 0f; waited < MENU_LOAD_TIMEOUT; waited += Time.unscaledDeltaTime)
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
                menu.TransitionBetweenScreens(ScreensType.LOBBY_ROOM, ROOM_TRANSITION_SECONDS);
        }

        private static void Fail(string reason)
        {
            MpCanary.Mark($"RoomMigration FAILED: {reason}");
            Migrating = false;
            ConnectionLostNotifier.Show(LocalizationManager.Localize("MP.RoomMigration.Failed"));
            var nm = InstanceFinder.NetworkManager;
            if (nm != null)
            {
                HostDisconnectWatchdog.NotifyIntentionalLeave();
                if (nm.ClientManager.Started) nm.ClientManager.StopConnection();
                if (nm.ServerManager.Started) nm.ServerManager.StopConnection(true);
            }
            if (MainMenuManager.HasInstance)
                MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.MULTIPLAYER_HUB, HUB_TRANSITION_SECONDS);
        }
    }

    /// <summary>Pure rule of who takes over a room (unit tested).</summary>
    public static class RoomSuccession
    {
        /// <summary>
        /// Remote players in take-over order: willing hosts first, then match players, then the rest, each by
        /// connection order.
        /// </summary>
        public static List<int> Order(IEnumerable<int> remoteClientIds, IEnumerable<int> matchParticipants,
                                      IEnumerable<int> unwillingToHost = null, int chosen = -1)
        {
            var playing = new HashSet<int>(matchParticipants ?? Array.Empty<int>());
            var unwilling = new HashSet<int>(unwillingToHost ?? Array.Empty<int>());
            var order = new List<int>(remoteClientIds);
            order.Sort((a, b) =>
            {
                if (a == chosen || b == chosen) return a == b ? 0 : (a == chosen ? -1 : 1);
                bool ua = unwilling.Contains(a), ub = unwilling.Contains(b);
                if (ua != ub) return ua ? 1 : -1;
                bool pa = playing.Contains(a), pb = playing.Contains(b);
                return pa != pb ? (pa ? -1 : 1) : a.CompareTo(b);
            });
            return order;
        }
    }
}
