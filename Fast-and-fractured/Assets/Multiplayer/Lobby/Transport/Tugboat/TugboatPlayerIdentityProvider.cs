using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Abstractions.Tugboat
{
    public sealed class TugboatPlayerIdentity : IPlayerIdentity
    {
        public string DisplayName { get; }
        public string PlatformId { get; }

        public TugboatPlayerIdentity(string displayName, string platformId)
        {
            DisplayName = displayName;
            PlatformId = platformId;
        }

        public Task<Texture2D> GetAvatarAsync(CancellationToken ct = default)
        {
            var tex = Resources.Load<Texture2D>("UI/DefaultAvatar");
            return Task.FromResult(tex);
        }
    }

    public sealed class TugboatPlayerIdentityProvider : IPlayerIdentityProvider
    {
        private static string _cachedPlatformId;

        private static string GetOrCreatePlatformId()
        {
            if (_cachedPlatformId != null) return _cachedPlatformId;

            const string key = "LAN_PlatformId";
            if (!PlayerPrefs.HasKey(key))
                PlayerPrefs.SetString(key, Guid.NewGuid().ToString("N").Substring(0, 8));

            _cachedPlatformId = $"{Environment.UserName}_{PlayerPrefs.GetString(key)}";
            return _cachedPlatformId;
        }

        public Task<IPlayerIdentity> GetLocalIdentityAsync(CancellationToken ct = default)
        {
            IPlayerIdentity identity = new TugboatPlayerIdentity(
                Environment.UserName,
                GetOrCreatePlatformId());
            return Task.FromResult(identity);
        }

        public Task<IPlayerIdentity> GetIdentityAsync(string platformId, CancellationToken ct = default)
        {
            string displayName = !string.IsNullOrEmpty(platformId) && platformId.Contains("_")
                ? platformId.Substring(0, platformId.LastIndexOf('_'))
                : platformId ?? "Player";

            IPlayerIdentity identity = new TugboatPlayerIdentity(displayName, platformId ?? string.Empty);
            return Task.FromResult(identity);
        }
    }
}
