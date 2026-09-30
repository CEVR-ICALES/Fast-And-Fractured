using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Utilities;

/// <summary>
/// Legacy verification tool to check if ObjectPoolManager is properly configured.
/// Prefer using Tools/Fast And Fractured/Multiplayer Tools Hub.
/// </summary>
public class VerifyPoolManagerSetup : EditorWindow
{
    [MenuItem("Tools/Multiplayer/Verify Pool Manager", priority = 2001)]
    public static void ShowWindow()
    {
        VerifySetup();
    }

    private static void VerifySetup()
    {
        // Load scene
        string scenePath = "Assets/Multiplayer/Testing/Scenes/MultiplayerRoomTest.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        
        // Find ObjectPoolManager
        ObjectPoolManager poolManager = Object.FindObjectOfType<ObjectPoolManager>();
        
        if (poolManager == null)
        {
            Debug.LogError("[VerifyPoolManager] ❁EObjectPoolManager NOT found in MultiplayerRoomTest scene!");
            EditorUtility.DisplayDialog("Verification Failed", 
                "ObjectPoolManager NOT found in scene.\n\nPlease run 'Setup Pool Manager' first.", 
                "OK");
            return;
        }
        
        Debug.Log($"[VerifyPoolManager] [OK]Found ObjectPoolManager on GameObject: {poolManager.gameObject.name}");
        
        // Check if pools are assigned
        SerializedObject serializedManager = new SerializedObject(poolManager);
        SerializedProperty poolSOListProperty = serializedManager.FindProperty("poolSOList");
        
        int poolCount = poolSOListProperty.arraySize;
        Debug.Log($"[VerifyPoolManager] Pool count: {poolCount}");
        
        if (poolCount == 0)
        {
            Debug.LogWarning("[VerifyPoolManager] ⚠ ObjectPoolManager has 0 pools assigned!");
            EditorUtility.DisplayDialog("Verification Warning", 
                "ObjectPoolManager found but has 0 pools assigned.\n\nPlease run 'Setup Pool Manager' to configure pools.", 
                "OK");
            return;
        }
        
        // List all pools
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"[VerifyPoolManager] [OK]ObjectPoolManager configured with {poolCount} pools:");
        
        for (int i = 0; i < poolCount; i++)
        {
            var poolSO = poolSOListProperty.GetArrayElementAtIndex(i).objectReferenceValue as ObjectPoolSO;
            if (poolSO != null)
            {
                sb.AppendLine($"  - {poolSO.name} (Pooltype: {poolSO.pooltype}, Count: {poolSO.poolNum})");
            }
        }
        
        Debug.Log(sb.ToString());
        
        EditorUtility.DisplayDialog("Verification Success", 
            $"[OK]ObjectPoolManager properly configured!\n\n{poolCount} pools assigned.\n\nCheck Console for details.", 
            "OK");
    }
}
