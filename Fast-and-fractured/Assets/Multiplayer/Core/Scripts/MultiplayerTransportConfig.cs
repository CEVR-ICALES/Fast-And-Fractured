using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    [CreateAssetMenu(menuName = "Fast And Fractured/Multiplayer/Transport Config", fileName = "MultiplayerTransportConfig")]
    public sealed class MultiplayerTransportConfig : ScriptableObject
    {
        [Tooltip("Steam rooms connect through Steam's relay (FishySteamworks): works across the internet without opening ports. " +
                 "Off = every connection goes straight to the host's IP (Tugboat), which only works on the same network.")]
        [SerializeField] private bool useSteamRelay = true;

        public bool UseSteamRelay { get => useSteamRelay; set => useSteamRelay = value; }
    }
}
