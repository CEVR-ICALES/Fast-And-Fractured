using System.Collections.Generic;

namespace FastAndFractured.Multiplayer.HostMigration
{
    /// <summary>
    /// Pure rules of a migration (unit tested): who takes over, and which characters of the
    /// replayed start come back and who drives them.
    /// </summary>
    public static class HostMigrationRoster
    {
        /// <summary>The successor is the first remaining human in the order the host published.</summary>
        public static bool IsSuccessor(MatchSnapshotBroadcast snapshot, string localToken) =>
            snapshot.SuccessorTokens != null && snapshot.SuccessorTokens.Length > 0 &&
            snapshot.SuccessorTokens[0] == localToken;

        public struct Plan
        {
            /// <summary>Per connected human (in the given order): the character index it drives, or -1 (dead / unknown: spectates).</summary>
            public int[] HumanCharacterIndex;
            /// <summary>Surviving AI character indices, in spawn order.</summary>
            public List<int> AiCharacterIndices;
        }

        /// <summary>
        /// Humans are the first HumanTokens.Length characters of the replayed start, AIs the rest; a human alive in the
        /// snapshot gets its character back, anyone missing is left out.
        /// </summary>
        public static Plan Build(MatchSnapshotBroadcast snapshot, IList<string> connectedTokens)
        {
            int humanCount = snapshot.HumanTokens != null ? snapshot.HumanTokens.Length : 0;
            var alive = new HashSet<int>();
            if (snapshot.Characters != null)
                foreach (var c in snapshot.Characters)
                    if (c.Alive) alive.Add(c.OriginalIndex);

            var plan = new Plan
            {
                HumanCharacterIndex = new int[connectedTokens.Count],
                AiCharacterIndices = new List<int>(),
            };
            for (int i = 0; i < connectedTokens.Count; i++)
            {
                int index = System.Array.IndexOf(snapshot.HumanTokens ?? new string[0], connectedTokens[i]);
                plan.HumanCharacterIndex[i] = index >= 0 && alive.Contains(index) ? index : -1;
            }
            for (int index = humanCount; index < snapshot.TotalCharacters; index++)
                if (alive.Contains(index)) plan.AiCharacterIndices.Add(index);
            return plan;
        }
    }
}
