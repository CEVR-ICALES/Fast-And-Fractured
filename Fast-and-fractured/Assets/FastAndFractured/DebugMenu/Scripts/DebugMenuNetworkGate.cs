using FishNet.Managing;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Testing
{
    public static class DebugMenuNetworkGate
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register() => DebugCheats.IsNetworkSession = IsNetworkSession;

        public static bool IsNetworkSession()
        {
            if (NetworkManager.Instances.Count == 0) return false;
            NetworkManager manager = NetworkManager.Instances[0];
            return IsSession(manager != null, manager != null && manager.IsServerStarted, manager != null && manager.IsClientStarted);
        }

        public static bool IsSession(bool hasManager, bool serverStarted, bool clientStarted) =>
            hasManager && (serverStarted || clientStarted);
    }
}
