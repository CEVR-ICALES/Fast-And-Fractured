using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Runtime-only relay installed on the exact GameObject that owns the Mega Chicken
    /// kill trigger. Keeping the callback on the collider object prevents unrelated boss
    /// colliders from being interpreted as kill-zone contacts.
    /// </summary>
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
