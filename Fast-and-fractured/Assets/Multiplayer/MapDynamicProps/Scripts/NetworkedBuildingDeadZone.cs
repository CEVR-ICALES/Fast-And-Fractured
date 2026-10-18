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

            // Replaces the legacy kill on every peer: removed in Awake, before any trigger.
            RemoveWrappedBehaviour();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!base.IsServerInitialized) return;

            // As BuildingDeadZone: only the car body collider (the one with the StatsController).
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
