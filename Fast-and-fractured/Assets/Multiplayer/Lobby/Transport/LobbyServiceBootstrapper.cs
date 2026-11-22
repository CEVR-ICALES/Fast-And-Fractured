using UnityEngine;

namespace FastAndFractured.Multiplayer.Abstractions
{
    public static class LobbyServiceBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap() => LobbyNetwork.Boot();
    }
}
