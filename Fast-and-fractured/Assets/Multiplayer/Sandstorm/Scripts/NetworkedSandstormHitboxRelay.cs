using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// The networked sandstorm's trigger endpoint, on its own child so the legacy SandstormController on the parent
    /// never gets the trigger messages.
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
