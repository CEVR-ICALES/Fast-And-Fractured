using FishNet.Object;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    public class NetworkedSuckingBuilding : ServerAuthoritativeWrapper<SuckingBuilding>
    {
        protected override bool KeepWrappedOnClients => true;

        protected override void OnConfigured()
        {
            if (Wrapped != null)
                Wrapped.SetPullFilter(ShouldPullOnThisPeer);
        }

        private bool ShouldPullOnThisPeer(GameObject character)
        {
            var nob = character != null ? character.GetComponentInParent<NetworkObject>() : null;
            return ShouldPull(base.IsServerInitialized, nob != null && nob.IsOwner);
        }

        internal static bool ShouldPull(bool isServer, bool isLocallyOwned) => isServer || isLocallyOwned;
    }
}
