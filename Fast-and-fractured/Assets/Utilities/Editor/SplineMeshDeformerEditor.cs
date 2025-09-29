#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Inspector for the SplineMeshDeformer component.
/// Provides buttons for processing the spline and debugging, along with helper warnings.
/// </summary>
[CustomEditor(typeof(SplineMeshDeformer)), CanEditMultipleObjects]
public class SplineMeshDeformerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        SplineMeshDeformer deformer = (SplineMeshDeformer)target;

        if (!deformer.AssertAllModelsHaveReadWriteEnabled())
        {
            EditorGUILayout.HelpBox(
                "One or more models used in the prefabs do not have 'Read/Write Enabled' checked in their import settings. This is required for mesh deformation.",
                MessageType.Error);

            if (GUILayout.Button("Attempt to Fix All Models"))
            {
                deformer.FixAllModelsReadWriteToggle();
            }
        }

        EditorGUILayout.Space();

        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f); 
        if (GUILayout.Button("Create and Deform Mesh", GUILayout.Height(30)))
        {
            foreach (Object obj in targets)
            {
                SplineMeshDeformer t = (SplineMeshDeformer)obj;
                Undo.RecordObject(t, "Process Spline Mesh");
                t.Process();
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            }
        }
        GUI.backgroundColor = Color.white; 

        if (GUILayout.Button("Clear Generated Meshes"))
        {
            foreach (Object obj in targets)
            {
                SplineMeshDeformer t = (SplineMeshDeformer)obj;
                Undo.RecordObject(t, "Clear Spline Meshes");
                t.Clear();
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            }
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Debug: Find Deformers with Null Parent"))
        {
            SplineMeshDeformer[] allDeformers = FindObjectsOfType<SplineMeshDeformer>();
            int count = 0;
            foreach (var d in allDeformers)
            {
                if (d.parent == null)
                {
                    Debug.Log($"SplineMeshDeformer '{d.name}' has a null parent.", d.gameObject);
                    count++;
                }
            }
            Debug.Log($"Found {count} SplineMeshDeformer(s) with a null parent property.");
        }
    }
}

#endif
