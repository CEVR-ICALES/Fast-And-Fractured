using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Runtime-only physics endpoint for the networked sandstorm. The relay lives on a
    /// dedicated child GameObject, so Unity never sends its trigger messages to the legacy
    /// SandstormController that remains on the parent for geometry and visual updates.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class NetworkedSandstormHitboxRelay : MonoBehaviour
    {
        private NetworkedSandstormController _owner;

        internal void Configure(NetworkedSandstormController owner)
        {
            _owner = owner;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_owner != null)
                _owner.HandleStormTriggerEnter(other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (_owner != null)
                _owner.HandleStormTriggerExit(other);
        }
    }
}
