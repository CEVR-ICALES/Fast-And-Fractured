using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Server-authoritative wrapper for DeadWallsBehaivour (map boundary kill zones).
    ///
    /// The singleplayer component calls StatsController.GetKilledNotify which runs a kill
    /// countdown coroutine locally — bypassing the networked death pipeline entirely.
    /// In multiplayer each client would independently kill its own view of the vehicle,
    /// producing desync and double-deaths.
    ///
    /// This wrapper removes the singleplayer component on ALL peers and replicates
    /// the KillTime countdown exclusively on the server, applying lethal damage through
    /// DealDamageServerRpc so the full networked death pipeline fires (kill feed, spectator
    /// transition, character-count tracking). The "OutsideMap" UI event is broadcast to the
    /// affected client via RpcNotifyOutsideMap.
    ///
    /// Setup: add this component and a NetworkObject to the DeadWalls GameObject in the scene.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkedDeadWallsBehaviour : NetworkBehaviour
    {
        private DeadWallsBehaivour[] _walls;
        private readonly List<Collider> _collidersToRestore = new List<Collider>();

        private readonly Dictionary<NetworkedStatsController, Coroutine> _activeKillCountdowns
            = new Dictionary<NetworkedStatsController, Coroutine>();
        private readonly Dictionary<NetworkedStatsController, HashSet<long>> _activeContacts
            = new Dictionary<NetworkedStatsController, HashSet<long>>();

        private void Awake()
        {
            // The networked variants wrap groups (Construction has 63 wall colliders) while
            // the NetworkObject lives on only one child. Install a lightweight relay on every
            // legacy wall beneath the prefab root so none keep running local kill timers.
            Transform wallsRoot = transform.parent != null ? transform.parent : transform;
            _walls = wallsRoot.GetComponentsInChildren<DeadWallsBehaivour>(true);

            foreach (var wall in _walls)
            {
                if (wall == null) continue;
                var relay = wall.GetComponent<NetworkedDeadWallRelay>();
                if (relay == null)
                    relay = wall.gameObject.AddComponent<NetworkedDeadWallRelay>();
                relay.Configure(this, wall);

                // Unity invokes physics callbacks on disabled MonoBehaviours. Temporarily turn
                // off the wall collider, destroy the legacy component, then restore the collider
                // for the relay on the next frame. This keeps the fix entirely in Multiplayer.
                foreach (var wallCollider in wall.GetComponents<Collider>())
                {
                    if (wallCollider == null || !wallCollider.enabled || _collidersToRestore.Contains(wallCollider))
                        continue;

                    wallCollider.enabled = false;
                    _collidersToRestore.Add(wallCollider);
                }

                Destroy(wall);
            }

            StartCoroutine(RestoreWallCollidersNextFrame());
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // Safety net for the collider restore. FishNet deactivates networked scene objects
            // between Awake and their spawn, which kills the RestoreWallCollidersNextFrame
            // coroutine started in Awake (a coroutine on a deactivated GameObject is stopped, not
            // paused, and Awake never runs again to restart it). When that happens the wall
            // trigger colliders stay disabled for the whole match and NOTHING can die to the map
            // boundary or pits. OnStartNetwork runs after the object is spawned and active again,
            // so restoring here guarantees the relays receive their trigger callbacks.
            RestoreWallColliders();
        }

        private IEnumerator RestoreWallCollidersNextFrame()
        {
            yield return null;
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

            // Same filter as the singleplayer wall (DeadWallsBehaivour.OnTriggerEnter): only the
            // car body collider - the one carrying the StatsController - and never while its body
            // is kinematic, which is how a car being placed or teleported (spawn, respawn) avoids
            // starting a kill timer. Without it any child collider of the car counted, kinematic
            // or not.
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

            float killTime = wall != null ? wall.KillTime : 1.5f;
            var coroutine = StartCoroutine(KillCountdown(stats, killTime));
            _activeKillCountdowns[stats] = coroutine;

            // Notify the owner client to show the "outside map" warning UI.
            if (stats.Owner != null && stats.Owner.IsValid)
                RpcNotifyOutsideMap(stats.Owner, true);
        }

        internal void HandleWallExit(NetworkedDeadWallRelay wall, Collider other, int relayId)
        {
            if (!base.IsServerInitialized || other == null) return;
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

            // Only cancel the countdown if the car is escaping toward the play area.
            // IsCharacterInFront checks the wall's captured orientation against the exit direction.
            if (wall != null && !wall.IsCharacterInFront(stats.transform)) return;

            CancelCountdown(stats);

            if (stats.Owner != null && stats.Owner.IsValid)
                RpcNotifyOutsideMap(stats.Owner, false);
        }

        private IEnumerator KillCountdown(NetworkedStatsController stats, float delay)
        {
            yield return new WaitForSeconds(delay);

            bool wasActive = _activeKillCountdowns.Remove(stats);
            _activeContacts.Remove(stats);
            if (wasActive && stats != null)
                stats.KillFromEnvironmentServer();
        }

        private void CancelCountdown(NetworkedStatsController stats)
        {
            if (_activeKillCountdowns.TryGetValue(stats, out var coroutine))
            {
                if (coroutine != null) StopCoroutine(coroutine);
                _activeKillCountdowns.Remove(stats);
            }
        }

        private static long MakeContactKey(int relayId, int colliderId)
        {
            return ((long)(uint)relayId << 32) | (uint)colliderId;
        }

        // Sends the "outside map" UI notification to the specific player client only.
        [TargetRpc]
        private void RpcNotifyOutsideMap(FishNet.Connection.NetworkConnection conn, bool entered)
        {
            var eventsManager = IngameEventsManager.ExistingInstance;
            if (entered && eventsManager != null)
                eventsManager.CreateEvent("Events.OutsideMap", 2f);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            foreach (var coroutine in _activeKillCountdowns.Values)
                if (coroutine != null) StopCoroutine(coroutine);
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
