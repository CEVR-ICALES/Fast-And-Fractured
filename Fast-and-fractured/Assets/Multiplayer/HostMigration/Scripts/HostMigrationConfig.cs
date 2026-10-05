using UnityEngine;

namespace FastAndFractured.Multiplayer.HostMigration
{
    /// <summary>Asset behind HostMigrationSettings (Resources/HostMigrationConfig).</summary>
    [CreateAssetMenu(menuName = "Fast And Fractured/Multiplayer/Host Migration Config", fileName = "HostMigrationConfig")]
    public sealed class HostMigrationConfig : ScriptableObject
    {
        [Tooltip("Continue the match on another player when the host leaves or crashes. Off = the match ends for everyone, as before.")]
        [SerializeField] private bool enabled;

        [Tooltip("Seconds between match snapshots sent by the host.")]
        [SerializeField] private float snapshotInterval = 0.5f;

        [Tooltip("Seconds the remaining players keep trying to reach the new host before giving up.")]
        [SerializeField] private float reconnectTimeout = 20f;

        public bool Enabled { get => enabled; set => enabled = value; }
        public float SnapshotInterval => Mathf.Max(0.1f, snapshotInterval);
        public float ReconnectTimeout => Mathf.Max(5f, reconnectTimeout);
    }
}
