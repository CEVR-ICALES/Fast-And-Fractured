using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    [CreateAssetMenu(menuName = "Fast And Fractured/Multiplayer/Availability Config", fileName = "MultiplayerAvailabilityConfig")]
    public sealed class MultiplayerAvailabilityConfig : ScriptableObject
    {
        [Tooltip("Off = the menu's multiplayer buttons flash red and do not open the multiplayer screens.")]
        [SerializeField] private bool multiplayerEnabled = true;

        public bool MultiplayerEnabled { get => multiplayerEnabled; set => multiplayerEnabled = value; }
    }
}
