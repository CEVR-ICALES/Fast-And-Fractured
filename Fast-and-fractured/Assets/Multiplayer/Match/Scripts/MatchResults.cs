using System.Collections.Generic;
using System.Linq;

namespace FastAndFractured.Multiplayer
{
    /// <summary>One row of the end-of-match table (sent by the server to every client).</summary>
    public struct MatchResultEntry
    {
        public int Placement;          // 1 = winner
        public string DisplayName;     // lobby name, or the character for an AI
        public string CharacterCode;   // e.g. "Carme_0" (icon + character name)
        public bool IsHuman;
        public int OwnerClientId;      // -1 for AIs
        public int Kills;
        public int DamageDealt;
        public bool Survived;
        public string EliminatedBy;    // empty when they survived or nobody is credited
    }

    /// <summary>
    /// End-of-match results for the multiplayer win/lose screen: your placement and the whole
    /// table, as in other battle royales. Built on the server from the deaths in the order they
    /// happened; kept here (static) because the map is unloaded before the menu shows them.
    /// </summary>
    public static class MatchResults
    {
        public struct Participant
        {
            public int Id;
            public string DisplayName;
            public string CharacterCode;
            public bool IsHuman;
            public int OwnerClientId;
            public int DamageDealt;
        }

        public struct Death
        {
            public int VictimId;
            public int KillerId;   // -1 when nobody is credited (storm, fall, AI)
        }

        /// <summary>Results of the last multiplayer match, waiting for the win/lose screen.</summary>
        public static MatchResultEntry[] Latest { get; private set; }
        public static int LocalClientId { get; private set; } = -1;

        public static void Store(MatchResultEntry[] entries, int localClientId)
        {
            Latest = entries;
            LocalClientId = localClientId;
        }

        public static void Clear()
        {
            Latest = null;
            LocalClientId = -1;
        }

        public static bool TryGetLocal(out MatchResultEntry entry)
        {
            entry = default;
            if (Latest == null || LocalClientId < 0) return false;
            foreach (var e in Latest)
                if (e.IsHuman && e.OwnerClientId == LocalClientId) { entry = e; return true; }
            return false;
        }

        /// <summary>
        /// Placement: the winner first, then anyone else still alive, then the dead from the
        /// last to fall to the first. Kills are the deaths credited to each participant.
        /// </summary>
        public static MatchResultEntry[] Build(IList<Participant> participants, IList<Death> deathsInOrder, int winnerId)
        {
            var byId = participants.ToDictionary(p => p.Id);
            var deathIndex = new Dictionary<int, int>();
            var killerOf = new Dictionary<int, int>();
            for (int i = 0; i < deathsInOrder.Count; i++)
            {
                var d = deathsInOrder[i];
                if (deathIndex.ContainsKey(d.VictimId)) continue;
                deathIndex[d.VictimId] = i;
                killerOf[d.VictimId] = d.KillerId;
            }

            var ordered = participants
                .OrderBy(p => p.Id == winnerId ? 0 : deathIndex.ContainsKey(p.Id) ? 2 : 1)
                .ThenByDescending(p => deathIndex.TryGetValue(p.Id, out int i) ? i : int.MaxValue)
                .ThenBy(p => p.Id)
                .ToList();

            var entries = new MatchResultEntry[ordered.Count];
            for (int i = 0; i < ordered.Count; i++)
            {
                var p = ordered[i];
                bool died = deathIndex.ContainsKey(p.Id);
                string eliminatedBy = string.Empty;
                if (died && killerOf.TryGetValue(p.Id, out int killer) && killer != p.Id && byId.TryGetValue(killer, out var k))
                    eliminatedBy = k.DisplayName;
                entries[i] = new MatchResultEntry
                {
                    Placement = i + 1,
                    DisplayName = p.DisplayName,
                    CharacterCode = p.CharacterCode,
                    IsHuman = p.IsHuman,
                    OwnerClientId = p.OwnerClientId,
                    Kills = deathsInOrder.Count(d => d.KillerId == p.Id && d.VictimId != p.Id),
                    DamageDealt = p.DamageDealt,
                    Survived = !died,
                    EliminatedBy = eliminatedBy,
                };
            }
            return entries;
        }

        /// <summary>"Carme_0" → "Carme".</summary>
        public static string CharacterName(string code)
        {
            if (string.IsNullOrEmpty(code)) return string.Empty;
            int underscore = code.IndexOf('_');
            return underscore > 0 ? code.Substring(0, underscore) : code;
        }
    }
}
