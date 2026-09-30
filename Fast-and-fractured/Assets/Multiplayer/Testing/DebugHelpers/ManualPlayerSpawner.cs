using FishNet.Managing;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Fallback manual player spawner for remote clients.
    /// Use this if FishNet's PlayerSpawner component doesn't work for remote clients.
    /// Attach to NetworkManager GameObject.
    /// </summary>
    public class ManualPlayerSpawner : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Enable manual spawning (disable if PlayerSpawner works)")]
        public bool enableManualSpawn = false;
        
        [Tooltip("DummyPlayer prefab to spawn")]
        public GameObject playerPrefab;
        
        [Header("Debug")]
        public bool verboseLogging = true;
        
        private NetworkManager _networkManager;
        
        private void Awake()
        {
            _networkManager = GetComponent<NetworkManager>();
            if (_networkManager == null)
            {
                Debug.LogError("[ManualPlayerSpawner] NetworkManager not found!");
                enabled = false;
                return;
            }
            
            // Disable FishNet PlayerSpawner to avoid conflicts
            var fishNetSpawner = GetComponent<FishNet.Component.Spawning.PlayerSpawner>();
            if (fishNetSpawner != null && enableManualSpawn)
            {
                fishNetSpawner.enabled = false;
                Debug.Log("<color=cyan>[ManualPlayerSpawner] Disabled FishNet PlayerSpawner to prevent conflicts</color>");
            }
            
            // Auto-find DummyPlayer if not assigned
            if (playerPrefab == null)
            {
                playerPrefab = FindDummyPlayerPrefab();
            }
            
            if (playerPrefab == null)
            {
                Debug.LogError("[ManualPlayerSpawner] Player Prefab not assigned and could not be found!");
                enabled = false;
                return;
            }
        }
        
        private GameObject FindDummyPlayerPrefab()
        {
#if UNITY_EDITOR
            // In editor, search assets
            var guids = UnityEditor.AssetDatabase.FindAssets("DummyPlayer t:Prefab");
            if (guids.Length > 0)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Debug.Log($"[ManualPlayerSpawner] Auto-found DummyPlayer at: {path}");
                return prefab;
            }
#endif
            
            // Try Resources folder
            var resourcePrefab = Resources.Load<GameObject>("DummyPlayer");
            if (resourcePrefab != null)
            {
                Debug.Log("[ManualPlayerSpawner] Found DummyPlayer in Resources");
                return resourcePrefab;
            }
            
            return null;
        }
        
        private void OnEnable()
        {
            if (_networkManager != null && enableManualSpawn)
            {
                _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
                
                if (verboseLogging)
                    Debug.Log("<color=cyan>[ManualPlayerSpawner] Manual spawning ENABLED</color>");
            }
        }
        
        private void OnDisable()
        {
            if (_networkManager != null)
            {
                _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            }
        }
        
        private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            // Only spawn when client fully connects
            if (args.ConnectionState != RemoteConnectionState.Started)
                return;
            
            if (verboseLogging)
                Debug.Log($"<color=yellow>[ManualPlayerSpawner] Client {conn.ClientId} connected. Spawning player...</color>");
            
            // Instantiate player prefab
            GameObject playerInstance = Instantiate(playerPrefab);
            playerInstance.name = $"DummyPlayer_Client{conn.ClientId}";
            
            // Get NetworkObject
            NetworkObject nob = playerInstance.GetComponent<NetworkObject>();
            if (nob == null)
            {
                Debug.LogError($"<color=red>[ManualPlayerSpawner] Player prefab missing NetworkObject component!</color>");
                Destroy(playerInstance);
                return;
            }
            
            // Spawn for specific client (sets ownership)
            _networkManager.ServerManager.Spawn(playerInstance, conn);
            
            if (verboseLogging)
                Debug.Log($"<color=green>[ManualPlayerSpawner] [OK]Spawned {playerInstance.name} for client {conn.ClientId}</color>");
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-find player prefab if not set
            if (playerPrefab == null)
            {
                // Try to find DummyPlayer in project
                var guids = UnityEditor.AssetDatabase.FindAssets("DummyPlayer t:Prefab");
                if (guids.Length > 0)
                {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Debug.Log($"[ManualPlayerSpawner] Auto-found DummyPlayer prefab at: {path}");
                }
            }
        }
#endif
    }
}
