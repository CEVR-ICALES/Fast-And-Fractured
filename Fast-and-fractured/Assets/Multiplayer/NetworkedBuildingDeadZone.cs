using FishNet.Object;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Re-routes the kill through DealDamageServerRpc so the networked death pipeline fires.</summary>
    [RequireComponent(typeof(BuildingDeadZone))]
    public class NetworkedBuildingDeadZone : ServerAuthoritativeWrapper<BuildingDeadZone>
    {
        private NetworkedStatsController _lastKilled;

        protected override void Awake()
        {
            base.Awake();

            // This wrapper fully replaces the legacy kill on every peer, including the server.
            // Remove it during Awake so it cannot receive a trigger before FishNet starts.
            RemoveWrappedBehaviour();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!base.IsServerInitialized) return;

            // Same filter as the singleplayer BuildingDeadZone: only the car body collider (the
            // one carrying the StatsController), not wheels or trigger children like a shield.
            if (!other.TryGetComponent(out StatsController _)) return;

            var stats = other.GetComponentInParent<NetworkedStatsController>();
            if (stats == null) return;

            if (_lastKilled == stats) return;
            _lastKilled = stats;

            stats.KillFromEnvironmentServer();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!base.IsServerInitialized) return;
            var stats = other.GetComponentInParent<NetworkedStatsController>();
            if (stats != null && _lastKilled == stats)
                _lastKilled = null;
        }
    }
}
