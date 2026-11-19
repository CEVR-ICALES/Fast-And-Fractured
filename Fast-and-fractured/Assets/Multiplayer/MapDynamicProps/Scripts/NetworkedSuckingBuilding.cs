using FishNet.Object;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    public class NetworkedSuckingBuilding : ServerAuthoritativeWrapper<SuckingBuilding>
    {
        protected override bool KeepWrappedOnClients => true;

        private ParabolicImpulse _launcher;

        protected override void OnConfigured()
        {
            if (Wrapped != null)
                Wrapped.SetPullFilter(ShouldPullOnThisPeer);

            // The launcher inside the building runs like the pull: the owner predicts its car's launch.
            _launcher = GetComponentInChildren<ParabolicImpulse>(true);
            if (_launcher == null) return;
            _launcher.SetImpulseFilter(ShouldLaunchOnThisPeer);
            if (base.IsServerInitialized)
                _launcher.onImpulse.AddListener(OnServerLaunch);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (_launcher != null)
                _launcher.onImpulse.RemoveListener(OnServerLaunch);
        }

        private bool ShouldPullOnThisPeer(GameObject character) =>
            ShouldPull(base.IsServerInitialized, IsLocallyOwned(character));

        private bool ShouldLaunchOnThisPeer(GameObject target) =>
            ShouldLaunch(base.IsServerInitialized, target != null && target.TryGetComponent(out PhysicsBehaviour _), IsLocallyOwned(target));

        private static bool IsLocallyOwned(GameObject target)
        {
            var nob = target != null ? target.GetComponentInParent<NetworkObject>() : null;
            return nob != null && nob.IsOwner;
        }

        internal static bool ShouldPull(bool isServer, bool isLocallyOwned) => isServer || isLocallyOwned;

        // Props the launcher can throw (golf balls) are moved by the server alone.
        internal static bool ShouldLaunch(bool isServer, bool isCar, bool isLocallyOwned) => isServer || (isCar && isLocallyOwned);

        private void OnServerLaunch(GameObject target)
        {
            var nob = target != null ? target.GetComponentInParent<NetworkObject>() : null;
            if (nob != null)
                RpcLaunched(nob);
        }

        // Peers that did not launch the car still drop it from the pull and play the burst.
        [ObserversRpc(ExcludeServer = true)]
        private void RpcLaunched(NetworkObject target)
        {
            if (target == null || _launcher == null) return;
            var body = target.GetComponentInChildren<StatsController>(true);
            if (body != null)
                _launcher.onImpulse.Invoke(body.gameObject);
        }
    }
}
