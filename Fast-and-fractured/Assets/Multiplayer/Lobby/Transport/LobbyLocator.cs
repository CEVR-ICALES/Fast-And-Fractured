using UnityEngine;

namespace FastAndFractured.Multiplayer.Abstractions
{
    public static class LobbyLocator
    {
        public static NetworkedLobbyManager GetLobbyManager()
        {
            var anyManager = Object.FindAnyObjectByType<NetworkedLobbyManager>();
            if (anyManager != null) return anyManager;

            var nm = Object.FindAnyObjectByType<FishNet.Managing.NetworkManager>();
            if (nm == null) return null;
            return nm.GetComponentInChildren<NetworkedLobbyManager>(true);
        }
    }
}
