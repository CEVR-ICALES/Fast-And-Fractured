using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using FastAndFractured.Multiplayer.Testing;

/// <summary>
/// Editor tool to configure AutoConnectOnPlayMode for ParallelSync testing
/// </summary>
public class ConfigureAutoConnect : EditorWindow
{
    private const string TARGET_SCENE_PATH = "Assets/Multiplayer/MultiplayerRoomTest.unity";
    private const string AUTO_CONNECT_GO_NAME = "AutoConnect";
    
    [MenuItem("Multiplayer/Configure Auto-Connect")]
    public static void ShowWindow()
    {
        GetWindow<ConfigureAutoConnect>("Auto-Connect Setup");
    }
    
    private void OnGUI()
    {
        EditorGUILayout.LabelField("ParallelSync Auto-Connect Setup", EditorStyles.boldLabel);
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
        
        var autoConnect = GameObject.Find(AUTO_CONNECT_GO_NAME);
        bool hasAutoConnect = autoConnect != null;
        
        if (hasAutoConnect)
        {
            var component = autoConnect.GetComponent<AutoConnectOnPlayMode>();
            if (component != null)
            {
                EditorGUILayout.HelpBox("[OK]AutoConnect configured", MessageType.Info);
                
                // Show current mode
                var so = new SerializedObject(component);
                var modeProp = so.FindProperty("_connectionMode");
                EditorGUILayout.PropertyField(modeProp, new GUIContent("Current Mode"));
                so.ApplyModifiedProperties();
            }
            else
            {
                EditorGUILayout.HelpBox("⚠ AutoConnect GameObject missing component", MessageType.Warning);
                if (GUILayout.Button("Add Component"))
                {
                    autoConnect.AddComponent<AutoConnectOnPlayMode>();
                    EditorSceneManager.MarkSceneDirty(currentScene);
                    EditorSceneManager.SaveOpenScenes();
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox("AutoConnect not found - Create it to enable auto-connection", MessageType.Info);
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Quick Setup", EditorStyles.boldLabel);
        
        EditorGUILayout.HelpBox(
            "For Main Editor (Host):\n" +
            "• Will automatically start as Host when entering Play Mode\n" +
            "• Tests will run automatically after connection",
            MessageType.Info);
        
        if (GUILayout.Button("Configure as AUTO HOST", GUILayout.Height(35)))
        {
            SetupAutoConnect(AutoConnectOnPlayMode.ConnectionMode.AUTO_HOST);
        }
        
        EditorGUILayout.Space();
        
        EditorGUILayout.HelpBox(
            "For ParallelSync Clone (Client):\n" +
            "• Will automatically start as Client when entering Play Mode\n" +
            "• Tests will run automatically after connection",
            MessageType.Info);
        
        if (GUILayout.Button("Configure as AUTO CLIENT", GUILayout.Height(35)))
        {
            SetupAutoConnect(AutoConnectOnPlayMode.ConnectionMode.AUTO_CLIENT);
        }
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("DISABLE Auto-Connect (Manual Mode)"))
        {
            SetupAutoConnect(AutoConnectOnPlayMode.ConnectionMode.DISABLED);
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "⚡ Workflow:\n" +
            "1. Main Editor: Configure as AUTO HOST\n" +
            "2. Clone Editor: Configure as AUTO CLIENT\n" +
            "3. Both editors: Click Play\n" +
            "4. Connection happens automatically\n" +
            "5. Tests run automatically after 3 seconds",
            MessageType.Info);
    }
    
    private void SetupAutoConnect(AutoConnectOnPlayMode.ConnectionMode mode)
    {
        var currentScene = SceneManager.GetActiveScene();
        
        var autoConnect = GameObject.Find(AUTO_CONNECT_GO_NAME);
        if (autoConnect == null)
        {
            autoConnect = new GameObject(AUTO_CONNECT_GO_NAME);
            Debug.Log($"<color=green>[AutoConnect] Created {AUTO_CONNECT_GO_NAME}</color>");
        }
        
        var component = autoConnect.GetComponent<AutoConnectOnPlayMode>();
        if (component == null)
        {
            component = autoConnect.AddComponent<AutoConnectOnPlayMode>();
        }
        
        // Set mode using SerializedObject
        var so = new SerializedObject(component);
        var modeProp = so.FindProperty("_connectionMode");
        modeProp.enumValueIndex = (int)mode;
        so.ApplyModifiedProperties();
        
        EditorSceneManager.MarkSceneDirty(currentScene);
        EditorSceneManager.SaveOpenScenes();
        
        string modeText = mode.ToString().Replace("_", " ");
        Debug.Log($"<color=cyan>[AutoConnect] Configured as {modeText}</color>");
        
        EditorUtility.DisplayDialog("Auto-Connect Configured",
            $"[OK]Configured as: {modeText}\n\n" +
            $"Now when you enter Play Mode, connection will happen automatically.",
            "OK");
    }
}
