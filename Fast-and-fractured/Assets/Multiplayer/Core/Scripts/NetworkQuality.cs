using System.Collections.Generic;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Client → server, once a second: how this peer's connection is doing.</summary>
    public struct NetworkQualityReport : IBroadcast
    {
        public string Name;
        public bool IsHost;
        public byte Link;
        public ushort PingMs;
        public byte DelayFrames;
        public byte RollbackFrames;
        public byte RollbackMaxFrames;
    }

    /// <summary>Server → clients, once a second: every player's latest report.</summary>
    public struct NetworkQualityTable : IBroadcast
    {
        public int[] ClientIds;
        public NetworkQualityReport[] Reports;
    }

    /// <summary>
    /// Per-player connection quality (Wi-Fi or cable, ping, input delay and rollback frames) for the room and the
    /// match HUD. Each peer reports itself once a second and the server sends the table back, via broadcasts so it
    /// survives scene changes and host migration. Frames are network ticks.
    /// </summary>
    public sealed class NetworkQuality : MonoBehaviour
    {
        public const float REPORT_INTERVAL = 1f;

        /// <summary>Latest table, by FishNet client id. Updated on every client.</summary>
        public static readonly Dictionary<int, NetworkQualityReport> Players = new Dictionary<int, NetworkQualityReport>();

        /// <summary>This peer's own numbers, refreshed each report even before the table returns.</summary>
        public static NetworkQualityReport Local { get; private set; }

        /// <summary>Increments whenever <see cref="Players"/> changes.</summary>
        public static int Version { get; private set; }

        private static NetworkQuality _instance;
        private static readonly Dictionary<int, NetworkQualityReport> ServerReports = new Dictionary<int, NetworkQualityReport>();

        private NetworkManager _hooked;
        private FishNet.Managing.Predicting.PredictionManager _prediction;
        private float _nextReport;

        // Rollback accumulation over one report window.
        private int _replaysThisReconcile, _reconciles, _replaysTotal, _replaysMax;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            Players.Clear();
            ServerReports.Clear();
            Local = default;
            Version = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[NetworkQuality]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<NetworkQuality>();
        }

        public static bool TryGet(int clientId, out NetworkQualityReport report) =>
            Players.TryGetValue(clientId, out report);

        private void Update()
        {
            Hook(InstanceFinder.NetworkManager);
            if (_hooked == null || Time.unscaledTime < _nextReport) return;
            _nextReport = Time.unscaledTime + REPORT_INTERVAL;

            if (_hooked.IsClientStarted) SendReport();
            if (_hooked.IsServerStarted) SendTable();
        }

        private void Hook(NetworkManager nm)
        {
            if (nm == _hooked) return;
            Unhook();
            if (nm == null) return;
            _hooked = nm;
            nm.ServerManager.RegisterBroadcast<NetworkQualityReport>(OnReport);
            nm.ServerManager.OnRemoteConnectionState += OnRemoteState;
            nm.ServerManager.OnServerConnectionState += OnServerState;
            nm.ClientManager.RegisterBroadcast<NetworkQualityTable>(OnTable);
            nm.ClientManager.OnClientConnectionState += OnClientState;
            // NetworkManager.PredictionManager is internal; the component sits on the same object.
            _prediction = nm.GetComponent<FishNet.Managing.Predicting.PredictionManager>();
            if (_prediction != null)
            {
                _prediction.OnPreReconcile += OnPreReconcile;
                _prediction.OnPostReplicateReplay += OnPostReplay;
                _prediction.OnPostReconcile += OnPostReconcile;
            }
        }

        private void Unhook()
        {
            if (_hooked == null) return;
            _hooked.ServerManager.UnregisterBroadcast<NetworkQualityReport>(OnReport);
            _hooked.ServerManager.OnRemoteConnectionState -= OnRemoteState;
            _hooked.ServerManager.OnServerConnectionState -= OnServerState;
            _hooked.ClientManager.UnregisterBroadcast<NetworkQualityTable>(OnTable);
            _hooked.ClientManager.OnClientConnectionState -= OnClientState;
            if (_prediction != null)
            {
                _prediction.OnPreReconcile -= OnPreReconcile;
                _prediction.OnPostReplicateReplay -= OnPostReplay;
                _prediction.OnPostReconcile -= OnPostReconcile;
                _prediction = null;
            }
            _hooked = null;
        }

        private void OnDestroy() => Unhook();

        private void OnPreReconcile(uint clientTick, uint serverTick) => _replaysThisReconcile = 0;
        private void OnPostReplay(uint clientTick, uint serverTick) => _replaysThisReconcile++;

        private void OnPostReconcile(uint clientTick, uint serverTick)
        {
            _reconciles++;
            _replaysTotal += _replaysThisReconcile;
            if (_replaysThisReconcile > _replaysMax) _replaysMax = _replaysThisReconcile;
        }

        private void SendReport()
        {
            var tm = _hooked.TimeManager;
            bool isHost = _hooked.IsServerStarted;
            long rtt = isHost ? 0 : tm.RoundTripTime;
            double tickMs = tm.TickDelta * 1000d;

            var report = new NetworkQualityReport
            {
                Name = NetworkedLobbyManager.GetLocalDisplayName(),
                IsHost = isHost,
                Link = (byte)NetworkLink.Local,
                PingMs = (ushort)Mathf.Clamp(rtt, 0, ushort.MaxValue),
                DelayFrames = (byte)(tickMs > 0 ? Mathf.Clamp(Mathf.CeilToInt((float)(rtt / 2d / tickMs)), 0, byte.MaxValue) : 0),
                RollbackFrames = (byte)(_reconciles > 0 ? Mathf.Clamp(Mathf.RoundToInt(_replaysTotal / (float)_reconciles), 0, byte.MaxValue) : 0),
                RollbackMaxFrames = (byte)Mathf.Clamp(_replaysMax, 0, byte.MaxValue),
            };
            _reconciles = _replaysTotal = _replaysMax = 0;

            Local = report;
            _hooked.ClientManager.Broadcast(report);
        }

        private void OnClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Stopped) return;
            Players.Clear();
            Version++;
        }

        private static void OnTable(NetworkQualityTable table, Channel channel)
        {
            Players.Clear();
            if (table.ClientIds != null && table.Reports != null)
                for (int i = 0; i < table.ClientIds.Length && i < table.Reports.Length; i++)
                    Players[table.ClientIds[i]] = table.Reports[i];
            Version++;
        }

        private static void OnReport(NetworkConnection conn, NetworkQualityReport report, Channel channel)
        {
            if (conn == null) return;
            ServerReports[conn.ClientId] = report;
        }

        private static void OnRemoteState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Stopped) ServerReports.Remove(conn.ClientId);
        }

        private static void OnServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Stopped) ServerReports.Clear();
        }

        private void SendTable()
        {
            var ids = new int[ServerReports.Count];
            var reports = new NetworkQualityReport[ServerReports.Count];
            int i = 0;
            foreach (var pair in ServerReports)
            {
                ids[i] = pair.Key;
                reports[i] = pair.Value;
                i++;
            }
            _hooked.ServerManager.Broadcast(new NetworkQualityTable { ClientIds = ids, Reports = reports });
        }
    }
}
