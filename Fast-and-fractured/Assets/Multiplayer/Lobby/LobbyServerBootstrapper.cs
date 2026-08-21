using FishNet;
using FishNet.Transporting;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Lobby
{
    /// <summary>
    /// Dynamically spawns the NetworkedLobbyManager from a Resources prefab when the server
    /// starts. Avoids the SceneId issues that scene-placed NetworkObjects hit when added by
    /// code (SceneId stays 0 → ServerRpc + SyncDict don't sync).
    /// Place on the same GameObject as the NetworkManager.
    /// </summary>
    public sealed class LobbyServerBootstrapper : MonoBehaviour
    {
        public const string PREFAB_RESOURCES_PATH = "LobbyManagerSpawnable";

        private GameObject _spawnedInstance;

        private void OnEnable()
        {
            var nm = InstanceFinder.NetworkManager;
            if (nm == null)
            {
                Debug.LogWarning("[LobbyServerBootstrapper] No NetworkManager at OnEnable.");
                return;
            }
            nm.ServerManager.OnServerConnectionState += OnServerConnectionState;

            if (InstanceFinder.IsServerStarted)
                TrySpawnLobbyManager();
        }

        private void OnDisable()
        {
            var nm = InstanceFinder.NetworkManager;
            if (nm != null) nm.ServerManager.OnServerConnectionState -= OnServerConnectionState;
        }

        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
                TrySpawnLobbyManager();
            else if (args.ConnectionState == LocalConnectionState.Stopped)
                _spawnedInstance = null;
        }

        private void TrySpawnLobbyManager()
        {
            if (_spawnedInstance != null) return;
            var existing = Object.FindAnyObjectByType<NetworkedLobbyManager>();
            if (existing != null && existing.IsSpawned)
            {
                _spawnedInstance = existing.gameObject;
                return;
            }

            var prefab = Resources.Load<GameObject>(PREFAB_RESOURCES_PATH);
            if (prefab == null)
            {
                Debug.LogError($"[LobbyServerBootstrapper] Resources/{PREFAB_RESOURCES_PATH} missing. " +
                               "Run Lobby Setup Wizard > SETUP EVERYTHING.");
                return;
            }

            _spawnedInstance = Instantiate(prefab);
            InstanceFinder.ServerManager.Spawn(_spawnedInstance);
        }
    }
}
