using FishNet.Object;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Marker on the player root: enables PlayerInputController only for the owner.</summary>
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
