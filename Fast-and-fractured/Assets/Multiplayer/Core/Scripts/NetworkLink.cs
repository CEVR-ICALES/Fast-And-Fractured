using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>How a machine reaches the network, as shown next to each player.</summary>
    public enum LinkKind : byte { Unknown = 0, Ethernet = 1, Wifi = 2 }

    /// <summary>
    /// Wi-Fi or cable detection, from the adapter that owns the outbound route's local address (else a non-virtual
    /// one with a gateway), on a worker thread and cached; plus ping to signal bars.
    /// </summary>
    public static class NetworkLink
    {
        private const float REFRESH_SECONDS = 15f;
        private const int FOUR_BARS_MAX_PING_MS = 40;
        private const int THREE_BARS_MAX_PING_MS = 80;
        private const int TWO_BARS_MAX_PING_MS = 150;
        private const string ROUTE_PROBE_ADDRESS = "8.8.8.8";
        private const int ROUTE_PROBE_PORT = 53;

        private static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
        private static float Now => (float)Clock.Elapsed.TotalSeconds;   // any thread, unlike Time

        private static volatile LinkKind _local = LinkKind.Unknown;
        private static float _nextRefresh;
        private static volatile bool _refreshing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _local = LinkKind.Unknown;
            _nextRefresh = 0f;
            _refreshing = false;
        }

        /// <summary>This machine's link. Kicks off a background refresh when stale.</summary>
        public static LinkKind Local
        {
            get
            {
                if (!_refreshing && Now >= _nextRefresh)
                {
                    _refreshing = true;
                    _nextRefresh = Now + REFRESH_SECONDS;
                    Task.Run(() =>
                    {
                        try { _local = Detect(); }
                        catch { /* keep the previous value */ }
                        finally { _refreshing = false; }
                    });
                }
                return _local;
            }
        }

        /// <summary>Blocking detection. Prefer <see cref="Local"/> from the main thread.</summary>
        public static LinkKind Detect()
        {
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            IPAddress outbound = OutboundAddress();

            if (outbound != null)
                foreach (var adapter in adapters)
                    if (adapter.OperationalStatus == OperationalStatus.Up && Owns(adapter, outbound))
                        return Classify(adapter.NetworkInterfaceType);

            LinkKind best = LinkKind.Unknown;
            foreach (var adapter in adapters)
            {
                if (adapter.OperationalStatus != OperationalStatus.Up || IsVirtual(adapter)) continue;
                LinkKind kind = Classify(adapter.NetworkInterfaceType);
                if (kind == LinkKind.Unknown) continue;
                if (HasGateway(adapter)) return kind;
                if (best == LinkKind.Unknown) best = kind;
            }
            return best;
        }

        public static LinkKind Classify(NetworkInterfaceType type)
        {
            switch (type)
            {
                case NetworkInterfaceType.Wireless80211:
                    return LinkKind.Wifi;
                case NetworkInterfaceType.Ethernet:
                case NetworkInterfaceType.Ethernet3Megabit:
                case NetworkInterfaceType.FastEthernetT:
                case NetworkInterfaceType.FastEthernetFx:
                case NetworkInterfaceType.GigabitEthernet:
                    return LinkKind.Ethernet;
                default:
                    return LinkKind.Unknown;
            }
        }

        /// <summary>Signal bars (0-4) for a round trip time. Negative = not measured.</summary>
        public static int Bars(int pingMs)
        {
            if (pingMs < 0) return 0;
            if (pingMs <= FOUR_BARS_MAX_PING_MS) return 4;
            if (pingMs <= THREE_BARS_MAX_PING_MS) return 3;
            if (pingMs <= TWO_BARS_MAX_PING_MS) return 2;
            return 1;
        }

        public static string ShortName(LinkKind kind) =>
            kind == LinkKind.Wifi ? "Wi-Fi" : kind == LinkKind.Ethernet ? "Ethernet" : "?";

        /// <summary>Parses the text form used in lobby adverts; Unknown when absent.</summary>
        public static LinkKind Parse(string value) =>
            byte.TryParse(value, out byte b) && Enum.IsDefined(typeof(LinkKind), b) ? (LinkKind)b : LinkKind.Unknown;

        private static IPAddress OutboundAddress()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect(ROUTE_PROBE_ADDRESS, ROUTE_PROBE_PORT);   // routing decision only, nothing is sent
                return (socket.LocalEndPoint as IPEndPoint)?.Address;
            }
            catch { return null; }
        }

        private static bool Owns(NetworkInterface adapter, IPAddress address)
        {
            foreach (var unicast in adapter.GetIPProperties().UnicastAddresses)
                if (unicast.Address.Equals(address)) return true;
            return false;
        }

        private static bool HasGateway(NetworkInterface adapter)
        {
            foreach (var gateway in adapter.GetIPProperties().GatewayAddresses)
                if (gateway.Address != null && !gateway.Address.Equals(IPAddress.Any)) return true;
            return false;
        }

        private static bool IsVirtual(NetworkInterface adapter)
        {
            string text = (adapter.Name + " " + adapter.Description).ToLowerInvariant();
            return text.Contains("virtual") || text.Contains("vethernet") || text.Contains("hyper-v")
                || text.Contains("vpn") || text.Contains("tap-") || text.Contains("loopback")
                || text.Contains("vmware") || text.Contains("hamachi") || text.Contains("zerotier");
        }
    }
}
