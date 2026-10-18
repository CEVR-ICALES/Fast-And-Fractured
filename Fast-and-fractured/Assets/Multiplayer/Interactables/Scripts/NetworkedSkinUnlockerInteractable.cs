using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Server: has the connection that owns the car write the unlock to its own PlayerPrefs (OnInteract only runs on
    /// the server, which would credit the host).
    /// </summary>
    public class NetworkedSkinUnlockerInteractable : ServerAuthoritativeWrapper<SkinUnlockerInteractable>
    {
        private const float UNLOCK_EVENT_SECONDS = 2f;

        protected override void OnConfigured()
        {
            base.OnConfigured();

            if (base.IsServerInitialized && Wrapped != null)
                Wrapped.InteractionRouter = RouteInteraction;
        }

        private bool RouteInteraction(GameObject interactionFrom)
        {
            if (interactionFrom == null) return false;

            // Inactive included: the car's NetworkObject can be an inactive parent while it spawns.
            NetworkObject nob = interactionFrom.GetComponentInParent<NetworkObject>(true);
            if (nob == null || nob.Owner == null || !nob.Owner.IsValid) return false;

            TargetCreditSkinUnlock(nob.Owner, Wrapped.SkinToUnlock);
            return true;
        }

        [TargetRpc]
        private void TargetCreditSkinUnlock(NetworkConnection connection, string skinToUnlock)
        {
            PlayerPrefs.SetInt(skinToUnlock, PlayerPrefs.GetInt(skinToUnlock) + 1);

            if (IngameEventsManager.HasInstance)
                IngameEventsManager.Instance.CreateEvent("Events.SkinUnlock", UNLOCK_EVENT_SECONDS);
        }
    }
}
