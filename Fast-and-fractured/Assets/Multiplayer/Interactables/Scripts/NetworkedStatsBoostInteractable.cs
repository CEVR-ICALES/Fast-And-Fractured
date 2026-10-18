using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Routes the stat boost through the server so SyncVar updates reach every client.</summary>
    public class NetworkedStatsBoostInteractable : ServerAuthoritativeWrapper<StatsBoostInteractable>
    {
        /// <summary>
        /// Server: tells only the connection that owns the car to raise the pickup event (OnInteract only runs on the
        /// server, where LocalPlayer is the host's car).
        /// </summary>
        protected override void OnConfigured()
        {
            base.OnConfigured();

            if (base.IsServerInitialized && Wrapped != null)
                Wrapped.IngameEventRouter = RouteIngameEvent;
        }

        private bool RouteIngameEvent(GameObject interactionFrom)
        {
            if (interactionFrom == null) return false;

            // Inactive included: the car's NetworkObject can be an inactive parent while it spawns.
            NetworkObject nob = interactionFrom.GetComponentInParent<NetworkObject>(true);
            if (nob == null || nob.Owner == null || !nob.Owner.IsValid) return false;

            TargetShowPickupEvent(nob.Owner, Wrapped.ingameEventText, Wrapped.ingameEventTimeOnScreen);
            return true;
        }

        [TargetRpc]
        private void TargetShowPickupEvent(NetworkConnection connection, string text, float timeOnScreen)
        {
            if (!IngameEventsManager.HasInstance) return;

            IngameEventsManager.Instance.CreateEvent(text, timeOnScreen);
        }
    }
}
