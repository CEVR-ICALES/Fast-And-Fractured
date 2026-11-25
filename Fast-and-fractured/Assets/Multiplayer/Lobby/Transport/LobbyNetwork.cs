using System;
using FastAndFractured.Multiplayer.Abstractions.Tugboat;
#if STEAMWORKS_NET
using FastAndFractured.Multiplayer.Abstractions.Steam;
#endif
using FishNet;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Abstractions
{
    public enum LobbyNetworkMode { Steam = 0, Lan = 1 }

    public static class LobbyNetwork
    {
        public const string MODE_KEY = "MP_LobbyNetworkMode";

        private static ILobbyTransport _steamTransport;
        private static IPlayerIdentityProvider _steamIdentity;
        private static ILobbyTransport _lanTransport;
        private static IPlayerIdentityProvider _lanIdentity;
        private static LobbyNetworkMode? _sessionChoice;

        public static LobbyNetworkMode Mode { get; private set; }
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _steamTransport = null;
            _steamIdentity = null;
            _lanTransport = null;
            _lanIdentity = null;
            _sessionChoice = null;
            Mode = LobbyNetworkMode.Steam;
            Changed = null;
        }

        public static bool SteamCompiled
        {
            get
            {
#if STEAMWORKS_NET
                return true;
#else
                return false;
#endif
            }
        }

        public static bool SteamAvailable => SteamCompiled && MultiplayerTransport.SteamRunning;

        public static LobbyNetworkMode Preferred =>
            PlayerPrefs.GetInt(MODE_KEY, (int)LobbyNetworkMode.Steam) == (int)LobbyNetworkMode.Lan
                ? LobbyNetworkMode.Lan
                : LobbyNetworkMode.Steam;

        public static LobbyNetworkMode Chosen => _sessionChoice ?? Preferred;

        public static LobbyNetworkMode Resolve(LobbyNetworkMode chosen, bool steamAvailable) =>
            steamAvailable ? chosen : LobbyNetworkMode.Lan;

        public static LobbyNetworkMode Other(LobbyNetworkMode mode) =>
            mode == LobbyNetworkMode.Steam ? LobbyNetworkMode.Lan : LobbyNetworkMode.Steam;

        // Steam starts with the menu scene, after this runs: the saved choice is taken as it is until Refresh.
        internal static void Boot()
        {
            if (LobbyServiceLocator.IsConfigured) return;
            Configure(SteamCompiled ? Chosen : LobbyNetworkMode.Lan);
        }

        public static void Refresh()
        {
            if (InRoom) return;
            Configure(Resolve(Chosen, SteamAvailable));
        }

        public static void Prefer(LobbyNetworkMode mode)
        {
            PlayerPrefs.SetInt(MODE_KEY, (int)mode);
            PlayerPrefs.Save();
            _sessionChoice = null;
            Refresh();
        }

        // This session only: nothing is saved (editor test rigs share the PlayerPrefs with the builds).
        public static void Use(LobbyNetworkMode mode)
        {
            _sessionChoice = mode;
            Refresh();
        }

        private static bool InRoom
        {
            get
            {
                var manager = InstanceFinder.NetworkManager;
                return manager != null && (manager.IsServerStarted || manager.IsClientStarted);
            }
        }

        private static void Configure(LobbyNetworkMode mode)
        {
            if (mode != LobbyNetworkMode.Steam || !TrySteam(out ILobbyTransport transport, out IPlayerIdentityProvider identity))
            {
                mode = LobbyNetworkMode.Lan;
                _lanTransport ??= new TugboatLobbyTransport();
                _lanIdentity ??= new TugboatPlayerIdentityProvider();
                transport = _lanTransport;
                identity = _lanIdentity;
            }

            bool changed = mode != Mode || !ReferenceEquals(LobbyServiceLocator.Transport, transport);
            Mode = mode;
            LobbyServiceLocator.Configure(transport, identity);
            if (changed) Changed?.Invoke();
        }

        private static bool TrySteam(out ILobbyTransport transport, out IPlayerIdentityProvider identity)
        {
#if STEAMWORKS_NET
            _steamTransport ??= new SteamLobbyTransport();
            _steamIdentity ??= new SteamPlayerIdentityProvider();
            transport = _steamTransport;
            identity = _steamIdentity;
            return true;
#else
            transport = null;
            identity = null;
            return false;
#endif
        }
    }
}
