using FishNet;
using UnityEngine;

namespace FastAndFractured.Multiplayer.HostMigration
{
    /// <summary>
    /// Server: sends the match snapshot to every client while the match runs. Added by
    /// NetworkedLevelControllerManager only when host migration is enabled.
    /// </summary>
    public sealed class HostMigrationSnapshotSender : MonoBehaviour
    {
        private NetworkedLevelControllerManager _manager;
        private float _interval = 0.5f;
        private float _nextSend;

        public void Initialize(NetworkedLevelControllerManager manager)
        {
            _manager = manager;
            var config = Resources.Load<HostMigrationConfig>(HostMigrationSettings.CONFIG_RESOURCES_PATH);
            if (config != null) _interval = config.SnapshotInterval;
        }

        private void Update()
        {
            if (_manager == null || !InstanceFinder.IsServerStarted || Time.unscaledTime < _nextSend) return;
            _nextSend = Time.unscaledTime + _interval;
            if (_manager.TryBuildMigrationSnapshot(out MatchSnapshotBroadcast snapshot))
                InstanceFinder.ServerManager.Broadcast(snapshot);
        }
    }
}
