using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FishNet;
using FishNet.Connection;
using FishNet.Transporting;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Abstractions.Tugboat
{
    /// <summary>
    /// LAN implementation of ILobbyTransport using FishNet Tugboat + UDP broadcast on port 47777.
    /// Broadcast payload format: RoomName|Port|CurrentPlayers|GameVersion|MaxPlayers
    /// </summary>
    public sealed class TugboatLobbyTransport : ILobbyTransport, IDisposable
    {
        private const int BROADCAST_PORT = 47777;
        private const int BROADCAST_INTERVAL_MS = 1000;
        private const int LIST_SCAN_DURATION_MS = 1500;
        private const char SEPARATOR = '|';

        private string _roomName;
        private ushort _gamePort;
        private int _maxPlayers;
        private readonly Dictionary<string, string> _metadata = new Dictionary<string, string>();

        private CancellationTokenSource _broadcastCts;
        private LobbyHandle _currentHandle;
        private bool _hosting;

        public event Action<LobbyHandle> OnLobbyMemberJoined;
        public event Action<LobbyHandle> OnLobbyMemberLeft;
        public event Action OnLobbyClosed;

        public async Task<LobbyHandle> CreateLobbyAsync(LobbyCreateOptions options, CancellationToken ct = default)
        {
            _roomName  = options.RoomName ?? "Room";
            _gamePort  = options.Port;
            _maxPlayers = options.MaxPlayers;

            if (options.Metadata != null)
                foreach (var kv in options.Metadata)
                    _metadata[kv.Key] = kv.Value;

            var nm = InstanceFinder.NetworkManager;
            if (nm == null) throw new InvalidOperationException("NetworkManager not found.");

            ConfigurePort(nm, _gamePort);

            // Subscribe before starting so we don't miss early events
            nm.ServerManager.OnRemoteConnectionState += HandleRemoteState;

            // Await server start
            var tcs = new TaskCompletionSource<bool>();
            void OnServerState(ServerConnectionStateArgs args)
            {
                if (args.ConnectionState == LocalConnectionState.Started)   tcs.TrySetResult(true);
                else if (args.ConnectionState == LocalConnectionState.Stopped) tcs.TrySetResult(false);
            }
            nm.ServerManager.OnServerConnectionState += OnServerState;
            nm.ServerManager.StartConnection();

            using (ct.Register(() => tcs.TrySetCanceled()))
            {
                bool started;
                try { started = await tcs.Task; }
                catch (OperationCanceledException) { nm.ServerManager.OnServerConnectionState -= OnServerState; throw; }
                nm.ServerManager.OnServerConnectionState -= OnServerState;

                if (!started) throw new Exception("[TugboatLobbyTransport] Failed to start server.");
            }

            if (!options.HostAsServer)
            {
                // Host mode: also start local client
                ConfigureAddress(nm, "localhost", _gamePort);
                nm.ClientManager.StartConnection();
            }

            _hosting = true;
            _currentHandle = LobbyHandle.FromAddress(GetLocalIP(), _gamePort);

            if (options.IsPublic)
                StartBroadcasting();

            return _currentHandle;
        }

        public async Task<bool> JoinLobbyAsync(LobbyHandle handle, CancellationToken ct = default)
        {
            var nm = InstanceFinder.NetworkManager;
            if (nm == null) return false;

            ConfigureAddress(nm, handle.Address, handle.Port);

            var tcs = new TaskCompletionSource<bool>();
            void OnClientState(ClientConnectionStateArgs args)
            {
                if (args.ConnectionState == LocalConnectionState.Started)    tcs.TrySetResult(true);
                else if (args.ConnectionState == LocalConnectionState.Stopped) tcs.TrySetResult(false);
            }
            nm.ClientManager.OnClientConnectionState += OnClientState;
            nm.ClientManager.StartConnection();

            bool result = false;
            using (ct.Register(() => tcs.TrySetCanceled()))
            {
                try { result = await tcs.Task; }
                catch (OperationCanceledException) { result = false; }
            }
            nm.ClientManager.OnClientConnectionState -= OnClientState;
            return result;
        }

        public Task LeaveLobbyAsync(CancellationToken ct = default)
        {
            StopBroadcasting();

            var nm = InstanceFinder.NetworkManager;
            if (nm != null)
            {
                nm.ServerManager.OnRemoteConnectionState -= HandleRemoteState;
                if (_hosting && nm.ServerManager.Started)
                    nm.ServerManager.StopConnection(true);
                else if (nm.ClientManager.Started)
                    nm.ClientManager.StopConnection();
            }

            _hosting = false;
            OnLobbyClosed?.Invoke();
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<LobbyListing>> ListLobbiesAsync(LobbyFilter filter, CancellationToken ct = default)
        {
            var results = new List<LobbyListing>();
            var seen    = new HashSet<string>();

            try
            {
                using var udp = new UdpClient();
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, BROADCAST_PORT));

                var deadline = DateTime.UtcNow.AddMilliseconds(LIST_SCAN_DURATION_MS);
                while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
                {
                    try
                    {
                        var receiveTask = udp.ReceiveAsync();
                        var timeout     = Task.Delay(200, ct);
                        if (await Task.WhenAny(receiveTask, timeout) != receiveTask) continue;

                        var packet  = receiveTask.Result;
                        var listing = ParsePayload(packet.Buffer, packet.RemoteEndPoint);
                        if (listing != null && seen.Add(listing.Handle.PlatformId))
                            results.Add(listing);
                    }
                    catch (OperationCanceledException) { break; }
                    catch { /* bad packet — skip */ }
                }
            }
            catch { /* socket unavailable */ }

            return results;
        }

        public void SetLobbyMetadata(string key, string value) => _metadata[key] = value;
        public string GetLobbyMetadata(string key) => _metadata.TryGetValue(key, out var v) ? v : null;

        private void StartBroadcasting()
        {
            StopBroadcasting();
            _broadcastCts = new CancellationTokenSource();
            _ = BroadcastLoopAsync(_broadcastCts.Token);
        }

        private void StopBroadcasting()
        {
            _broadcastCts?.Cancel();
            _broadcastCts?.Dispose();
            _broadcastCts = null;
        }

        private async Task BroadcastLoopAsync(CancellationToken ct)
        {
            try
            {
                using var udp = new UdpClient { EnableBroadcast = true };
                var endpoint  = new IPEndPoint(IPAddress.Broadcast, BROADCAST_PORT);

                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        int players = InstanceFinder.NetworkManager?.ServerManager?.Clients?.Count ?? 0;
                        byte[] data = Encoding.UTF8.GetBytes(BuildPayload(players));
                        udp.Send(data, data.Length, endpoint);
                    }
                    catch { /* ignore send error */ }

                    await Task.Delay(BROADCAST_INTERVAL_MS, ct);
                }
            }
            catch (OperationCanceledException) { }
        }

        private string BuildPayload(int playerCount) =>
            string.Join(SEPARATOR.ToString(), _roomName, _gamePort, playerCount, Application.version, _maxPlayers);

        private static LobbyListing ParsePayload(byte[] data, IPEndPoint remote)
        {
            try
            {
                var parts = Encoding.UTF8.GetString(data).Split('|');
                if (parts.Length < 5) return null;
                if (!ushort.TryParse(parts[1], out ushort port)) return null;
                int.TryParse(parts[2], out int players);
                int.TryParse(parts[4], out int maxPlayers);

                return new LobbyListing
                {
                    Handle         = LobbyHandle.FromAddress(remote.Address.ToString(), port),
                    RoomName       = parts[0],
                    CurrentPlayers = players,
                    MaxPlayers     = maxPlayers,
                    PingMs         = 0,
                };
            }
            catch { return null; }
        }

        private void HandleRemoteState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Started)
                OnLobbyMemberJoined?.Invoke(_currentHandle);
            else if (args.ConnectionState == RemoteConnectionState.Stopped)
                OnLobbyMemberLeft?.Invoke(_currentHandle);
        }

        private static void ConfigurePort(FishNet.Managing.NetworkManager nm, ushort port)
            => nm.TransportManager.Transport?.SetPort(port);

        private static void ConfigureAddress(FishNet.Managing.NetworkManager nm, string address, ushort port)
        {
            nm.TransportManager.Transport?.SetClientAddress(address);
            nm.TransportManager.Transport?.SetPort(port);
        }

        private static string GetLocalIP()
        {
            try
            {
                using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                s.Connect("8.8.8.8", 65530);
                return ((IPEndPoint)s.LocalEndPoint).Address.ToString();
            }
            catch { return "127.0.0.1"; }
        }

        public void Dispose()
        {
            StopBroadcasting();
            var nm = InstanceFinder.NetworkManager;
            if (nm != null)
                nm.ServerManager.OnRemoteConnectionState -= HandleRemoteState;
        }
    }
}
