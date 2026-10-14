using System;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured.Progression
{
    /// <summary>
    /// The local player's profile: total XP (and so the level), the rewards unlocked so far and
    /// the accessory equipped per character and slot. Saved in PlayerPrefs as JSON.
    ///
    /// XP comes from multiplayer matches (AwardMatch, from the match results); every level
    /// reached unlocks the rewards of ProgressionRewards for that level.
    /// </summary>
    public static class PlayerProgression
    {
        private const string PREFS_KEY = "MP_Progression";

        [Serializable]
        private sealed class Profile
        {
            public int totalXp;
            public List<string> unlocked = new List<string>();
            public List<string> equipped = new List<string>();   // "Character|Slot|rewardId"
            public string lastAwardedMatch;
        }

        /// <summary>What the last match gave (for the results screen).</summary>
        public struct Award
        {
            public int Xp;
            public int TotalBefore;
            public int TotalAfter;
            public int LevelBefore;
            public int LevelAfter;
            public List<ProgressionReward> NewRewards;
        }

        public static event Action OnChanged;

        private static Profile _profile;
        public static Award? LastAward { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _profile = null;
            LastAward = null;
            OnChanged = null;
        }

        private static Profile Data
        {
            get
            {
                if (_profile != null) return _profile;
                string json = PlayerPrefs.GetString(PREFS_KEY, string.Empty);
                try { _profile = string.IsNullOrEmpty(json) ? new Profile() : JsonUtility.FromJson<Profile>(json); }
                catch { _profile = null; }
                if (_profile == null) _profile = new Profile();
                return _profile;
            }
        }

        private static void Save()
        {
            PlayerPrefs.SetString(PREFS_KEY, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        public static int TotalXp => Data.totalXp;
        public static int Level => ProgressionMath.LevelForTotalXp(Data.totalXp);
        public static int XpIntoLevel => Data.totalXp - ProgressionMath.TotalXpForLevel(Level);
        public static int XpForThisLevel => ProgressionMath.XpToNextLevel(Level);
        public static bool IsMaxLevel => Level >= ProgressionMath.MAX_LEVEL;

        public static bool IsUnlocked(string rewardId) => Data.unlocked.Contains(rewardId);

        /// <summary>Unlocked reward ids and equipped entries ("Character|Slot|rewardId"), read-only (telemetry).</summary>
        public static IReadOnlyList<string> UnlockedIds => Data.unlocked;
        public static IReadOnlyList<string> EquippedEntries => Data.equipped;

        /// <summary>
        /// Grants the XP of one finished match, once (the same results are not counted twice),
        /// and unlocks the rewards of every level reached.
        /// </summary>
        public static Award? AwardMatch(string matchKey, int xp)
        {
            if (xp <= 0 || (!string.IsNullOrEmpty(matchKey) && matchKey == Data.lastAwardedMatch)) return null;
            int before = Level;
            int totalBefore = Data.totalXp;
            Data.totalXp += xp;
            Data.lastAwardedMatch = matchKey;
            int after = Level;

            var newRewards = new List<ProgressionReward>();
            foreach (var reward in ProgressionRewards.Load().rewards)
                if (reward != null && reward.level <= after && !Data.unlocked.Contains(reward.id))
                {
                    Data.unlocked.Add(reward.id);
                    newRewards.Add(reward);
                }
            Save();
            LastAward = new Award { Xp = xp, TotalBefore = totalBefore, TotalAfter = Data.totalXp, LevelBefore = before, LevelAfter = after, NewRewards = newRewards };
            return LastAward;
        }

        // -- Accessories ------------------------------------------------------------------
        public static string Equipped(string character, AccessorySlot slot)
        {
            string prefix = $"{character}|{slot}|";
            foreach (var entry in Data.equipped)
                if (entry.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return entry.Substring(prefix.Length);
            return null;
        }

        /// <summary>Equips an unlocked accessory (null to take the slot off).</summary>
        public static void Equip(string character, AccessorySlot slot, string rewardId)
        {
            string prefix = $"{character}|{slot}|";
            Data.equipped.RemoveAll(e => e.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(rewardId) && IsUnlocked(rewardId)) Data.equipped.Add(prefix + rewardId);
            Save();
        }

        /// <summary>Editor / tests: wipe the local profile.</summary>
        public static void ResetProfile()
        {
            _profile = new Profile();
            LastAward = null;
            Save();
        }
    }
}
