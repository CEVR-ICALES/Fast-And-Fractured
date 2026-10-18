using System.Collections.Generic;
using System.Linq;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Death and alive bookkeeping of a networked match, free of Unity and FishNet so it can be unit tested.</summary>
    public class MatchDeathTracker
    {
        public enum Outcome
        {
            /// <summary>Match continues.</summary>
            None,
            /// <summary>Exactly one character remains: the winner.</summary>
            Winner,
            /// <summary>No characters remain.</summary>
            Draw
        }

        public readonly struct DeathResult
        {
            /// <summary>False when the victim was unknown or already dead (nothing changed).</summary>
            public readonly bool Applied;
            public readonly Outcome MatchOutcome;
            /// <summary>ObjectId of the winner when MatchOutcome == Winner; otherwise -1.</summary>
            public readonly int WinnerObjectId;

            public DeathResult(bool applied, Outcome outcome, int winnerObjectId)
            {
                Applied = applied;
                MatchOutcome = outcome;
                WinnerObjectId = winnerObjectId;
            }

            public static readonly DeathResult NotApplied = new DeathResult(false, Outcome.None, -1);
        }

        private readonly Dictionary<int, bool> _aliveById = new Dictionary<int, bool>();
        private readonly Dictionary<int, int> _characterByClient = new Dictionary<int, int>();

        public int AliveCount { get; private set; }
        public int TrackedCount => _aliveById.Count;
        public int PlayerCount => _characterByClient.Count;

        public void Reset()
        {
            _aliveById.Clear();
            _characterByClient.Clear();
            AliveCount = 0;
        }

        /// <summary>Registers a character (idempotent); pass the owning client id for players, none for AI.</summary>
        public void RegisterCharacter(int objectId, int? ownerClientId = null)
        {
            if (!_aliveById.ContainsKey(objectId))
            {
                _aliveById[objectId] = true;
                AliveCount++;
            }

            if (ownerClientId.HasValue)
                _characterByClient[ownerClientId.Value] = objectId;
        }

        public bool IsKnown(int objectId) => _aliveById.ContainsKey(objectId);

        public bool IsAlive(int objectId) => _aliveById.TryGetValue(objectId, out bool alive) && alive;

        public bool TryGetCharacterForClient(int clientId, out int objectId)
            => _characterByClient.TryGetValue(clientId, out objectId);

        /// <summary>Drops the client→character mapping (character tracking is unaffected).</summary>
        public void ForgetClient(int clientId) => _characterByClient.Remove(clientId);

        /// <summary>Marks the victim dead; unknown or already dead victims change nothing (Applied false).</summary>
        public DeathResult ReportDeath(int victimObjectId)
        {
            if (!IsAlive(victimObjectId))
                return DeathResult.NotApplied;

            _aliveById[victimObjectId] = false;
            AliveCount--;

            if (AliveCount == 1)
                return new DeathResult(true, Outcome.Winner, FirstAliveObjectId());
            if (AliveCount <= 0)
                return new DeathResult(true, Outcome.Draw, -1);
            return new DeathResult(true, Outcome.None, -1);
        }

        /// <summary>ObjectId of the first alive character, or -1 when none remain.</summary>
        public int FirstAliveObjectId()
        {
            foreach (var kvp in _aliveById)
                if (kvp.Value)
                    return kvp.Key;
            return -1;
        }

        /// <summary>True while at least one human-controlled character is still alive.</summary>
        public bool AnyPlayerAlive => _characterByClient.Values.Any(IsAlive);

        public IEnumerable<int> AliveObjectIds => _aliveById.Where(kvp => kvp.Value).Select(kvp => kvp.Key);

        /// <summary>Compact debug description for error logs ("3 tracked, 2 alive: [12+, 15+, 18-]").</summary>
        public string DescribeTracked()
        {
            var ids = string.Join(", ", _aliveById.Select(kvp => $"{kvp.Key}{(kvp.Value ? "+" : "-")}"));
            return $"{TrackedCount} tracked, {AliveCount} alive: [{ids}]";
        }
    }
}
