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
        private const string ID_KEY = "LAN_PlatformId";
        private const int ID_LENGTH = 8;
        private const int NAME_TAG_LENGTH = 4;
        private const char ID_SEPARATOR = '_';
        private const string GENERIC_PLAYER = "Player";
        // Every Steam Deck and every Proton prefix logs in with the same account name.
        private static readonly string[] SHARED_ACCOUNT_NAMES = { "steamuser", "deck" };

        private static string _cachedName;
        private static string _cachedPlatformId;

        private static string LocalId()
        {
            if (!PlayerPrefs.HasKey(ID_KEY))
                PlayerPrefs.SetString(ID_KEY, Guid.NewGuid().ToString("N").Substring(0, ID_LENGTH));
            return PlayerPrefs.GetString(ID_KEY);
        }

        private static void EnsureLocal()
        {
            string name = LocalName(MultiplayerTransport.SteamPersonaName, Environment.UserName, LocalId());
            if (name == _cachedName && _cachedPlatformId != null) return;
            _cachedName = name;
            _cachedPlatformId = name + ID_SEPARATOR + LocalId();
        }

        public static string LocalName(string steamName, string accountName, string id)
        {
            if (!string.IsNullOrWhiteSpace(steamName)) return steamName.Trim();
            bool shared = string.IsNullOrWhiteSpace(accountName);
            foreach (string name in SHARED_ACCOUNT_NAMES)
                shared |= string.Equals(accountName, name, StringComparison.OrdinalIgnoreCase);
            if (!shared) return accountName;
            string tag = string.IsNullOrEmpty(id) ? string.Empty : id.Substring(0, Mathf.Min(NAME_TAG_LENGTH, id.Length));
            return GENERIC_PLAYER + "-" + tag;
        }

        public Task<IPlayerIdentity> GetLocalIdentityAsync(CancellationToken ct = default)
        {
            EnsureLocal();
            IPlayerIdentity identity = new TugboatPlayerIdentity(_cachedName, _cachedPlatformId);
            return Task.FromResult(identity);
        }

        public Task<IPlayerIdentity> GetIdentityAsync(string platformId, CancellationToken ct = default)
        {
            string displayName = !string.IsNullOrEmpty(platformId) && platformId.IndexOf(ID_SEPARATOR) >= 0
                ? platformId.Substring(0, platformId.LastIndexOf(ID_SEPARATOR))
                : platformId ?? GENERIC_PLAYER;

            IPlayerIdentity identity = new TugboatPlayerIdentity(displayName, platformId ?? string.Empty);
            return Task.FromResult(identity);
        }
    }
}
