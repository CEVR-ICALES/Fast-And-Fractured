using UnityEngine;
using UnityEditor;

public class PrefabMaterialShaderProcessor : EditorWindow
{
    private GameObject targetObject;
    private Shader targetShader;
    private string alphaClipKeyword = "_ALPHATEST_ON";

    [MenuItem("Tools/Utilities/Prefab Material Shader Processor")]
    public static void ShowWindow()
    {
        GetWindow<PrefabMaterialShaderProcessor>("Prefab Processor");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Single Prefab Material Modifier", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Drag a prefab from your Project folder OR an instance of a prefab from the Scene Hierarchy into the 'Target Object' field.",
            MessageType.Info);

        EditorGUILayout.Space(10);

        // --- UI Fields ---
        targetObject = (GameObject)EditorGUILayout.ObjectField("Target Object", targetObject, typeof(GameObject), true); // Allow scene objects
        targetShader = (Shader)EditorGUILayout.ObjectField("Target Shader", targetShader, typeof(Shader), false);
        alphaClipKeyword = EditorGUILayout.TextField(new GUIContent("Alpha Clip Keyword", "The shader keyword used to enable alpha clipping, e.g., _ALPHATEST_ON."), alphaClipKeyword);

        EditorGUILayout.Space(20);

        // --- Process Button ---
        GUI.enabled = targetObject != null && targetShader != null && !string.IsNullOrEmpty(alphaClipKeyword);

        if (GUILayout.Button("Process Prefab", GUILayout.Height(40)))
        {
            ProcessSinglePrefab();
        }
        GUI.enabled = true;
    }

  
    
 
    private void ProcessSinglePrefab() // The core logic for modifying the materials on the target prefab.
    {
        // 1. Find the source prefab asset, regardless of what was dragged in.
        GameObject prefabAsset = GetPrefabRoot(targetObject);

        if (prefabAsset == null)
        {
            EditorUtility.DisplayDialog("Error: Not a Prefab", "The object you dragged is not a prefab or part of one. Please drag a prefab asset or an instance from the scene.", "OK");
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar("Processing Prefab", $"Modifying materials on: {prefabAsset.name}", 0.5f);

            // 2. Find all renderers within the prefab asset
            Renderer[] renderers = prefabAsset.GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                // Use .sharedMaterials to modify the material assets directly
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat == null) continue;

                    // 3. Apply the changes
                    mat.shader = targetShader;
                    mat.EnableKeyword(alphaClipKeyword);

                    // Mark the material asset as "dirty" to ensure changes are saved
                    EditorUtility.SetDirty(mat);
                }
            }
            // Mark the prefab asset itself as dirty as well
            EditorUtility.SetDirty(prefabAsset);
        }
        finally
        {
            // 4. Clean up and save all changes
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Processing Complete", $"Successfully processed the prefab: {prefabAsset.name}", "OK");
        }
    }

    private GameObject GetPrefabRoot(GameObject inputObject) // Gets the root prefab asset from either a scene instance or a project asset.
    {
        if (inputObject == null) return null;

        // Case 1: The object is an asset in the Project view.
        if (PrefabUtility.IsPartOfPrefabAsset(inputObject))
        {
            return inputObject;
        }

        // Case 2: The object is an instance in the Scene.
        if (PrefabUtility.IsPartOfAnyPrefab(inputObject))
        {
            // GetCorrespondingObjectFromSource finds the asset this instance is from.
            return PrefabUtility.GetCorrespondingObjectFromSource(inputObject);
        }

        // If neither, it's not a prefab.
        return null;
    }
}
