using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using FishNet.Object;

/// <summary>
/// Tool to setup multiplayer wrappers in MultiplayerRoomTest scene
/// </summary>
public class SetupMultiplayerWrappers : EditorWindow
{
    private const string TARGET_SCENE_PATH = "Assets/Multiplayer/MultiplayerRoomTest.unity";
    
[MenuItem("Multiplayer/Setup Wrappers in Scene")]
public static void ShowWindow()
{
    GetWindow<SetupMultiplayerWrappers>("Setup Wrappers");
}

[MenuItem("Tools/Fast And Fractured/Setup Wrappers in Scene", priority = 25)]
public static void ShowWindowFromTools()
{
    ShowWindow();
}
    
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Setup Multiplayer Wrappers", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        var currentScene = SceneManager.GetActiveScene();
        bool isCorrectScene = currentScene.path == TARGET_SCENE_PATH;
        
        if (!isCorrectScene)
        {
            EditorGUILayout.HelpBox($"Switch to MultiplayerRoomTest scene first", MessageType.Warning);
            if (GUILayout.Button("Load Scene"))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(TARGET_SCENE_PATH);
                }
            }
            return;
        }
        
        EditorGUILayout.HelpBox("[OK] MultiplayerRoomTest scene loaded", MessageType.Info);
        EditorGUILayout.Space();
        
        // Check current status
        var singletonGuard = FindObjectOfType<FastAndFractured.Multiplayer.MultiplayerSingletonGuard>();
        var levelController = GameObject.Find("NetworkedLevelController");
        var interactableHandler = FindObjectOfType<FastAndFractured.Multiplayer.NetworkedInteractableHandler>();
        
        EditorGUILayout.LabelField("Current Status:", EditorStyles.boldLabel);
        
        if (singletonGuard != null)
        {
            EditorGUILayout.HelpBox("[OK] MultiplayerSingletonGuard: Found in scene", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("[X] MultiplayerSingletonGuard: Not found (CRITICAL)", MessageType.Error);
        }
        
        if (levelController != null)
        {
            var wrapper = levelController.GetComponent<FastAndFractured.Multiplayer.NetworkedLevelControllerWrapper>();
            if (wrapper != null)
            {
                EditorGUILayout.HelpBox("[OK] NetworkedLevelControllerWrapper: Configured", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("⚠ NetworkedLevelController: Missing wrapper component", MessageType.Warning);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("[X] NetworkedLevelController: GameObject not found", MessageType.Error);
        }
        
        if (interactableHandler != null)
        {
            EditorGUILayout.HelpBox("[OK] NetworkedInteractableHandler: Found in scene", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("[X] NetworkedInteractableHandler: Not found in scene", MessageType.Error);
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Actions:", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Setup All Wrappers", GUILayout.Height(35)))
        {
            SetupAllWrappers();
        }
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Verify Setup"))
        {
            VerifySetup();
        }
    }
    
    private void SetupAllWrappers()
    {
        Debug.Log("<color=cyan>===== SETTING UP MULTIPLAYER WRAPPERS =====</color>");
        
        // 0. Setup MultiplayerSingletonGuard (MUST be first - prevents auxiliar cascade)
        SetupSingletonGuard();
        
        // 1. Setup NetworkedLevelControllerWrapper
        SetupLevelControllerWrapper();
        
        // 2. Setup NetworkedInteractableHandler
        SetupInteractableHandler();
        
        // 3. Save scene
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        
        Debug.Log("<color=green>[OK] All wrappers configured and scene saved!</color>");
        
        EditorUtility.DisplayDialog("Setup Complete",
            "[OK] All wrappers configured!\n\n" +
            "MultiplayerSingletonGuard: Added\n" +
            "NetworkedLevelControllerWrapper: Added\n" +
            "NetworkedInteractableHandler: Added\n\n" +
            "Scene saved. Ready to test!",
            "OK");
    }
    
    private void SetupSingletonGuard()
    {
        // Check if already exists
        var existing = FindObjectOfType<FastAndFractured.Multiplayer.MultiplayerSingletonGuard>();
        if (existing != null)
        {
            Debug.Log("<color=yellow>[SetupWrappers] MultiplayerSingletonGuard already exists</color>");
            return;
        }
        
        // Create GameObject
        GameObject go = new GameObject("MultiplayerSingletonGuard");
        go.AddComponent<FastAndFractured.Multiplayer.MultiplayerSingletonGuard>();
        
        Debug.Log("<color=green>[SetupWrappers] [OK] Created MultiplayerSingletonGuard</color>");
    }
    
    private void SetupLevelControllerWrapper()
    {
        var levelController = GameObject.Find("NetworkedLevelController");
        
        if (levelController == null)
        {
            Debug.LogError("[SetupWrappers] NetworkedLevelController GameObject not found!");
            return;
        }
        
        // Check if wrapper already exists
        var wrapper = levelController.GetComponent<FastAndFractured.Multiplayer.NetworkedLevelControllerWrapper>();
        if (wrapper != null)
        {
            Debug.Log("<color=yellow>[SetupWrappers] NetworkedLevelControllerWrapper already exists</color>");
            return;
        }
        
        // Add wrapper component
        wrapper = levelController.AddComponent<FastAndFractured.Multiplayer.NetworkedLevelControllerWrapper>();
        Debug.Log("<color=green>[SetupWrappers] [OK] Added NetworkedLevelControllerWrapper</color>");
    }
    
    private void SetupInteractableHandler()
    {
        // Check if already exists
        var existing = FindObjectOfType<FastAndFractured.Multiplayer.NetworkedInteractableHandler>();
        if (existing != null)
        {
            Debug.Log("<color=yellow>[SetupWrappers] NetworkedInteractableHandler already exists</color>");
            return;
        }
        
        // Create GameObject
        GameObject go = new GameObject("NetworkedInteractableHandler");
        
        // Add NetworkedInteractableHandler component
        var handler = go.AddComponent<FastAndFractured.Multiplayer.NetworkedInteractableHandler>();
        
        // Add NetworkObject component
        var netObj = go.AddComponent<NetworkObject>();
        
        Debug.Log("<color=green>[SetupWrappers] [OK] Created NetworkedInteractableHandler</color>");
    }
    
    private void VerifySetup()
    {
        Debug.Log("<color=cyan>===== VERIFYING WRAPPER SETUP =====</color>");
        
        bool allGood = true;
        
        // Check MultiplayerSingletonGuard
        var guard = FindObjectOfType<FastAndFractured.Multiplayer.MultiplayerSingletonGuard>();
        if (guard != null)
        {
            Debug.Log("<color=green>[OK] MultiplayerSingletonGuard: Found in scene</color>");
        }
        else
        {
            Debug.LogError("<color=red>[X] MultiplayerSingletonGuard: NOT FOUND (CRITICAL)</color>");
            allGood = false;
        }
        
        // Check NetworkedLevelController
        var levelController = GameObject.Find("NetworkedLevelController");
        if (levelController != null)
        {
            var manager = levelController.GetComponent<FastAndFractured.Multiplayer.NetworkedLevelControllerManager>();
            var wrapper = levelController.GetComponent<FastAndFractured.Multiplayer.NetworkedLevelControllerWrapper>();
            var netObj = levelController.GetComponent<NetworkObject>();
            
            if (manager != null && wrapper != null && netObj != null)
            {
                Debug.Log("<color=green>[OK] NetworkedLevelController: Fully configured</color>");
                Debug.Log($"  - NetworkedLevelControllerManager: Present");
                Debug.Log($"  - NetworkedLevelControllerWrapper: Present");
                Debug.Log($"  - NetworkObject: Present");
            }
            else
            {
                Debug.LogError("<color=red>[X] NetworkedLevelController: Missing components!</color>");
                if (manager == null) Debug.LogError("  - NetworkedLevelControllerManager: MISSING");
                if (wrapper == null) Debug.LogError("  - NetworkedLevelControllerWrapper: MISSING");
                if (netObj == null) Debug.LogError("  - NetworkObject: MISSING");
                allGood = false;
            }
        }
        else
        {
            Debug.LogError("<color=red>[X] NetworkedLevelController GameObject not found!</color>");
            allGood = false;
        }
        
        // Check NetworkedInteractableHandler
        var interactableHandler = FindObjectOfType<FastAndFractured.Multiplayer.NetworkedInteractableHandler>();
        if (interactableHandler != null)
        {
            var netObj = interactableHandler.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                Debug.Log("<color=green>[OK] NetworkedInteractableHandler: Fully configured</color>");
                Debug.Log($"  - NetworkedInteractableHandler: Present");
                Debug.Log($"  - NetworkObject: Present");
            }
            else
            {
                Debug.LogError("<color=red>[X] NetworkedInteractableHandler: Missing NetworkObject!</color>");
                allGood = false;
            }
        }
        else
        {
            Debug.LogError("<color=red>[X] NetworkedInteractableHandler not found in scene!</color>");
            allGood = false;
        }
        
        // Check InteractableHandler (original singleton)
        var originalHandler = FindObjectOfType<FastAndFractured.InteractableHandler>();
        if (originalHandler != null)
        {
            Debug.Log("<color=green>[OK] InteractableHandler (original): Found in scene</color>");
        }
        else
        {
            Debug.LogWarning("<color=yellow>⚠ InteractableHandler (original): Not found (will auto-create)</color>");
        }
        
        Debug.Log("<color=cyan>══════════════════════════════</color>");
        
        if (allGood)
        {
            Debug.Log("<color=green>[OK] ALL CHECKS PASSED!</color>");
            EditorUtility.DisplayDialog("Verification Passed",
                "[OK] All wrappers are properly configured!\n\n" +
                "Ready to test multiplayer.",
                "OK");
        }
        else
        {
            Debug.LogError("<color=red>[X] SOME CHECKS FAILED! See console for details.</color>");
            EditorUtility.DisplayDialog("Verification Failed",
                "[X] Some wrappers are not configured correctly.\n\n" +
                "Check console for details and run 'Setup All Wrappers'.",
                "OK");
        }
    }
}
