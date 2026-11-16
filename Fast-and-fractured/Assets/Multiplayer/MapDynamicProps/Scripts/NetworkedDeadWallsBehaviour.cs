using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;
using Utilities;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Server-authoritative map boundary walls: the singleplayer DeadWallsBehaivour is removed on every peer and
    /// the server runs the kill countdown, killing through the networked death pipeline.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkedDeadWallsBehaviour : NetworkBehaviour
    {
        private const float DEFAULT_KILL_TIME = 1.5f;
        private const float OUTSIDE_MAP_EVENT_SECONDS = 2f;

        private DeadWallsBehaivour[] _walls;
        private readonly List<Collider> _collidersToRestore = new List<Collider>();

        private readonly Dictionary<NetworkedStatsController, ITimer> _activeKillCountdowns
            = new Dictionary<NetworkedStatsController, ITimer>();
        private readonly Dictionary<NetworkedStatsController, HashSet<long>> _activeContacts
            = new Dictionary<NetworkedStatsController, HashSet<long>>();

        private void Awake()
        {
            // The NetworkObject is on one child of a wall group: relay every legacy wall under the root.
            Transform wallsRoot = transform.parent != null ? transform.parent : transform;
            _walls = wallsRoot.GetComponentsInChildren<DeadWallsBehaivour>(true);

            foreach (var wall in _walls)
            {
                if (wall == null) continue;
                var relay = wall.GetComponent<NetworkedDeadWallRelay>();
                if (relay == null)
                    relay = wall.gameObject.AddComponent<NetworkedDeadWallRelay>();
                relay.Configure(this, wall);

                // Unity calls physics callbacks on disabled components: collider off until the legacy wall is gone.
                foreach (var wallCollider in wall.GetComponents<Collider>())
                {
                    if (wallCollider == null || !wallCollider.enabled || _collidersToRestore.Contains(wallCollider))
                        continue;

                    wallCollider.enabled = false;
                    _collidersToRestore.Add(wallCollider);
                }

                Destroy(wall);
            }
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            RestoreWallColliders();
        }

        private void RestoreWallColliders()
        {
            if (_collidersToRestore.Count == 0) return;

            foreach (var wallCollider in _collidersToRestore)
                if (wallCollider != null)
                    wallCollider.enabled = true;
            _collidersToRestore.Clear();
            MpCanary.Mark($"DeadWalls colliders restored on '{gameObject.name}'");
        }

        internal void HandleWallEnter(NetworkedDeadWallRelay wall, Collider other, int relayId)
        {
            if (!base.IsServerInitialized || other == null) return;
            if (HandleGolfBall(other, entered: true)) return;

            // As DeadWallsBehaivour: only the car body collider, never while kinematic (placing or respawning).
            if (!other.TryGetComponent(out StatsController _)) return;

            var stats = FindNetworkedStats(other);
            if (stats == null) return;

            Rigidbody body = other.attachedRigidbody != null ? other.attachedRigidbody : other.GetComponent<Rigidbody>();
            bool kinematic = body == null || body.isKinematic;
            Debug.Log($"[NetworkedDeadWalls] '{other.name}' entered wall '{(wall != null ? wall.name : "?")}' " +
                      $"kinematic={kinematic} pos={other.transform.position} t={Time.timeSinceLevelLoad:F2}" +
                      (kinematic ? " -> ignored" : " -> kill countdown"));
            if (kinematic) return;

            if (!_activeContacts.TryGetValue(stats, out var contacts))
            {
                contacts = new HashSet<long>();
                _activeContacts.Add(stats, contacts);
            }

            long contactKey = MakeContactKey(relayId, other.GetInstanceID());
            if (!contacts.Add(contactKey)) return;
            if (_activeKillCountdowns.ContainsKey(stats)) return;

            float killTime = wall != null ? wall.KillTime : DEFAULT_KILL_TIME;
            _activeKillCountdowns[stats] = MatchTimers.After(killTime, () => OnKillCountdownComplete(stats));

            if (stats.Owner != null && stats.Owner.IsValid)
                RpcNotifyOutsideMap(stats.Owner, true);
        }

        internal void HandleWallExit(NetworkedDeadWallRelay wall, Collider other, int relayId)
        {
            if (!base.IsServerInitialized || other == null) return;
            if (HandleGolfBall(other, entered: false)) return;
            if (!other.TryGetComponent(out StatsController _)) return;

            var stats = FindNetworkedStats(other);
            if (stats == null) return;

            if (_activeContacts.TryGetValue(stats, out var contacts))
            {
                if (!contacts.Remove(MakeContactKey(relayId, other.GetInstanceID())))
                    return;
                if (contacts.Count > 0) return;
                _activeContacts.Remove(stats);
            }
            else
            {
                return;
            }

            // Only cancel when the car escapes toward the play area.
            if (wall != null && !wall.IsCharacterInFront(stats.transform)) return;

            CancelCountdown(stats);

            if (stats.Owner != null && stats.Owner.IsValid)
                RpcNotifyOutsideMap(stats.Owner, false);
        }

        /// <summary>Server: golf balls leaving the map go back to their start, as the legacy walls do.</summary>
        private static bool HandleGolfBall(Collider other, bool entered)
        {
            if (!other.TryGetComponent(out GolfBallBehaviour ball)) return false;
            Rigidbody body = other.attachedRigidbody != null ? other.attachedRigidbody : other.GetComponent<Rigidbody>();
            if (body == null || body.isKinematic) return true;
            try
            {
                if (entered) ball.OutOfBoundsEvent();
                else ball.CancelOutOfBoundsEvent();
            }
            catch (KeyNotFoundException)
            {
                // GolfBallSpawnHandle throws for a ball already moved away (same in singleplayer).
            }
            return true;
        }

        private void OnKillCountdownComplete(NetworkedStatsController stats)
        {
            if (this == null || !base.IsServerInitialized) return;

            bool wasActive = _activeKillCountdowns.Remove(stats);
            _activeContacts.Remove(stats);
            if (wasActive && stats != null)
                stats.KillFromEnvironmentServer();
        }

        private void CancelCountdown(NetworkedStatsController stats)
        {
            if (_activeKillCountdowns.TryGetValue(stats, out var countdown))
            {
                MatchTimers.Stop(ref countdown);
                _activeKillCountdowns.Remove(stats);
            }
        }

        private static long MakeContactKey(int relayId, int colliderId)
        {
            return ((long)(uint)relayId << 32) | (uint)colliderId;
        }

        [TargetRpc]
        private void RpcNotifyOutsideMap(FishNet.Connection.NetworkConnection conn, bool entered)
        {
            var eventsManager = IngameEventsManager.ExistingInstance;
            if (entered && eventsManager != null)
                eventsManager.CreateEvent("Events.OutsideMap", OUTSIDE_MAP_EVENT_SECONDS);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            foreach (var countdown in _activeKillCountdowns.Values)
            {
                var timer = countdown;
                MatchTimers.Stop(ref timer);
            }
            _activeKillCountdowns.Clear();
            _activeContacts.Clear();
        }

        private static NetworkedStatsController FindNetworkedStats(Collider other)
        {
            var stats = other.GetComponentInParent<NetworkedStatsController>();
            if (stats == null) stats = other.GetComponentInChildren<NetworkedStatsController>();
            return stats;
        }
    }
}
