using FishNet.Object;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Marker component placed on the player root NetworkObject. Toggles the local
    /// PlayerInputController based on ownership so only the owner reads input.
    /// Vehicle spawning and lifecycle are handled by NetworkedLevelControllerManager,
    /// not by this component.
    /// </summary>
    public class NetworkedPlayer : NetworkBehaviour
    {
        public override void OnStartServer()
        {
            base.OnStartServer();
            if (FindFirstObjectByType<NetworkedLevelControllerManager>() == null &&
                LevelController.Instance == null)
            {
                Debug.LogError("[NetworkedPlayer] Neither NetworkedLevelControllerManager nor LevelController found on server.");
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            var inputController = GetComponent<PlayerInputController>();
            if (inputController != null)
                inputController.enabled = base.IsOwner;
        }
    }
}
