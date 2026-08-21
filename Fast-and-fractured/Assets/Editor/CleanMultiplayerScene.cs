using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor tool to identify and optionally disable singleplayer-only objects in multiplayer scene
/// </summary>
public class CleanMultiplayerScene : EditorWindow
{
    private const string TARGET_SCENE_PATH = "Assets/Multiplayer/MultiplayerRoomTest.unity";
    
    private Vector2 scrollPosition;
    private bool foundIssues = false;
    
    [MenuItem("Multiplayer/Clean Scene (Remove SP Objects)")]
    public static void ShowWindow()
    {
        GetWindow<CleanMultiplayerScene>("Clean MP Scene");
    }
    
    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        EditorGUILayout.LabelField("Multiplayer Scene Cleanup", EditorStyles.boldLabel);
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
            EditorGUILayout.EndScrollView();
            return;
        }
        
        EditorGUILayout.HelpBox("This tool identifies singleplayer-only objects that shouldn't be in multiplayer scene", MessageType.Info);
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Scan Scene for Issues", GUILayout.Height(30)))
        {
            ScanScene();
        }
        
        EditorGUILayout.Space();
        
        EditorGUILayout.LabelField("Known Problem Objects:", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "These singleplayer components cause issues in multiplayer:\n\n" +
            "• InteractableHandler - Manages pickup spawns\n" +
            "• SkinUnlockHandler - Skin unlock system\n" +
            "• LevelController - Singleplayer level logic\n" +
            "• PauseManager - Singleplayer pause menu\n\n" +
            "These should NOT be active in MultiplayerRoomTest scene",
            MessageType.Warning);
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Disable All Singleplayer Objects", GUILayout.Height(35)))
        {
            if (EditorUtility.DisplayDialog("Disable Singleplayer Objects",
                "This will DISABLE (not delete) all singleplayer-only objects.\n\n" +
                "You can re-enable them manually if needed.\n\n" +
                "Continue?",
                "Yes, Disable Them",
                "Cancel"))
            {
                DisableSingleplayerObjects();
            }
        }
        
        EditorGUILayout.EndScrollView();
    }
    
    private void ScanScene()
    {
        Debug.Log("<color=cyan>══╁ESCANNING MULTIPLAYER SCENE ══╁E/color>");
        foundIssues = false;
        
        // Check for problematic singleplayer components
        var interactableHandlers = FindObjectsOfType<FastAndFractured.InteractableHandler>(true);
        var skinUnlockHandlers = FindObjectsOfType<SkinUnlockHandler>(true);
        
        // Also check for LevelController, PauseManager, etc.
        Component[] allComponents = FindObjectsOfType<Component>(true);
        
        int issueCount = 0;
        
        foreach (var component in allComponents)
        {
            if (component == null) continue;
            
            string typeName = component.GetType().Name;
            
            // Known singleplayer-only types
            if (typeName == "InteractableHandler" ||
                typeName == "SkinUnlockHandler" ||
                typeName == "LevelController" ||
                typeName == "PauseManager")
            {
                if (component.gameObject.activeInHierarchy)
                {
                    Debug.LogWarning($"⚠ Found ACTIVE singleplayer object: {component.gameObject.name} ({typeName})", component.gameObject);
                    issueCount++;
                    foundIssues = true;
                }
                else
                {
                    Debug.Log($"<color=green>[OK]{component.gameObject.name} ({typeName}) - Already disabled</color>");
                }
            }
        }
        
        if (issueCount == 0)
        {
            Debug.Log("<color=green>✓✓[OK]No singleplayer objects found active!</color>");
            EditorUtility.DisplayDialog("Scan Complete", 
                "[OK]Scene is clean!\n\nNo problematic singleplayer objects found.",
                "OK");
        }
        else
        {
            Debug.LogWarning($"<color=yellow>⚠ Found {issueCount} active singleplayer objects</color>");
            EditorUtility.DisplayDialog("Issues Found", 
                $"⚠ Found {issueCount} active singleplayer objects.\n\n" +
                "Check console for details.\n" +
                "Use 'Disable All Singleplayer Objects' to fix.",
                "OK");
        }
        
        Debug.Log("<color=cyan>══════════════════════════════╁E/color>");
    }
    
    private void DisableSingleplayerObjects()
    {
        Component[] allComponents = FindObjectsOfType<Component>(true);
        
        int disabledCount = 0;
        
        foreach (var component in allComponents)
        {
            if (component == null) continue;
            
            string typeName = component.GetType().Name;
            
            // Known singleplayer-only types
            if (typeName == "InteractableHandler" ||
                typeName == "SkinUnlockHandler" ||
                typeName == "LevelController" ||
                typeName == "PauseManager")
            {
                if (component.gameObject.activeInHierarchy)
                {
                    component.gameObject.SetActive(false);
                    Debug.Log($"<color=yellow>[Cleanup] Disabled: {component.gameObject.name} ({typeName})</color>", component.gameObject);
                    disabledCount++;
                }
            }
        }
        
        if (disabledCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            
            Debug.Log($"<color=green>[Cleanup] [OK]Disabled {disabledCount} singleplayer objects and saved scene</color>");
            
            EditorUtility.DisplayDialog("Cleanup Complete",
                $"[OK]Disabled {disabledCount} singleplayer objects\n\n" +
                "Scene has been saved.\n" +
                "Try running tests again.",
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Nothing to Clean",
                "No active singleplayer objects found.",
                "OK");
        }
    }
}
