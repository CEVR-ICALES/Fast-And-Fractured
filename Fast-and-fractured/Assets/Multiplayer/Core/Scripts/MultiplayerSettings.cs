using System;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Player preferences for online play (Settings ▸ Multiplayer, MultiplayerSettingsTab).
    /// Stored in PlayerPrefs; read live by the systems they drive.
    /// </summary>
    public static class MultiplayerSettings
    {
        private const string KEY_PLAYER_NAMES = "MP_ShowPlayerNames";
        private const string KEY_BOT_NAMES = "MP_ShowBotNames";
        private const string KEY_NAME_DISTANCE = "MP_NameTagDistance";
        private const string KEY_NETWORK_STATS = "MP_ShowNetworkStats";
        private const string KEY_WILLING_HOST = "MP_WillingToHost";
        private const string KEY_PLAYER_LEVELS = "MP_ShowPlayerLevels";

        public const float MIN_NAME_DISTANCE = 40f;
        public const float MAX_NAME_DISTANCE = 200f;
        private const float DEFAULT_NAME_DISTANCE = 110f;

        /// <summary>Raised whenever one of the settings changes.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Changed = null;

        /// <summary>Other players' names above their cars.</summary>
        public static bool ShowPlayerNames
        {
            get => GetBool(KEY_PLAYER_NAMES, true);
            set => SetBool(KEY_PLAYER_NAMES, value);
        }

        /// <summary>Bots' names above their cars.</summary>
        public static bool ShowBotNames
        {
            get => GetBool(KEY_BOT_NAMES, true);
            set => SetBool(KEY_BOT_NAMES, value);
        }

        /// <summary>Distance (m) at which the names above the cars have faded out.</summary>
        public static float NameTagDistance
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(KEY_NAME_DISTANCE, DEFAULT_NAME_DISTANCE), MIN_NAME_DISTANCE, MAX_NAME_DISTANCE);
            set { PlayerPrefs.SetFloat(KEY_NAME_DISTANCE, Mathf.Clamp(value, MIN_NAME_DISTANCE, MAX_NAME_DISTANCE)); Saved(); }
        }

        /// <summary>The connection panel (ping, delay, rollback) shown in matches; F3 still toggles it.</summary>
        public static bool ShowNetworkStats
        {
            get => GetBool(KEY_NETWORK_STATS, true);
            set => SetBool(KEY_NETWORK_STATS, value);
        }

        /// <summary>Other players' progression level next to their name in the room.</summary>
        public static bool ShowPlayerLevels
        {
            get => GetBool(KEY_PLAYER_LEVELS, true);
            set => SetBool(KEY_PLAYER_LEVELS, value);
        }

        /// <summary>Take over the room when its host leaves (room migration). Off = others are picked first.</summary>
        public static bool WillingToHost
        {
            get => GetBool(KEY_WILLING_HOST, true);
            set => SetBool(KEY_WILLING_HOST, value);
        }

        private static bool GetBool(string key, bool fallback) => PlayerPrefs.GetInt(key, fallback ? 1 : 0) != 0;

        private static void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            Saved();
        }

        private static void Saved()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
