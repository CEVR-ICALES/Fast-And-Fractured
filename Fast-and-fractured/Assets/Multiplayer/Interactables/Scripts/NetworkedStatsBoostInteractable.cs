using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Routes the stat boost through the server so SyncVar updates reach every client.</summary>
    public class NetworkedStatsBoostInteractable : ServerAuthoritativeWrapper<StatsBoostInteractable>
    {
        /// <summary>
        /// The pickup message used to land on the wrong screen: it appeared on the host whoever had
        /// actually driven into the booster.
        ///
        /// StatsBoostInteractable is destroyed on clients by the wrapper, so OnInteract only ever
        /// runs on the server, and its "was it me?" test compares against
        /// LevelController.Instance.LocalPlayer - which on the server is the host's own car. That
        /// question cannot be answered locally once one machine resolves pickups for everybody.
        ///
        /// So the server answers it instead: it looks up who owns the car that touched the booster
        /// and tells only that connection to raise the event.
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

            // (true): the car's NetworkObject can be an inactive parent while FishNet is still
            // spawning it, and the active-only overload would return null.
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
