using UnityEngine;
using FishNet.Managing;

namespace FastAndFractured.Multiplayer
{
    public class NetworkManagerValidator : MonoBehaviour
    {
        [Header("Expected Configuration")]
        [Tooltip("Player prefab that should spawn for each connecting client")]
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
            var playerPrefab = nm.SpawnablePrefabs.GetObject(false,0);
            
            if (playerPrefab == null)
            {
                Debug.LogError("<color=red>[NetworkManagerValidator] ❁ENO PLAYER PREFAB SET!</color>");
                Debug.LogError("<color=red>Fix: NetworkManager ->Inspector ->Spawnable Prefabs ->Player Prefab ->Set to NetworkedPlayer</color>");
                return;
            }
            
            Debug.Log($"<color=cyan>[NetworkManagerValidator] [OK]Player Prefab set: {playerPrefab.name}</color>");
            
            if (expectedPlayerPrefab != null && playerPrefab != expectedPlayerPrefab.gameObject)
            {
                Debug.LogWarning($"<color=yellow>[NetworkManagerValidator] ⚠ Player prefab mismatch!</color>");
                Debug.LogWarning($"  Expected: {expectedPlayerPrefab.name}");
                Debug.LogWarning($"  Actual: {playerPrefab.name}");
            }
            
            var networkObject = playerPrefab.GetComponent<FishNet.Object.NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"<color=red>[NetworkManagerValidator] ❁E{playerPrefab.name} missing NetworkObject component!</color>");
            }
            else
            {
                Debug.Log($"<color=cyan>[NetworkManagerValidator] [OK]{playerPrefab.name} has NetworkObject</color>");
            }
        }
    }
}
