#if STEAMWORKS_NET
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FishNet;
using FishNet.Connection;
using FishNet.Transporting;
using Steamworks;
using FastAndFractured.Platform;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Abstractions.Steam
{
    /// <summary>
    /// ILobbyTransport over Steam Matchmaking for discovery and Tugboat for the connection: the host writes its
    /// IP:port into the Steam lobby and clients read it to connect.
    /// </summary>
    public sealed class SteamLobbyTransport : ILobbyTransport, IDisposable
    {
        private const string KEY_ROOM_NAME   = "room_name";
        private const string KEY_VERSION     = "version";
        private const string KEY_MAX_PLAYERS = "max_players";
        private const string KEY_SERVER_IP   = "server_ip";
        private const string KEY_SERVER_PORT = "server_port";
        // Lobby metadata mirrors the connection count: GetNumLobbyMembers is stale for lobbies the caller has not joined.
        private const string KEY_NUM_PLAYERS = "num_players";
        private const string KEY_HOST_LINK   = "host_link";
        // Steam keeps a lobby alive while anyone is still in it: the browser hides one whose host stopped writing this.
        private const string KEY_HEARTBEAT   = "heartbeat";
        // The host's SteamID, set when it also listens on Steam's relay: clients connect to it from any network.
        private const string KEY_RELAY_HOST  = "relay_host";
        internal const uint HEARTBEAT_INTERVAL_SECONDS = 15;
        internal const uint STALE_HEARTBEAT_SECONDS = 60;
        // FishySteamworks' own connect timeout never fires: give up on the relay after this and try the IP.
        private const float RELAY_CONNECT_TIMEOUT_SECONDS = 10f;
        private const ushort DEFAULT_SERVER_PORT = 7777;
        // Any public address: connecting a UDP socket only picks the outgoing interface.
        private const string ROUTE_PROBE_ADDRESS = "8.8.8.8";
        private const int ROUTE_PROBE_PORT = 65530;

        private CSteamID _currentLobbyId = CSteamID.Nil;
        private bool _hosting;
        private ushort _hostedPort;
        private bool _watchingJoinedClient;
        private LobbyHeartbeat _heartbeat;

        /// <summary>Only list lobbies of this Application.version; turn off to test an editor against an older build.</summary>
        public static bool _filterByVersion = true;

        // Kept alive against GC.
        private CallResult<LobbyCreated_t>   _lobbyCreatedResult;
        private CallResult<LobbyMatchList_t> _lobbyListResult;
        private Callback<LobbyEnter_t>       _lobbyEnterCallback;
        private Callback<LobbyChatUpdate_t>  _lobbyChatUpdateCallback;

        public event Action<LobbyHandle> OnLobbyMemberJoined;
        public event Action<LobbyHandle> OnLobbyMemberLeft;
        public event Action OnLobbyClosed;

        public SteamLobbyTransport()
        {
            _lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            Application.quitting += LeaveCurrentLobby;
        }


        public async Task<LobbyHandle> CreateLobbyAsync(LobbyCreateOptions options, CancellationToken ct = default)
        {
            if (!SteamManager.Initialized)
                throw new InvalidOperationException("[SteamLobbyTransport] Steam not initialized.");

            _hostedPort = options.Port;

            var lobbyType = options.IsPublic
                ? ELobbyType.k_ELobbyTypePublic
                : ELobbyType.k_ELobbyTypeFriendsOnly;

            LeaveCurrentLobby();

            var createTcs = new TaskCompletionSource<LobbyCreated_t>();
            _lobbyCreatedResult?.Dispose();
            _lobbyCreatedResult = CallResult<LobbyCreated_t>.Create((r, failure) =>
            {
                if (failure) createTcs.TrySetException(new Exception("Steam lobby I/O failed."));
                else         createTcs.TrySetResult(r);
            });
            _lobbyCreatedResult.Set(SteamMatchmaking.CreateLobby(lobbyType, options.MaxPlayers));

            LobbyCreated_t created;
            using (ct.Register(() => createTcs.TrySetCanceled()))
                created = await createTcs.Task;

            if ((EResult)created.m_eResult != EResult.k_EResultOK)
                throw new Exception($"[SteamLobbyTransport] Lobby creation failed: {(EResult)created.m_eResult}");

            _currentLobbyId = new CSteamID(created.m_ulSteamIDLobby);
            _hosting        = true;

            string serverIp = GetLocalIP();
            Debug.Log($"[SteamLobbyTransport] Created lobby {_currentLobbyId.m_SteamID} | " +
                      $"version='{Application.version}' | appId={SteamUtils.GetAppID()} | " +
                      $"type={lobbyType} | ip={serverIp}:{options.Port}");
            SteamMatchmaking.SetLobbyData(_currentLobbyId, KEY_ROOM_NAME,   options.RoomName ?? "Room");
            SteamMatchmaking.SetLobbyData(_currentLobbyId, KEY_VERSION,     Application.version);
            SteamMatchmaking.SetLobbyData(_currentLobbyId, KEY_MAX_PLAYERS, options.MaxPlayers.ToString());
            SteamMatchmaking.SetLobbyData(_currentLobbyId, KEY_SERVER_IP,   serverIp);
            SteamMatchmaking.SetLobbyData(_currentLobbyId, KEY_SERVER_PORT, options.Port.ToString());
            // Seeded to 0; the real count is written once the host's client (or the first remote) connects.
            SteamMatchmaking.SetLobbyData(_currentLobbyId, KEY_NUM_PLAYERS, "0");
            SteamMatchmaking.SetLobbyData(_currentLobbyId, KEY_HOST_LINK,
                ((byte)FastAndFractured.Multiplayer.NetworkLink.Detect()).ToString());
            StartHeartbeat();

            if (options.Metadata != null)
                foreach (var kv in options.Metadata)
                    SteamMatchmaking.SetLobbyData(_currentLobbyId, kv.Key, kv.Value);

            var nm = InstanceFinder.NetworkManager;
            if (nm == null) throw new InvalidOperationException("NetworkManager not found.");

            MultiplayerTransport.SetPort(options.Port);
            nm.ServerManager.OnRemoteConnectionState += HandleRemoteState;
            // FishNet raises no OnRemoteConnectionState for the host's own client.
            nm.ClientManager.OnClientConnectionState += HandleLocalClientState;

            await StartServerAsync(nm, ct);
            if (MultiplayerTransport.SteamRelayAvailable)
                SteamMatchmaking.SetLobbyData(_currentLobbyId, KEY_RELAY_HOST, MultiplayerTransport.LocalSteamId);

            // The match teardown stops the server directly: without this the lobby outlived it, empty, in the browser.
            nm.ServerManager.OnServerConnectionState -= HandleServerStopped;
            nm.ServerManager.OnServerConnectionState += HandleServerStopped;

            if (!options.HostAsServer)
            {
                MultiplayerTransport.SetLocalHostTarget();
                nm.ClientManager.StartConnection();
            }
            else
            {
                // Dedicated server: no local client, so the count is 0.
                UpdateLobbyPlayerCount();
            }

            return new LobbyHandle(_currentLobbyId.m_SteamID.ToString(), serverIp, options.Port);
        }


        public async Task<bool> JoinLobbyAsync(LobbyHandle handle, CancellationToken ct = default)
        {
            // A non-Steam id is a direct IP join (LobbyHandle.FromAddress): connect straight away.
            if (!ulong.TryParse(handle.PlatformId, out ulong lobbyRaw))
                return !string.IsNullOrEmpty(handle.Address) && await JoinAddressAsync(handle.Address, handle.Port, ct);

            if (!SteamManager.Initialized) return false;

            LeaveCurrentLobby();

            var joinTcs = new TaskCompletionSource<LobbyEnter_t>();
            _lobbyEnterCallback?.Dispose();
            _lobbyEnterCallback = Callback<LobbyEnter_t>.Create(r => joinTcs.TrySetResult(r));
            SteamMatchmaking.JoinLobby(new CSteamID(lobbyRaw));

            LobbyEnter_t entered;
            using (ct.Register(() => joinTcs.TrySetCanceled()))
            {
                try { entered = await joinTcs.Task; }
                catch { return false; }
                finally
                {
                    _lobbyEnterCallback?.Dispose();
                    _lobbyEnterCallback = null;
                }
            }

            if (entered.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                return false;

            _currentLobbyId = new CSteamID(entered.m_ulSteamIDLobby);

            string serverIp   = SteamMatchmaking.GetLobbyData(_currentLobbyId, KEY_SERVER_IP);
            string serverPort = SteamMatchmaking.GetLobbyData(_currentLobbyId, KEY_SERVER_PORT);

            if (string.IsNullOrEmpty(serverIp))
            {
                Debug.LogError("[SteamLobbyTransport] Lobby is missing server_ip — server may not be ready yet.");
                LeaveCurrentLobby();
                return false;
            }

            if (!ushort.TryParse(serverPort, out ushort port)) port = DEFAULT_SERVER_PORT;

            bool joined = false;
            try
            {
                string relayHost = SteamMatchmaking.GetLobbyData(_currentLobbyId, KEY_RELAY_HOST);
                if (MultiplayerTransport.SteamRelayAvailable && MultiplayerTransport.IsSteamId(relayHost))
                {
                    using (var relayAttempt = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        relayAttempt.CancelAfter(TimeSpan.FromSeconds(RELAY_CONNECT_TIMEOUT_SECONDS));
                        joined = await JoinAddressAsync(relayHost, port, relayAttempt.Token);
                    }
                    if (!joined)
                    {
                        FastAndFractured.Multiplayer.HostDisconnectWatchdog.NotifyIntentionalLeave();
                        InstanceFinder.ClientManager?.StopConnection();
                        Debug.LogWarning("[SteamLobbyTransport] Could not reach the host through Steam's relay; trying its IP.");
                    }
                }
                if (!joined && !ct.IsCancellationRequested)
                    joined = await JoinAddressAsync(serverIp, port, ct);
            }
            finally
            {
                if (joined) WatchJoinedClient();
                else LeaveCurrentLobby();
            }
            return joined;
        }

        private static async Task<bool> JoinAddressAsync(string ip, ushort port, CancellationToken ct)
        {
            var nm = InstanceFinder.NetworkManager;
            if (nm == null) return false;

            MultiplayerTransport.SetClientTarget(ip, port);
            return await StartClientAsync(nm, ct);
        }

        private void HandleServerStopped(FishNet.Transporting.ServerConnectionStateArgs args)
        {
            if (args.ConnectionState != FishNet.Transporting.LocalConnectionState.Stopped) return;
            var nm = InstanceFinder.NetworkManager;
            // Multipass reports each transport: the room lives while any of them still serves.
            if (nm != null && nm.ServerManager.Started) return;
            if (nm != null)
            {
                nm.ServerManager.OnServerConnectionState -= HandleServerStopped;
                nm.ServerManager.OnRemoteConnectionState -= HandleRemoteState;
                nm.ClientManager.OnClientConnectionState -= HandleLocalClientState;
            }
            if (!_hosting) return;
            LeaveCurrentLobby();
            OnLobbyClosed?.Invoke();
        }

        private void WatchJoinedClient()
        {
            var nm = InstanceFinder.NetworkManager;
            if (nm == null || _watchingJoinedClient) return;
            nm.ClientManager.OnClientConnectionState += HandleJoinedClientState;
            _watchingJoinedClient = true;
        }

        private void HandleJoinedClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Stopped || _hosting) return;
            LeaveCurrentLobby();
        }

        private void LeaveCurrentLobby()
        {
            if (_watchingJoinedClient)
            {
                var nm = InstanceFinder.NetworkManager;
                if (nm != null) nm.ClientManager.OnClientConnectionState -= HandleJoinedClientState;
                _watchingJoinedClient = false;
            }
            StopHeartbeat();
            if (_currentLobbyId != CSteamID.Nil && SteamManager.Initialized)
                SteamMatchmaking.LeaveLobby(_currentLobbyId);
            _currentLobbyId = CSteamID.Nil;
            _hosting = false;
        }

        private void StartHeartbeat()
        {
            StopHeartbeat();
            WriteHeartbeat();
            var go = new GameObject("[SteamLobbyHeartbeat]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _heartbeat = go.AddComponent<LobbyHeartbeat>();
            _heartbeat.Owner = this;
        }

        private void StopHeartbeat()
        {
            if (_heartbeat != null) UnityEngine.Object.Destroy(_heartbeat.gameObject);
            _heartbeat = null;
        }

        private void WriteHeartbeat()
        {
            if (_hosting && _currentLobbyId != CSteamID.Nil && SteamManager.Initialized)
                SteamMatchmaking.SetLobbyData(_currentLobbyId, KEY_HEARTBEAT, SteamUtils.GetServerRealTime().ToString());
        }

        internal static bool IsHeartbeatStale(string heartbeat, uint now)
        {
            if (!uint.TryParse(heartbeat, out uint last)) return false;
            return now > last && now - last > STALE_HEARTBEAT_SECONDS;
        }

        private sealed class LobbyHeartbeat : MonoBehaviour
        {
            public SteamLobbyTransport Owner;
            private float _next;

            private void Update()
            {
                if (Time.unscaledTime < _next) return;
                _next = Time.unscaledTime + HEARTBEAT_INTERVAL_SECONDS;
                Owner?.WriteHeartbeat();
            }
        }


        public Task LeaveLobbyAsync(CancellationToken ct = default)
        {
            var nm = InstanceFinder.NetworkManager;
            if (nm != null)
            {
                nm.ServerManager.OnRemoteConnectionState -= HandleRemoteState;
                nm.ClientManager.OnClientConnectionState -= HandleLocalClientState;
                if (_hosting && nm.ServerManager.Started)
                    nm.ServerManager.StopConnection(true);
                else if (nm.ClientManager.Started)
                    nm.ClientManager.StopConnection();
            }

            LeaveCurrentLobby();
            OnLobbyClosed?.Invoke();
            return Task.CompletedTask;
        }


        public async Task<IReadOnlyList<LobbyListing>> ListLobbiesAsync(LobbyFilter filter, CancellationToken ct = default)
        {
            var results = new List<LobbyListing>();
            if (!SteamManager.Initialized) return results;

            Debug.Log($"[SteamLobbyTransport] Listing lobbies | filterVersion='{Application.version}' | " +
                      $"appId={SteamUtils.GetAppID()} | versionFilter={(_filterByVersion ? "ON" : "OFF")}");

            if (_filterByVersion)
                SteamMatchmaking.AddRequestLobbyListStringFilter(
                    KEY_VERSION, Application.version, ELobbyComparison.k_ELobbyComparisonEqual);

            if (filter?.RequiredMetadata != null)
                foreach (var kv in filter.RequiredMetadata)
                    SteamMatchmaking.AddRequestLobbyListStringFilter(
                        kv.Key, kv.Value, ELobbyComparison.k_ELobbyComparisonEqual);

            var listTcs = new TaskCompletionSource<LobbyMatchList_t>();
            _lobbyListResult?.Dispose();
            _lobbyListResult = CallResult<LobbyMatchList_t>.Create((r, failure) =>
            {
                if (failure) listTcs.TrySetResult(default);
                else         listTcs.TrySetResult(r);
            });
            _lobbyListResult.Set(SteamMatchmaking.RequestLobbyList());

            LobbyMatchList_t list;
            using (ct.Register(() => listTcs.TrySetCanceled()))
            {
                try { list = await listTcs.Task; }
                catch { return results; }
            }

            Debug.Log($"[SteamLobbyTransport] RequestLobbyList returned {(int)list.m_nLobbiesMatching} matching lobbies.");

            uint now = SteamUtils.GetServerRealTime();
            for (int i = 0; i < (int)list.m_nLobbiesMatching; i++)
            {
                var sid       = SteamMatchmaking.GetLobbyByIndex(i);
                if (IsHeartbeatStale(SteamMatchmaking.GetLobbyData(sid, KEY_HEARTBEAT), now)) continue;
                string name   = SteamMatchmaking.GetLobbyData(sid, KEY_ROOM_NAME);
                string ip     = SteamMatchmaking.GetLobbyData(sid, KEY_SERVER_IP);
                string portS  = SteamMatchmaking.GetLobbyData(sid, KEY_SERVER_PORT);
                string maxS   = SteamMatchmaking.GetLobbyData(sid, KEY_MAX_PLAYERS);
                string numS   = SteamMatchmaking.GetLobbyData(sid, KEY_NUM_PLAYERS);
                int maxP      = int.TryParse(maxS,  out int m) ? m : 8;
                ushort port   = ushort.TryParse(portS, out ushort p) ? p : DEFAULT_SERVER_PORT;
                // The host's count; Steam's member count only for lobbies without it.
                int members   = int.TryParse(numS, out int n)
                    ? n
                    : SteamMatchmaking.GetNumLobbyMembers(sid);

                results.Add(new LobbyListing
                {
                    Handle         = new LobbyHandle(sid.m_SteamID.ToString(), ip, port),
                    RoomName       = string.IsNullOrEmpty(name) ? "Room" : name,
                    CurrentPlayers = members,
                    MaxPlayers     = maxP,
                    PingMs         = 0,
                    HostLink       = FastAndFractured.Multiplayer.NetworkLink.Parse(SteamMatchmaking.GetLobbyData(sid, KEY_HOST_LINK)),
                    InMatch        = SteamMatchmaking.GetLobbyData(sid, LobbyListing.KEY_IN_MATCH) == "1",
                });
            }

            return results;
        }


        public void SetLobbyMetadata(string key, string value)
        {
            if (_currentLobbyId != CSteamID.Nil)
                SteamMatchmaking.SetLobbyData(_currentLobbyId, key, value);
        }

        public string GetLobbyMetadata(string key) =>
            _currentLobbyId != CSteamID.Nil
                ? SteamMatchmaking.GetLobbyData(_currentLobbyId, key)
                : null;


        private void OnLobbyChatUpdate(LobbyChatUpdate_t cb)
        {
            var h = new LobbyHandle(_currentLobbyId.m_SteamID.ToString(), string.Empty, _hostedPort);
            uint entered = (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered;
            uint left    = (uint)(EChatMemberStateChange.k_EChatMemberStateChangeLeft   |
                                   EChatMemberStateChange.k_EChatMemberStateChangeDisconnected |
                                   EChatMemberStateChange.k_EChatMemberStateChangeKicked |
                                   EChatMemberStateChange.k_EChatMemberStateChangeBanned);

            if ((cb.m_rgfChatMemberStateChange & entered) != 0) OnLobbyMemberJoined?.Invoke(h);
            if ((cb.m_rgfChatMemberStateChange & left)    != 0) OnLobbyMemberLeft?.Invoke(h);
        }

        private void HandleRemoteState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            var h = new LobbyHandle(_currentLobbyId.m_SteamID.ToString(), string.Empty, _hostedPort);
            if (args.ConnectionState == RemoteConnectionState.Started) OnLobbyMemberJoined?.Invoke(h);
            if (args.ConnectionState == RemoteConnectionState.Stopped) OnLobbyMemberLeft?.Invoke(h);

            // On Stopped the conn may still be in Clients: pass it so the count leaves it out.
            UpdateLobbyPlayerCount(
                leavingConn: args.ConnectionState == RemoteConnectionState.Stopped ? conn : null);
        }

        private void HandleLocalClientState(FishNet.Transporting.ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == FishNet.Transporting.LocalConnectionState.Started ||
                args.ConnectionState == FishNet.Transporting.LocalConnectionState.Stopped)
                UpdateLobbyPlayerCount();
        }

        /// <summary>Writes the player count (FishNet connections; the server process is not a player) into the lobby.</summary>
        private void UpdateLobbyPlayerCount(NetworkConnection leavingConn = null)
        {
            if (!_hosting || _currentLobbyId == CSteamID.Nil) return;
            var clients = InstanceFinder.ServerManager?.Clients;
            int total = clients?.Count ?? 0;
            if (leavingConn != null && clients != null && clients.ContainsKey(leavingConn.ClientId))
                total -= 1;
            if (total < 0) total = 0;
            SteamMatchmaking.SetLobbyData(_currentLobbyId, KEY_NUM_PLAYERS, total.ToString());
        }

        private static async Task StartServerAsync(FishNet.Managing.NetworkManager nm, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<bool>();
            // Tugboat decides: the relay is optional, its failure must not fail the room.
            int primary = MultiplayerTransport.Tugboat != null ? MultiplayerTransport.Tugboat.Index : -1;
            void OnState(ServerConnectionStateArgs a)
            {
                if (primary >= 0 && a.TransportIndex != primary) return;
                if (a.ConnectionState == LocalConnectionState.Started) tcs.TrySetResult(true);
                if (a.ConnectionState == LocalConnectionState.Stopped) tcs.TrySetResult(false);
            }
            nm.ServerManager.OnServerConnectionState += OnState;
            MultiplayerTransport.StartServer(nm);

            bool ok;
            using (ct.Register(() => tcs.TrySetCanceled()))
            {
                try { ok = await tcs.Task; }
                catch { nm.ServerManager.OnServerConnectionState -= OnState; throw; }
            }
            nm.ServerManager.OnServerConnectionState -= OnState;
            if (!ok) throw new Exception("[SteamLobbyTransport] Server failed to start.");
        }

        private static async Task<bool> StartClientAsync(FishNet.Managing.NetworkManager nm, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<bool>();
            int clientIndex = nm.TransportManager.Transport is FishNet.Transporting.Multipass.Multipass multipass && multipass.ClientTransport != null
                ? multipass.ClientTransport.Index : -1;
            void OnState(ClientConnectionStateArgs a)
            {
                if (clientIndex >= 0 && a.TransportIndex != clientIndex) return;
                if (a.ConnectionState == LocalConnectionState.Started) tcs.TrySetResult(true);
                if (a.ConnectionState == LocalConnectionState.Stopped) tcs.TrySetResult(false);
            }
            nm.ClientManager.OnClientConnectionState += OnState;
            nm.ClientManager.StartConnection();

            bool ok;
            using (ct.Register(() => tcs.TrySetCanceled()))
            {
                try { ok = await tcs.Task; }
                catch { ok = false; }
            }
            nm.ClientManager.OnClientConnectionState -= OnState;
            return ok;
        }

        private static string GetLocalIP()
        {
            try
            {
                using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                s.Connect(ROUTE_PROBE_ADDRESS, ROUTE_PROBE_PORT);
                return ((IPEndPoint)s.LocalEndPoint).Address.ToString();
            }
            catch { return "127.0.0.1"; }
        }

        public void Dispose()
        {
            Application.quitting -= LeaveCurrentLobby;
            LeaveCurrentLobby();
            _lobbyChatUpdateCallback?.Dispose();
            _lobbyCreatedResult?.Dispose();
            _lobbyListResult?.Dispose();
            _lobbyEnterCallback?.Dispose();
        }
    }
}
#endif
