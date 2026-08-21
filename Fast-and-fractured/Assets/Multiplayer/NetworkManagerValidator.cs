using UnityEngine;
using FishNet.Managing;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Helper script to validate NetworkManager configuration.
    /// Attach to NetworkManager GameObject.
    /// </summary>
    public class NetworkManagerValidator : MonoBehaviour
    {
        [Header("Expected Configuration")]
        [Tooltip("DummyPlayer prefab that should spawn for each connecting client")]
        public GameObject expectedPlayerPrefab;
        
        private void Start()
        {
            var networkManager = GetComponent<NetworkManager>();
            if (networkManager == null)
            {
                Debug.LogError("[NetworkManagerValidator] No NetworkManager found on this GameObject!");
                return;
            }
            
            ValidatePlayerPrefab(networkManager);
        }
        
        private void ValidatePlayerPrefab(NetworkManager nm)
        {
            // Check if player prefab is set
            var playerPrefab = nm.SpawnablePrefabs.GetObject(false,0);
            
            if (playerPrefab == null)
            {
                Debug.LogError("<color=red>[NetworkManagerValidator] ❁ENO PLAYER PREFAB SET!</color>");
                Debug.LogError("<color=red>Fix: NetworkManager ->Inspector ->Spawnable Prefabs ->Player Prefab ->Set to DummyPlayer</color>");
                return;
            }
            
            Debug.Log($"<color=cyan>[NetworkManagerValidator] [OK]Player Prefab set: {playerPrefab.name}</color>");
            
            if (expectedPlayerPrefab != null && playerPrefab != expectedPlayerPrefab.gameObject)
            {
                Debug.LogWarning($"<color=yellow>[NetworkManagerValidator] ⚠ Player prefab mismatch!</color>");
                Debug.LogWarning($"  Expected: {expectedPlayerPrefab.name}");
                Debug.LogWarning($"  Actual: {playerPrefab.name}");
            }
            
            // Validate DummyPlayer has required components
            var networkObject = playerPrefab.GetComponent<FishNet.Object.NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"<color=red>[NetworkManagerValidator] ❁E{playerPrefab.name} missing NetworkObject component!</color>");
            }
            else
            {
                Debug.Log($"<color=cyan>[NetworkManagerValidator] [OK]{playerPrefab.name} has NetworkObject</color>");
            }
            
            var debugSelector = playerPrefab.GetComponent<DebugCharacterSelector>();
            if (debugSelector == null)
            {
                Debug.LogWarning($"<color=yellow>[NetworkManagerValidator] ⚠ {playerPrefab.name} missing DebugCharacterSelector!</color>");
            }
            else
            {
                Debug.Log($"<color=cyan>[NetworkManagerValidator] [OK]{playerPrefab.name} has DebugCharacterSelector</color>");
            }
            
            var autoSelector = playerPrefab.GetComponent<AutoCharacterSelector>();
            if (autoSelector == null)
            {
                Debug.LogWarning($"<color=yellow>[NetworkManagerValidator] ⚠ {playerPrefab.name} missing AutoCharacterSelector (optional)</color>");
            }
            else
            {
                Debug.Log($"<color=cyan>[NetworkManagerValidator] [OK]{playerPrefab.name} has AutoCharacterSelector</color>");
                Debug.Log($"  Auto-select enabled: {autoSelector.enabled}");
            }
        }
    }
}
