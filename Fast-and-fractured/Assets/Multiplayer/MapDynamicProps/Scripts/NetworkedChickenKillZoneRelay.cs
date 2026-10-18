using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Relay on the exact kill-trigger object of the Mega Chicken, so other boss colliders never count as kills.</summary>
    [AddComponentMenu("")]
    public sealed class NetworkedChickenKillZoneRelay : MonoBehaviour
    {
        private NetworkedMegaChickenBoss _owner;

        internal void Configure(NetworkedMegaChickenBoss owner)
        {
            _owner = owner;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_owner != null)
                _owner.HandleKillZoneTrigger(other);
        }
    }
}
