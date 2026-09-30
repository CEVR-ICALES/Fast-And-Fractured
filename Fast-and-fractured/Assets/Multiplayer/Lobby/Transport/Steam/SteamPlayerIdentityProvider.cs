#if STEAMWORKS_NET
using System.Threading;
using System.Threading.Tasks;
using Steamworks;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Abstractions.Steam
{
    public sealed class SteamPlayerIdentity : IPlayerIdentity
    {
        public string DisplayName { get; }
        public string PlatformId { get; }

        private readonly ulong _steamId;

        public SteamPlayerIdentity(string displayName, ulong steamId)
        {
            DisplayName = displayName;
            PlatformId  = steamId.ToString();
            _steamId    = steamId;
        }

        public Task<Texture2D> GetAvatarAsync(CancellationToken ct = default)
        {
            // Steam avatar loading is async; return a default for now.
            // Full implementation would use SteamFriends.GetMediumFriendAvatar + texture conversion.
            var tex = Resources.Load<Texture2D>("UI/DefaultAvatar");
            return Task.FromResult(tex);
        }
    }

    public sealed class SteamPlayerIdentityProvider : IPlayerIdentityProvider
    {
        public Task<IPlayerIdentity> GetLocalIdentityAsync(CancellationToken ct = default)
        {
            if (!SteamManager.Initialized)
                return Task.FromResult<IPlayerIdentity>(new SteamPlayerIdentity("Player", 0));

            string name    = SteamFriends.GetPersonaName();
            ulong  steamId = SteamUser.GetSteamID().m_SteamID;
            return Task.FromResult<IPlayerIdentity>(new SteamPlayerIdentity(name, steamId));
        }

        public Task<IPlayerIdentity> GetIdentityAsync(string platformId, CancellationToken ct = default)
        {
            if (!ulong.TryParse(platformId, out ulong steamId))
                return Task.FromResult<IPlayerIdentity>(new SteamPlayerIdentity(platformId ?? "Player", 0));

            string name = SteamFriends.GetFriendPersonaName(new CSteamID(steamId));
            if (string.IsNullOrEmpty(name)) name = $"Player_{steamId}";

            return Task.FromResult<IPlayerIdentity>(new SteamPlayerIdentity(name, steamId));
        }
    }
}
#endif
