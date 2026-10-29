using UnityEngine;

namespace FastAndFractured.Multiplayer.HostMigration
{
    /// <summary>Asset behind HostMigrationSettings (Resources/HostMigrationConfig).</summary>
    [CreateAssetMenu(menuName = "Fast And Fractured/Multiplayer/Host Migration Config", fileName = "HostMigrationConfig")]
    public sealed class HostMigrationConfig : ScriptableObject
    {
        private const float MIN_SNAPSHOT_INTERVAL = 0.1f;
        private const float MIN_RECONNECT_TIMEOUT = 5f;

        [Tooltip("Continue the match on another player when the host leaves or crashes. Off = the match ends for everyone, as before.")]
        [SerializeField] private bool enabled;

        [Tooltip("Seconds between match snapshots sent by the host.")]
        [SerializeField] private float snapshotInterval = 0.5f;

        [Tooltip("Seconds the remaining players keep trying to reach the new host before giving up.")]
        [SerializeField] private float reconnectTimeout = 20f;

        [Tooltip("Keep the room when its host leaves: another player takes it over and everyone rejoins. Off = everyone goes back to the multiplayer hub.")]
        [SerializeField] private bool roomMigration = true;

        public bool Enabled { get => enabled; set => enabled = value; }
        public bool RoomMigration { get => roomMigration; set => roomMigration = value; }
        public float SnapshotInterval => Mathf.Max(MIN_SNAPSHOT_INTERVAL, snapshotInterval);
        public float ReconnectTimeout => Mathf.Max(MIN_RECONNECT_TIMEOUT, reconnectTimeout);
    }
}
