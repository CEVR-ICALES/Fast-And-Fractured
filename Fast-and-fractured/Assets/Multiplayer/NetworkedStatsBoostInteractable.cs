using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Routes the stat boost through the server so SyncVar updates reach every client.</summary>
    public class NetworkedStatsBoostInteractable : ServerAuthoritativeWrapper<StatsBoostInteractable>
    {
    }
}
