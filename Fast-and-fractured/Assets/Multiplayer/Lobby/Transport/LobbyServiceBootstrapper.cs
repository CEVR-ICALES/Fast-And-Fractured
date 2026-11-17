using FastAndFractured.Multiplayer.Abstractions.Tugboat;
#if STEAMWORKS_NET
using FastAndFractured.Multiplayer.Abstractions.Steam;
#endif
using UnityEngine;

namespace FastAndFractured.Multiplayer.Abstractions
{
    public static class LobbyServiceBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (LobbyServiceLocator.IsConfigured) return;

#if STEAMWORKS_NET
            LobbyServiceLocator.Configure(
                new SteamLobbyTransport(),
                new SteamPlayerIdentityProvider());
#else
            LobbyServiceLocator.Configure(
                new TugboatLobbyTransport(),
                new TugboatPlayerIdentityProvider());
#endif
        }
    }
}
