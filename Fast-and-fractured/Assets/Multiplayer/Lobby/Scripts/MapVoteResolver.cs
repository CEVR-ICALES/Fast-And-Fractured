using System;
using System.Collections.Generic;

namespace FastAndFractured.Multiplayer.Lobby
{
    public static class MapVoteResolver
    {
        /// <summary>
        /// Tallies votes and returns the winning mapId.
        /// Votes for mapIds not in <paramref name="validIds"/> are ignored.
        /// If no valid votes exist, or all are tied, uses <paramref name="rng"/> to pick randomly.
        /// </summary>
        public static int Resolve(IReadOnlyDictionary<int, int> votes, IReadOnlyList<int> validIds, Random rng)
        {
            if (validIds == null || validIds.Count == 0)
                throw new ArgumentException("validIds must not be empty.", nameof(validIds));

            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var tally = new Dictionary<int, int>();
            foreach (var id in validIds)
                tally[id] = 0;

            if (votes != null)
            {
                foreach (var kvp in votes)
                {
                    if (tally.ContainsKey(kvp.Value))
                        tally[kvp.Value]++;
                }
            }

            int maxVotes = 0;
            foreach (var count in tally.Values)
                if (count > maxVotes) maxVotes = count;

            var winners = new List<int>();
            foreach (var kvp in tally)
                if (kvp.Value == maxVotes) winners.Add(kvp.Key);

            return winners[rng.Next(winners.Count)];
        }
    }
}
