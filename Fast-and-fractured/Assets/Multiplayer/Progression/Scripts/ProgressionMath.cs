using UnityEngine;

namespace FastAndFractured.Multiplayer.Progression
{
    /// <summary>Pure rules of the levelling system (unit tested): the XP curve and what a match is worth.</summary>
    public static class ProgressionMath
    {
        public const int MAX_LEVEL = 50;
        private const int BASE_XP_PER_LEVEL = 200;
        private const int EXTRA_XP_PER_LEVEL = 50;

        // Match XP
        private const int XP_FOR_PLAYING = 50;
        private const int XP_FOR_WINNING = 150;
        private const int XP_FOR_BEST_PLACEMENT = 120;   // scaled down to 0 for last place
        private const int XP_PER_KILL = 40;
        private const int XP_PER_100_DAMAGE = 10;
        private const int MAX_DAMAGE_XP = 100;

        /// <summary>XP needed to go from <paramref name="level"/> to the next one.</summary>
        public static int XpToNextLevel(int level) =>
            BASE_XP_PER_LEVEL + EXTRA_XP_PER_LEVEL * (Mathf.Clamp(level, 1, MAX_LEVEL) - 1);

        /// <summary>Level reached with this much XP in total (level 1 at 0 XP).</summary>
        public static int LevelForTotalXp(int totalXp)
        {
            int level = 1;
            int remaining = Mathf.Max(0, totalXp);
            while (level < MAX_LEVEL && remaining >= XpToNextLevel(level))
            {
                remaining -= XpToNextLevel(level);
                level++;
            }
            return level;
        }

        /// <summary>Total XP at which <paramref name="level"/> starts.</summary>
        public static int TotalXpForLevel(int level)
        {
            int total = 0;
            for (int l = 1; l < Mathf.Clamp(level, 1, MAX_LEVEL); l++) total += XpToNextLevel(l);
            return total;
        }

        /// <summary>
        /// XP for one match from its results: something for playing, a placement bonus that goes
        /// from the full amount for 1st to nothing for last, kills, damage (capped) and the win.
        /// </summary>
        public static int MatchXp(int placement, int participants, int kills, int damageDealt)
        {
            int xp = XP_FOR_PLAYING;
            if (participants > 1)
            {
                float rank = Mathf.Clamp01((participants - Mathf.Clamp(placement, 1, participants)) / (float)(participants - 1));
                xp += Mathf.RoundToInt(XP_FOR_BEST_PLACEMENT * rank);
            }
            if (placement == 1) xp += XP_FOR_WINNING;
            xp += Mathf.Max(0, kills) * XP_PER_KILL;
            xp += Mathf.Min(MAX_DAMAGE_XP, Mathf.Max(0, damageDealt) / 100 * XP_PER_100_DAMAGE);
            return xp;
        }
    }
}
