using UnityEngine;

namespace FastAndFractured.Multiplayer.Abstractions
{
    public static class LobbyLocator
    {
        private static NetworkedLobbyManager _found;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _found = null;

        public static NetworkedLobbyManager GetLobbyManager()
        {
            // Remembered while it stays active: a scene-wide search costs over a millisecond inside a map.
            if (_found != null && _found.gameObject.activeInHierarchy) return _found;

            var anyManager = Object.FindAnyObjectByType<NetworkedLobbyManager>();
            if (anyManager != null)
            {
                _found = anyManager;
                return anyManager;
            }

            var nm = Object.FindAnyObjectByType<FishNet.Managing.NetworkManager>();
            if (nm == null) return null;
            return nm.GetComponentInChildren<NetworkedLobbyManager>(true);
        }
    }
}
