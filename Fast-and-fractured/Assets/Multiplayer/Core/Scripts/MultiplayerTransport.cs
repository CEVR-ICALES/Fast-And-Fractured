using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using UnityEngine;
#if STEAMWORKS_NET
using FastAndFractured.Platform;
using Steamworks;
#endif

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// The NetworkManager runs Multipass with Tugboat (IP: same network, direct join) and FishySteamworks (Steam's relay,
    /// any network). A host listens on both; a client goes through the relay when its target is a SteamID, through
    /// Tugboat otherwise. Resources/MultiplayerTransportConfig turns the relay off.
    /// </summary>
    public static class MultiplayerTransport
    {
        public const string CONFIG_RESOURCES_PATH = "MultiplayerTransportConfig";
        private const string LOCAL_ADDRESS = "localhost";
        // Individual SteamID64s start here: far above any number an IP address could be read as.
        private const ulong MIN_STEAM_ID = 0x0110000100000000UL;

        private static MultiplayerTransportConfig _config;
        private static bool _loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _loaded = false;

        public static bool SteamRelayConfigured
        {
            get
            {
                if (!_loaded)
                {
                    _loaded = true;
                    _config = Resources.Load<MultiplayerTransportConfig>(CONFIG_RESOURCES_PATH);
                }
                return _config == null || _config.UseSteamRelay;
            }
        }

        public static void Reload() => _loaded = false;

        public static bool SteamRelayAvailable => SteamRelayConfigured && SteamRunning && Steam != null;

        public static bool SteamRunning
        {
            get
            {
#if STEAMWORKS_NET
                return SteamManager.Initialized;
#else
                return false;
#endif
            }
        }

        public static string LocalSteamId
        {
            get
            {
#if STEAMWORKS_NET
                return SteamManager.Initialized ? SteamUser.GetSteamID().m_SteamID.ToString() : null;
#else
                return null;
#endif
            }
        }

        public static Tugboat Tugboat => Find<Tugboat>();
        public static global::FishySteamworks.FishySteamworks Steam => Find<global::FishySteamworks.FishySteamworks>();

        private static T Find<T>() where T : Transport
        {
            var manager = InstanceFinder.TransportManager;
            if (manager == null || manager.Transport == null) return null;
            if (manager.Transport is T direct) return direct;
            return manager.Transport is Multipass multipass ? multipass.GetTransport<T>() : null;
        }

        public static bool IsSteamId(string address) => ulong.TryParse(address, out ulong id) && id >= MIN_STEAM_ID;

        public static ushort Port
        {
            get
            {
                var tugboat = Tugboat;
                if (tugboat != null) return tugboat.GetPort();
                var transport = InstanceFinder.TransportManager != null ? InstanceFinder.TransportManager.Transport : null;
                return transport != null ? transport.GetPort() : (ushort)0;
            }
        }

        public static void SetPort(ushort port)
        {
            var transport = InstanceFinder.TransportManager != null ? InstanceFinder.TransportManager.Transport : null;
            if (transport != null) transport.SetPort(port);
        }

        public static void StartServer(NetworkManager networkManager)
        {
            if (networkManager.TransportManager.Transport is Multipass multipass)
            {
                StartServerOn(multipass, Tugboat);
                if (SteamRelayAvailable) StartServerOn(multipass, Steam);
                return;
            }
            networkManager.ServerManager.StartConnection();
        }

        private static void StartServerOn(Multipass multipass, Transport transport)
        {
            if (transport != null) multipass.StartConnection(true, transport.Index);
        }

        public static void SetClientTarget(string address, ushort port)
        {
            var manager = InstanceFinder.TransportManager;
            if (manager == null || manager.Transport == null) return;

            bool viaRelay = IsSteamId(address) && SteamRelayAvailable;
            Transport client = viaRelay ? Steam : (Transport)Tugboat;
            if (client == null) client = manager.Transport;
            if (manager.Transport is Multipass multipass && client != manager.Transport)
                multipass.SetClientTransport(client);

            client.SetClientAddress(address);
            if (!viaRelay) client.SetPort(port);
        }

        public static void SetLocalHostTarget() => SetClientTarget(LOCAL_ADDRESS, Port);

        public static float GetTimeout(bool asServer)
        {
            var tugboat = Tugboat;
            return tugboat != null ? tugboat.GetTimeout(asServer) : 0f;
        }

        public static void SetTimeout(float seconds, bool asServer)
        {
            var tugboat = Tugboat;
            if (tugboat != null) tugboat.SetTimeout(seconds, asServer);
        }
    }
}
