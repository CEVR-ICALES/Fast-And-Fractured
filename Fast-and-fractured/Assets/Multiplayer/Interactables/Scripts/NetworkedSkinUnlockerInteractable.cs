using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Routes the skin unlock through the server so the picking player's own PlayerPrefs (and
    /// toast) update - not the host's.
    ///
    /// SkinUnlockerInteractable is destroyed on clients by the wrapper, so OnInteract only ever
    /// runs on the server, and PlayerPrefs.SetInt would silently credit the unlock to whichever
    /// machine is hosting instead of the player who actually drove over the pickup. The server
    /// looks up who owns the car that touched it and has that connection write its own PlayerPrefs.
    /// </summary>
    public class NetworkedSkinUnlockerInteractable : ServerAuthoritativeWrapper<SkinUnlockerInteractable>
    {
        protected override void OnConfigured()
        {
            base.OnConfigured();

            if (base.IsServerInitialized && Wrapped != null)
                Wrapped.InteractionRouter = RouteInteraction;
        }

        private bool RouteInteraction(GameObject interactionFrom)
        {
            if (interactionFrom == null) return false;

            // (true): the car's NetworkObject can be an inactive parent while FishNet is still
            // spawning it, and the active-only overload would return null.
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
                IngameEventsManager.Instance.CreateEvent("Events.SkinUnlock", 2f);
        }
    }
}
