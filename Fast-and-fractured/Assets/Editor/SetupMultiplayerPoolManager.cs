using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Utilities;
using System.Collections.Generic;

/// <summary>
/// Legacy menu entrypoint for setting up ObjectPoolManager in MultiplayerRoomTest scene.
/// Prefer using Tools/Fast And Fractured/Multiplayer Tools Hub.
/// </summary>
public class SetupMultiplayerPoolManager : EditorWindow
{
    [MenuItem("Tools/Multiplayer/Setup Pool Manager", priority = 2000)]
    public static void ShowWindow()
    {
        GetWindow<SetupMultiplayerPoolManager>("Setup Pool Manager");
    }

    private void OnGUI()
    {
        GUILayout.Label("Setup ObjectPoolManager for Multiplayer", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        EditorGUILayout.HelpBox(
            "Legacy tool. Prefer Tools/Fast And Fractured/Multiplayer Tools Hub.\n" +
            "This will add/configure ObjectPoolManager in MultiplayerRoomTest scene with networked bullet pools.",
            MessageType.Info);
        GUILayout.Space(10);

        if (GUILayout.Button("Setup Pool Manager", GUILayout.Height(40)))
        {
            SetupPoolManager();
        }
        
        GUILayout.Space(20);
    }

    private static void SetupPoolManager()
    {
        // Load scene
        string scenePath = "Assets/Multiplayer/Testing/Scenes/MultiplayerRoomTest.unity";
        
        if (!System.IO.File.Exists(scenePath))
        {
            Debug.LogError($"[SetupPoolManager] Scene not found at: {scenePath}");
            EditorUtility.DisplayDialog("Error", "MultiplayerRoomTest.unity scene not found!", "OK");
            return;
        }
        
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        
        // Find or create ObjectPoolManager
        ObjectPoolManager poolManager = Object.FindObjectOfType<ObjectPoolManager>();
        
        if (poolManager == null)
        {
            // Create new GameObject with ObjectPoolManager
            GameObject poolManagerGO = new GameObject("ObjectPoolManager");
            poolManager = poolManagerGO.AddComponent<ObjectPoolManager>();
            Debug.Log("[SetupPoolManager] Created ObjectPoolManager GameObject");
        }
        else
        {
            Debug.Log("[SetupPoolManager] Found existing ObjectPoolManager");
        }

        // Load all pool ScriptableObjects from Assets/Multiplayer/Pool/
        List<ObjectPoolSO> poolSOList = new List<ObjectPoolSO>();
        
        string[] poolNames = new string[]
        {
            // Networked projectiles
            "NBTomatoPool",      // NORMAL_BULLET_TOMATO
            "NBEggPool",         // NORMAL_BULLET_EGG
            "NBGolfBall",        // NORMAL_BULLET_GOLF
            "NBCroqueta",        // NORMAL_BULLET_CROQUETA
            "PBChicken",         // PUSH_BULLET_CHICKEN
            "PBGolfBat",         // PUSH_BULLET_GOLF
            "PBMelon",           // PUSH_BULLET_MELON
            "PBSarten",          // PUSH_BULLET_SARTENAZO
            
            // Cosmetic (optional - client-side only)
            "NBCosmeticTomato",
            "NBCosmeticGolf"
        };

        foreach (string poolName in poolNames)
        {
            string assetPath = $"Assets/Multiplayer/Pool/{poolName}.asset";
            ObjectPoolSO poolSO = AssetDatabase.LoadAssetAtPath<ObjectPoolSO>(assetPath);
            
            if (poolSO != null)
            {
                poolSOList.Add(poolSO);
                Debug.Log($"[SetupPoolManager] Loaded pool: {poolSO.name} (Pooltype: {poolSO.pooltype})");
            }
            else
            {
                Debug.LogWarning($"[SetupPoolManager] Could not find pool at: {assetPath}");
            }
        }

        // Assign pool list to manager using SerializedObject
        SerializedObject serializedManager = new SerializedObject(poolManager);
        SerializedProperty poolSOListProperty = serializedManager.FindProperty("poolSOList");
        
        poolSOListProperty.ClearArray();
        for (int i = 0; i < poolSOList.Count; i++)
        {
            poolSOListProperty.InsertArrayElementAtIndex(i);
            poolSOListProperty.GetArrayElementAtIndex(i).objectReferenceValue = poolSOList[i];
        }
        
        serializedManager.ApplyModifiedProperties();
        
        Debug.Log($"[SetupPoolManager] Assigned {poolSOList.Count} pools to ObjectPoolManager");
        
        // Mark scene dirty and save
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        
        Debug.Log("<color=green>[SetupPoolManager] [OK]Setup complete! Scene saved.</color>");
        EditorUtility.DisplayDialog("Setup Complete", 
            $"ObjectPoolManager configured with {poolSOList.Count} networked bullet pools.\n\n" +
            "Scene has been saved.", 
            "OK");
    }
}
