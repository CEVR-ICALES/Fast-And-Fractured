using UnityEngine;
using FishNet.Managing;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Auto-configures NetworkManager with debugging and spawn fix tools on scene load.
    /// Attach this to ANY GameObject in the scene (or will auto-create one).
    /// </summary>
    [DefaultExecutionOrder(-32769)] // Run before FishNet NetworkManager Awake (-32768)
    public class AutoSceneSetup : MonoBehaviour
    {
        [Header("Auto-Setup Configuration")]
        [Tooltip("Add PlayerSpawnDebugger automatically")]
        [SerializeField] private bool addDebugger = true;
        
        [Tooltip("Add ManualPlayerSpawner automatically")]
        [SerializeField] private bool addManualSpawner = true;
        
        [Tooltip("Enable manual spawning by default (if PlayerSpawner fails)")]
        [SerializeField] private bool enableManualSpawnByDefault = true;
        
        [Tooltip("Reference to DummyPlayer prefab (auto-finds if null)")]
        [SerializeField] private GameObject dummyPlayerPrefab;
        
        [Header("Status")]
        [SerializeField] private bool setupCompleted = false;
        
        private void Awake()
        {
            // Always ensure NetworkManager is root before FishNet tries DontDestroyOnLoad.
            NetworkManager networkManager = FindFirstObjectByType<NetworkManager>();
            if (networkManager != null && networkManager.transform.parent != null)
            {
                string previousParentName = networkManager.transform.parent.name;
                networkManager.transform.SetParent(null, true);
                Debug.LogWarning($"<color=yellow>[AutoSceneSetup] NetworkManager was parented under '{previousParentName}'. Unparented in Awake so FishNet DontDestroyOnLoad can succeed.</color>");
            }

            if (setupCompleted)
            {
                Debug.Log("[AutoSceneSetup] Setup already completed, skipping.");
                return;
            }
            
            Debug.Log("<color=cyan>[AutoSceneSetup] Running auto-configuration...</color>");
            
            // Find NetworkManager
            if (networkManager == null)
            {
                Debug.LogError("<color=red>[AutoSceneSetup] NetworkManager not found in scene!</color>");
                return;
            }
            
            Debug.Log($"<color=green>[AutoSceneSetup] Found NetworkManager: {networkManager.name}</color>");
            
            // Find or load DummyPlayer prefab
            if (dummyPlayerPrefab == null)
            {
                dummyPlayerPrefab = FindDummyPlayerPrefab();
            }
            
            // Add PlayerSpawnDebugger
            if (addDebugger)
            {
                var debugger = networkManager.GetComponent<PlayerSpawnDebugger>();
                if (debugger == null)
                {
                    debugger = networkManager.gameObject.AddComponent<PlayerSpawnDebugger>();
                    Debug.Log("<color=green>[AutoSceneSetup] [OK]Added PlayerSpawnDebugger</color>");
                }
                else
                {
                    Debug.Log("<color=yellow>[AutoSceneSetup] PlayerSpawnDebugger already exists</color>");
                }
            }
            
            // Add ManualPlayerSpawner
            if (addManualSpawner)
            {
                var manualSpawner = networkManager.GetComponent<ManualPlayerSpawner>();
                if (manualSpawner == null)
                {
                    manualSpawner = networkManager.gameObject.AddComponent<ManualPlayerSpawner>();
                    Debug.Log("<color=green>[AutoSceneSetup] [OK]Added ManualPlayerSpawner</color>");
                    
                    // Configure it
                    manualSpawner.enableManualSpawn = enableManualSpawnByDefault;
                    manualSpawner.verboseLogging = true;
                    manualSpawner.playerPrefab = dummyPlayerPrefab;
                    
                    Debug.Log($"<color=green>[AutoSceneSetup] [OK]Configured ManualPlayerSpawner (enabled={enableManualSpawnByDefault})</color>");
                }
                else
                {
                    Debug.Log("<color=yellow>[AutoSceneSetup] ManualPlayerSpawner already exists</color>");
                }
            }
            
            setupCompleted = true;
            Debug.Log("<color=green>[AutoSceneSetup] [OK] Auto-configuration complete!</color>");
            Debug.Log("<color=cyan>[AutoSceneSetup] Test with host + remote client to see spawn debugging.</color>");
        }
        
        private GameObject FindDummyPlayerPrefab()
        {
#if UNITY_EDITOR
            // In editor, search for DummyPlayer prefab
            var guids = UnityEditor.AssetDatabase.FindAssets("DummyPlayer t:Prefab");
            if (guids.Length > 0)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    Debug.Log($"<color=green>[AutoSceneSetup] Found DummyPlayer prefab at: {path}</color>");
                    return prefab;
                }
            }
#endif
            
            // Try Resources folder (for runtime)
            GameObject resourcePrefab = Resources.Load<GameObject>("DummyPlayer");
            if (resourcePrefab != null)
            {
                Debug.Log("[AutoSceneSetup] Found DummyPlayer in Resources");
                return resourcePrefab;
            }
            
            // Last attempt: search in scene for reference
            var playerSpawner = FindFirstObjectByType<FishNet.Component.Spawning.PlayerSpawner>();
            if (playerSpawner != null)
            {
                // Access via reflection since PlayerSpawner fields might be private
                var prefabField = playerSpawner.GetType().GetField("_playerPrefab", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (prefabField != null)
                {
                    var prefabValue = prefabField.GetValue(playerSpawner) as GameObject;
                    if (prefabValue != null)
                    {
                        Debug.Log("[AutoSceneSetup] Found DummyPlayer from existing PlayerSpawner component");
                        return prefabValue;
                    }
                }
            }
            
            Debug.LogWarning("<color=yellow>[AutoSceneSetup] DummyPlayer prefab not found automatically.</color>");
            Debug.LogWarning("<color=yellow>ManualPlayerSpawner will attempt to find it, or assign manually.</color>");
            
            return null;
        }
        
        [ContextMenu("Force Setup Now")]
        private void ForceSetup()
        {
            setupCompleted = false;
            Awake();
        }
    }
    
#if UNITY_EDITOR
    /// <summary>
    /// Editor script to auto-create AutoSceneSetup if it doesn't exist
    /// </summary>
    [UnityEditor.InitializeOnLoad]
    public static class AutoSceneSetupInitializer
    {
        static AutoSceneSetupInitializer()
        {
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode)
        {
            // Only run for MultiplayerRoomTest scene
            if (!scene.name.Contains("MultiplayerRoom"))
                return;

            // Check if AutoSceneSetup exists
            var existing = Object.FindFirstObjectByType<AutoSceneSetup>();
            if (existing != null)
                return;

            // Create it
            GameObject setupObj = new GameObject("_AutoSceneSetup");
            setupObj.AddComponent<AutoSceneSetup>();

            Debug.Log($"<color=green>[AutoSceneSetupInitializer] Created AutoSceneSetup in {scene.name}</color>");

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
#endif
}
