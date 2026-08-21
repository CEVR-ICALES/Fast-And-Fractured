using FastAndFractured.Multiplayer.Testing;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using FishNet.Object;
using Utilities;

/// <summary>
/// Editor window to automatically setup and run multiplayer tests
/// Verifies all critical scene components are properly configured
/// </summary>
public class AutomatedMultiplayerTestSetup : EditorWindow
{
    private const string TARGET_SCENE_PATH = "Assets/Multiplayer/MultiplayerRoomTest.unity";
    private const string TEST_COORDINATOR_NAME = "TestCoordinator";
    private const string POOL_MANAGER_NAME = "ObjectPoolManager";
    
    private bool autoEnterPlayMode = false;
    private Vector2 scrollPosition;
    
    [MenuItem("Multiplayer/Setup Automated Tests")]
    public static void ShowWindow()
    {
        GetWindow<AutomatedMultiplayerTestSetup>("Auto Test Setup");
    }
    
    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        EditorGUILayout.LabelField("Automated Multiplayer Test Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // Status check
        var currentScene = SceneManager.GetActiveScene();
        bool isCorrectScene = currentScene.path == TARGET_SCENE_PATH;
        
        if (!isCorrectScene)
        {
            EditorGUILayout.HelpBox($"Current scene: {currentScene.path}\nRequired: {TARGET_SCENE_PATH}", MessageType.Warning);
            if (GUILayout.Button("Load MultiplayerRoomTest Scene"))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(TARGET_SCENE_PATH);
                }
            }
            EditorGUILayout.EndScrollView();
            return;
        }
        
        EditorGUILayout.HelpBox("[OK]MultiplayerRoomTest scene loaded", MessageType.Info);
        EditorGUILayout.Space();
        
        // === Critical Components Status ===
        EditorGUILayout.LabelField("Critical Components", EditorStyles.boldLabel);
        
        bool allGood = true;
        
        // 1. TestCoordinator check
        var existingCoordinator = GameObject.Find(TEST_COORDINATOR_NAME);
        bool hasCoordinator = existingCoordinator != null;
        bool coordinatorConfigured = false;
        
        if (hasCoordinator)
        {
            var testComponent = existingCoordinator.GetComponent<FastAndFractured.Multiplayer.Testing.MultiplayerTestCoordinator>();
            var networkObject = existingCoordinator.GetComponent<NetworkObject>();
            coordinatorConfigured = testComponent != null && networkObject != null;
            
            if (coordinatorConfigured)
            {
                EditorGUILayout.HelpBox("[OK]TestCoordinator: Properly configured", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("⚠ TestCoordinator: Missing components", MessageType.Warning);
                if (GUILayout.Button("Fix TestCoordinator"))
                {
                    SetupTestCoordinator(existingCoordinator);
                }
                allGood = false;
            }
        }
        else
        {
            EditorGUILayout.HelpBox("[OK]TestCoordinator: Not found in scene", MessageType.Error);
            if (GUILayout.Button("Create TestCoordinator"))
            {
                CreateTestCoordinator();
            }
            allGood = false;
        }
        
        // 2. ObjectPoolManager check
        var poolManager = GameObject.Find(POOL_MANAGER_NAME);
        bool hasPoolManager = poolManager != null;
        bool poolManagerConfigured = false;
        int poolCount = 0;
        
        if (hasPoolManager)
        {
            var poolComponent = poolManager.GetComponent<Utilities.ObjectPoolManager>();
            if (poolComponent != null)
            {
                // Use reflection to access private poolSOList field
                var poolSOListField = typeof(Utilities.ObjectPoolManager).GetField("poolSOList", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (poolSOListField != null)
                {
                    var poolSOList = poolSOListField.GetValue(poolComponent) as System.Collections.IList;
                    if (poolSOList != null)
                    {
                        poolCount = poolSOList.Count;
                        poolManagerConfigured = poolCount >= 10; // Need at least 10 pools for all projectile types
                        
                        if (poolManagerConfigured)
                        {
                            EditorGUILayout.HelpBox($"[OK]ObjectPoolManager: Configured with {poolCount} pools", MessageType.Info);
                        }
                        else
                        {
                            EditorGUILayout.HelpBox($"⚠ ObjectPoolManager: Only {poolCount} pools (need ≥10)", MessageType.Warning);
                            if (GUILayout.Button("Setup Pools (Use SetupMultiplayerPoolManager tool)"))
                            {
                                EditorUtility.DisplayDialog("Setup Required",
                                    "Use Multiplayer Tools Hub:\n\n" +
                                    "Tools > Fast And Fractured > Multiplayer Tools Hub\n\n" +
                                    "Then click: Setup Pool Manager\n\n" +
                                    "This will configure all 10 required projectile pools.",
                                    "OK");
                            }
                            allGood = false;
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("⚠ ObjectPoolManager: poolSOList is null", MessageType.Warning);
                        allGood = false;
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("⚠ ObjectPoolManager: Cannot access poolSOList field", MessageType.Warning);
                    allGood = false;
                }
            }
            else
            {
                EditorGUILayout.HelpBox("⚠ ObjectPoolManager: Component missing", MessageType.Warning);
                allGood = false;
            }
        }
        else
        {
            EditorGUILayout.HelpBox("[OK]ObjectPoolManager: Not found in scene", MessageType.Error);
            EditorGUILayout.HelpBox(
                "REQUIRED: Create ObjectPoolManager GameObject\n" +
                "Open Multiplayer Tools Hub and run Setup Pool Manager",
                MessageType.Error);
            allGood = false;
        }
        
        // 3. Check other critical objects
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Scene Dependencies", EditorStyles.boldLabel);
        
        var networking = GameObject.Find("NetworkingCommons");
        var timeManager = GameObject.Find("NetworkedTimeManager");
        var sceneCommons = GameObject.Find("SceneCommonsPreserver");
        var lobby = GameObject.Find("NetworkLobby");
        
        bool hasNetworking = networking != null;
        bool hasTimeManager = timeManager != null;
        bool hasSceneCommons = sceneCommons != null;
        bool hasLobby = lobby != null;
        
        string dependencyStatus = "";
        if (hasNetworking) dependencyStatus += "[OK]NetworkingCommons\n";
        else { dependencyStatus += "[OK]NetworkingCommons (MISSING)\n"; allGood = false; }
        
        if (hasTimeManager) dependencyStatus += "[OK]NetworkedTimeManager\n";
        else { dependencyStatus += "[OK]NetworkedTimeManager (MISSING)\n"; allGood = false; }
        
        if (hasSceneCommons) dependencyStatus += "[OK]SceneCommonsPreserver\n";
        else { dependencyStatus += "[OK]SceneCommonsPreserver (MISSING)\n"; allGood = false; }
        
        if (hasLobby) dependencyStatus += "[OK]NetworkLobby";
        else { dependencyStatus += "[OK]NetworkLobby (MISSING)"; allGood = false; }
        
        EditorGUILayout.HelpBox(dependencyStatus, allGood ? MessageType.Info : MessageType.Warning);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
        
        if (!coordinatorConfigured)
        {
            if (GUILayout.Button("Setup TestCoordinator", GUILayout.Height(30)))
            {
                if (hasCoordinator)
                {
                    SetupTestCoordinator(existingCoordinator);
                }
                else
                {
                    CreateTestCoordinator();
                }
            }
        }
        
        if (allGood)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("[OK]All systems ready for testing!", MessageType.Info);
            
            autoEnterPlayMode = EditorGUILayout.Toggle("Auto Enter Play Mode", autoEnterPlayMode);
            
            if (GUILayout.Button("▶ Run Tests Now", GUILayout.Height(40)))
            {
                if (autoEnterPlayMode && !EditorApplication.isPlaying)
                {
                    EditorApplication.isPlaying = true;
                    Debug.Log("<color=cyan>[AutoTest] Entering Play Mode...</color>");
                }
                else if (!EditorApplication.isPlaying)
                {
                    EditorUtility.DisplayDialog("Ready to Test", 
                        "[OK]Scene configured correctly!\n\n" +
                        "Manual steps:\n" +
                        "1. Click Play button\n" +
                        "2. Start Host in this editor\n" +
                        "3. Start Client in ParallelSync clone\n" +
                        "4. Wait 3 seconds for test results\n\n" +
                        "(Or enable 'Auto Enter Play Mode' for automatic start)", 
                        "OK");
                }
                else
                {
                    Debug.Log("<color=yellow>[AutoTest] Already in Play Mode. Connect Host/Client to run tests.</color>");
                }
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "ParallelSync Testing:\n" +
                "• Main Editor: Start Host\n" +
                "• Clone Editor: Start Client\n" +
                "• Tests run automatically after 3 seconds\n" +
                "• Check console for 'MULTIPLAYER TEST REPORT'", 
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "⚠ Cannot run tests - Fix missing components above first",
                MessageType.Warning);
        }
        
        EditorGUILayout.Space();
        if (GUILayout.Button("📖 Open Testing Guide"))
        {
            var guidePath = "Assets/Multiplayer/Testing/AUTOMATED_TESTING_GUIDE.md";
            var guideAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(guidePath);
            if (guideAsset != null)
            {
                EditorGUIUtility.PingObject(guideAsset);
                Selection.activeObject = guideAsset;
            }
            else
            {
                Debug.LogWarning($"Testing guide not found at {guidePath}");
            }
        }
        
        EditorGUILayout.EndScrollView();
    }
    
    private void SetupEverything()
    {
        // Load scene if needed
        var currentScene = SceneManager.GetActiveScene();
        if (currentScene.path != TARGET_SCENE_PATH)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            EditorSceneManager.OpenScene(TARGET_SCENE_PATH);
        }
        
        // Create or fix coordinator
        var existingCoordinator = GameObject.Find(TEST_COORDINATOR_NAME);
        if (existingCoordinator != null)
        {
            SetupTestCoordinator(existingCoordinator);
        }
        else
        {
            CreateTestCoordinator();
        }
        
        EditorUtility.DisplayDialog("Setup Complete", 
            "[OK]Test coordinator created and configured!\n\n" +
            "Ready to run tests.\n" +
            "Use ParallelSync to test with Host + Client.", 
            "OK");
    }
    
    private void CreateTestCoordinator()
    {
        var go = new GameObject(TEST_COORDINATOR_NAME);
        SetupTestCoordinator(go);
        
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
        
        Debug.Log($"<color=green>[AutoSetup] [OK]Created {TEST_COORDINATOR_NAME}</color>");
    }
    
    private void SetupTestCoordinator(GameObject go)
    {
        // Add MultiplayerTestCoordinator if missing
        var testComponent = go.GetComponent<MultiplayerTestCoordinator>();
        if (testComponent == null)
        {
            testComponent = go.AddComponent<MultiplayerTestCoordinator>();
            Debug.Log($"<color=green>[AutoSetup] [OK]Added MultiplayerTestCoordinator</color>");
        }
        
        // Add NetworkObject if missing
        var networkObject = go.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            networkObject = go.AddComponent<NetworkObject>();
            Debug.Log($"<color=green>[AutoSetup] [OK]Added NetworkObject</color>");
        }
        
        // Mark scene as dirty to save
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        
        Debug.Log($"<color=green>[AutoSetup] [OK]TestCoordinator fully configured and scene saved</color>");
    }
    
    [MenuItem("Multiplayer/Quick Test Status Check #&t")]
    public static void QuickStatusCheck()
    {
        var currentScene = SceneManager.GetActiveScene();
        if (currentScene.path != TARGET_SCENE_PATH)
        {
            Debug.LogWarning($"[AutoSetup] Wrong scene: {currentScene.name}\nSwitch to MultiplayerRoomTest first");
            ShowWindow();
            return;
        }
        
        // Quick status log
        var coordinator = GameObject.Find(TEST_COORDINATOR_NAME);
        var poolManager = GameObject.Find(POOL_MANAGER_NAME);
        var networking = GameObject.Find("NetworkingCommons");
        
        string report = "<color=cyan>══╁EMULTIPLAYER TEST STATUS ══╁E/color>\n";
        
        if (coordinator != null)
        {
            var test = coordinator.GetComponent<FastAndFractured.Multiplayer.Testing.MultiplayerTestCoordinator>();
            var net = coordinator.GetComponent<NetworkObject>();
            if (test != null && net != null)
                report += "<color=green>[OK]TestCoordinator: Ready</color>\n";
            else
                report += "<color=yellow>⚠ TestCoordinator: Misconfigured</color>\n";
        }
        else
        {
            report += "<color=red>[OK]TestCoordinator: Missing</color>\n";
        }
        
        if (poolManager != null)
        {
            var pool = poolManager.GetComponent<Utilities.ObjectPoolManager>();
            if (pool != null)
            {
                // Use reflection to access private poolSOList field
                var poolSOListField = typeof(Utilities.ObjectPoolManager).GetField("poolSOList", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (poolSOListField != null)
                {
                    var poolSOList = poolSOListField.GetValue(pool) as System.Collections.IList;
                    if (poolSOList != null && poolSOList.Count >= 10)
                        report += $"<color=green>[OK]ObjectPoolManager: {poolSOList.Count} pools</color>\n";
                    else if (poolSOList != null)
                        report += $"<color=yellow>⚠ ObjectPoolManager: Only {poolSOList.Count} pools (need ≥10)</color>\n";
                    else
                        report += "<color=yellow>⚠ ObjectPoolManager: Not configured</color>\n";
                }
                else
                {
                    report += "<color=yellow>⚠ ObjectPoolManager: Cannot verify pool count</color>\n";
                }
            }
            else
            {
                report += "<color=yellow>⚠ ObjectPoolManager: Component missing</color>\n";
            }
        }
        else
        {
            report += "<color=red>[OK]ObjectPoolManager: Missing</color>\n";
        }
        
        if (networking != null)
            report += "<color=green>[OK]NetworkingCommons: Found</color>\n";
        else
            report += "<color=red>[OK]NetworkingCommons: Missing</color>\n";
        
        report += "<color=cyan>══════════════════════════════</color>";
        Debug.Log(report);
        
        ShowWindow();
    }
}
