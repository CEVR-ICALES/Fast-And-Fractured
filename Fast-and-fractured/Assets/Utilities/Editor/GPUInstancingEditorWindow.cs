using UnityEngine;
using UnityEditor;
using System.IO;

public class GPUInstancingEditorWindow : EditorWindow
{
    private IMaterialProcessor materialProcessor;
    private int modifiedCount;

    [MenuItem("Tools/Utilities/GPU Instancing/Enable on All Materials")]
    public static void ShowWindow()
    {
        GetWindow<GPUInstancingEditorWindow>("GPU Instancing Tool");
    }

    private void OnEnable()
    {
        materialProcessor = new MaterialProcessor();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("GPU Instancing Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (GUILayout.Button("Enable GPU Instancing on All Materials"))
        {
            ProcessMaterials();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Materials Modified: " + modifiedCount);
    }

    private void ProcessMaterials()
    {
        string[] materialGUIDs = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        modifiedCount = 0;

        foreach (string guid in materialGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat != null && !mat.enableInstancing)
            {
                materialProcessor.ApplyGPUInstancing(mat);
                modifiedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"GPU Instancing enabled on {modifiedCount} materials.");
    }
}
public interface IMaterialProcessor
{
    void ApplyGPUInstancing(Material material);
}
public class MaterialProcessor : IMaterialProcessor
{
    public void ApplyGPUInstancing(Material material)
    {
        if (material != null && !material.enableInstancing)
        {
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);  
        }
    }
}