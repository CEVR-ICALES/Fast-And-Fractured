using System;

namespace FastAndFractured.Multiplayer.Abstractions
{
    public static class LobbyServiceLocator
    {
        public static ILobbyTransport Transport { get; private set; }
        public static IPlayerIdentityProvider IdentityProvider { get; private set; }

        public static bool IsConfigured => Transport != null && IdentityProvider != null;

        public static void Configure(ILobbyTransport transport, IPlayerIdentityProvider identity)
        {
            Transport = transport ?? throw new ArgumentNullException(nameof(transport));
            IdentityProvider = identity ?? throw new ArgumentNullException(nameof(identity));
        }

        public static void Reset()
        {
            Transport = null;
            IdentityProvider = null;
        }
    }
}
