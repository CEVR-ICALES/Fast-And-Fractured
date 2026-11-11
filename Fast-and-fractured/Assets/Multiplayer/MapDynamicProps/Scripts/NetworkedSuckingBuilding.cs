using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Runs the inward-pull only on the server; clients get the resulting velocity via Rigidbody prediction.</summary>
    public class NetworkedSuckingBuilding : ServerAuthoritativeWrapper<SuckingBuilding>
    {
    }
}
