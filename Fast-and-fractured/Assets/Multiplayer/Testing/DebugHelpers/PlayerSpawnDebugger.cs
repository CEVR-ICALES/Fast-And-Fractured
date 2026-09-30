using FishNet.Managing;
using FishNet.Connection;
using FishNet.Transporting;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Debug script to track player spawning events.
    /// Attach to NetworkManager GameObject.
    /// </summary>
    public class PlayerSpawnDebugger : MonoBehaviour
    {
        private NetworkManager _networkManager;
        
        private void Awake()
        {
            _networkManager = GetComponent<NetworkManager>();
            if (_networkManager == null)
            {
                Debug.LogError("[PlayerSpawnDebugger] NetworkManager not found!");
                enabled = false;
                return;
            }
        }
        
        private void OnEnable()
        {
            if (_networkManager != null)
            {
                _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
                _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
                _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            }
        }
        
        private void OnDisable()
        {
            if (_networkManager != null)
            {
                _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
                _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
                _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            }
        }
        
        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            Debug.Log($"<color=magenta>[PlayerSpawnDebugger] SERVER STATE: {args.ConnectionState}</color>");
        }
        
        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            Debug.Log($"<color=cyan>[PlayerSpawnDebugger] CLIENT STATE: {args.ConnectionState}</color>");
        }
        
        private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            Debug.Log($"<color=yellow>[PlayerSpawnDebugger] REMOTE CLIENT {conn.ClientId} - State: {args.ConnectionState}</color>");
            
            if (args.ConnectionState == RemoteConnectionState.Started)
            {
                Debug.Log($"<color=yellow>[PlayerSpawnDebugger] Client {conn.ClientId} fully connected. Checking for player spawn...</color>");
                
                // Wait a bit then check if player was spawned
                StartCoroutine(CheckPlayerSpawned(conn));
            }
        }
        
        private System.Collections.IEnumerator CheckPlayerSpawned(NetworkConnection conn)
        {
            // Connections intentionally own no NetworkObjects while they are in the lobby.
            // Wait for the match transition before diagnosing the character spawner; the old
            // fixed 0.5s delay reported every player who spent more than half a second readying up
            // as a spawn failure.
            while (conn != null && conn.IsActive)
            {
                var lobby = FindFirstObjectByType<NetworkedLobbyManager>();
                if (lobby != null && lobby.GameStarted.Value)
                    break;

                yield return null;
            }

            if (conn == null || !conn.IsActive)
                yield break;

            const float SPAWN_TIMEOUT = 15f;
            float elapsed = 0f;
            while (conn.Objects.Count == 0 && conn.IsActive && elapsed < SPAWN_TIMEOUT)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // A completed/aborted match may already have returned to the menu and despawned its
            // cars. That is not evidence that the original spawn failed.
            if (!conn.IsActive ||
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex == LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX)
                yield break;

            var objects = conn.Objects;
            if (objects.Count == 0)
            {
                Debug.LogError($"<color=red>[PlayerSpawnDebugger] Client {conn.ClientId} has no owned NetworkObjects " +
                               $"{SPAWN_TIMEOUT:F0}s after the match started.</color>");
                yield break;
            }

            Debug.Log($"<color=green>[PlayerSpawnDebugger] [OK]Client {conn.ClientId} has {objects.Count} NetworkObject(s):</color>");
            foreach (var obj in objects)
            {
                Debug.Log($"<color=green>  - {obj.name} (ObjectId: {obj.ObjectId})</color>");
            }
        }
    }
}
