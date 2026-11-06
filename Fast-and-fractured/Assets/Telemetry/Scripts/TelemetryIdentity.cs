using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
#if STEAMWORKS_NET
using FastAndFractured.Platform;
using Steamworks;
#endif

namespace FastAndFractured.GameTelemetry
{
    /// <summary>
    /// Who is playing: the salted hash of the Steam ID, plus an install id when Steam is not running (also GA4's
    /// client id).
    /// </summary>
    public static class TelemetryIdentity
    {
        private const string CLIENT_ID_KEY = "Telemetry_ClientId";
        private const int HASH_BYTES = 16;

        /// <summary>Random id of this installation, kept in PlayerPrefs.</summary>
        public static string ClientId
        {
            get
            {
                string id = PlayerPrefs.GetString(CLIENT_ID_KEY, string.Empty);
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString(CLIENT_ID_KEY, id);
                    PlayerPrefs.Save();
                }
                return id;
            }
        }

        /// <summary>The hashed Steam ID, or null when Steam is not up (yet).</summary>
        public static string SteamHash
        {
            get
            {
#if STEAMWORKS_NET
                try
                {
                    if (!SteamManager.Initialized) return null;
                    return HashSteamId(SteamUser.GetSteamID().m_SteamID);
                }
                catch { return null; }   // Steam missing or shut down: no id rather than a crash
#else
                return null;
#endif
            }
        }

        public static string HashSteamId(ulong steamId)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(TelemetryConfig.STEAM_ID_SALT + ":" + steamId));
            var text = new StringBuilder(HASH_BYTES * 2);
            for (int i = 0; i < HASH_BYTES; i++) text.Append(hash[i].ToString("x2"));
            return text.ToString();
        }
    }
}
