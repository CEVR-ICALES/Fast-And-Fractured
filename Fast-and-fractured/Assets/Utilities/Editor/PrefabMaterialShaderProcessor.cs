#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class PrefabMaterialShaderProcessor : EditorWindow
{
    private Shader targetShader;
    private float minDistance = 0.0f;
    private float maxDistance = 100.0f;
    private string alphaClipKeyword = "_ALPHATEST_ON";
    private string alphaClipFloatProperty = "_AlphaClip";

    private List<GameObject> targetObjects = new List<GameObject>();
    private Vector2 scrollPosition;

    [MenuItem("Tools/Utilities/Prefab Material Shader Processor")]
    public static void ShowWindow()
    {
        GetWindow<PrefabMaterialShaderProcessor>("Prefab Processor");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Drag & Drop Material Modifier", EditorStyles.boldLabel);

        targetShader = (Shader)EditorGUILayout.ObjectField("Target Shader", targetShader, typeof(Shader), false);
        EditorGUILayout.Space();
        minDistance = EditorGUILayout.FloatField(new GUIContent("Min Distance", "Sets the _MinDistance float property."), minDistance);
        maxDistance = EditorGUILayout.FloatField(new GUIContent("Max Distance", "Sets the _MaxDistance float property."), maxDistance);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Alpha Clipping Settings", EditorStyles.miniBoldLabel);
        alphaClipKeyword = EditorGUILayout.TextField(new GUIContent("Keyword", "E.g., _ALPHATEST_ON"), alphaClipKeyword);
        alphaClipFloatProperty = EditorGUILayout.TextField(new GUIContent("Float Property Name", "E.g., _AlphaClip"), alphaClipFloatProperty);

        EditorGUILayout.Space(20);

        DrawDragAndDropArea();

        DrawObjectList();

        DrawActionButtons();
    }

    private void DrawDragAndDropArea()
    {
        Event currentEvent = Event.current;
        Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "Drag & Drop GameObjects Here (can be dropped directly from the scene, if u drop an object with cildren on it the children objects will be processed too)");

        switch (currentEvent.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!dropArea.Contains(currentEvent.mousePosition))
                    break;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (currentEvent.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (Object draggedObject in DragAndDrop.objectReferences)
                    {
                        if (draggedObject is GameObject go && !targetObjects.Contains(go))
                        {
                            targetObjects.Add(go);
                        }
                    }
                }
                Event.current.Use();
                break;
        }
    }

    private void DrawObjectList()
    {
        EditorGUILayout.LabelField("Objects to Process:", EditorStyles.boldLabel);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MinHeight(100), GUILayout.MaxHeight(300));

        if (targetObjects.Count == 0)
        {
            EditorGUILayout.LabelField("  List is empty.", EditorStyles.centeredGreyMiniLabel);
        }
        else
        {
            for (int i = 0; i < targetObjects.Count; i++)
            {
                targetObjects[i] = (GameObject)EditorGUILayout.ObjectField($"  Element {i}", targetObjects[i], typeof(GameObject), true);
            }
        }

        EditorGUILayout.EndScrollView();

        //Remove null entries if objects were deleted or removed from the list
        targetObjects.RemoveAll(item => item == null);
    }

    private void DrawActionButtons()
    {
        EditorGUILayout.Space();

        //Process Button
        GUI.enabled = targetObjects.Count > 0 && targetShader != null;
        if (GUILayout.Button("Process Listed Objects", GUILayout.Height(40)))
        {
            ProcessDroppedObjects();
            //Clear the list after processing for the next batch
            targetObjects.Clear();
        }
        GUI.enabled = true;

        //Clear List Button
        if (targetObjects.Count > 0)
        {
            if (GUILayout.Button("Clear List"))
            {
                targetObjects.Clear();
            }
        }
    }

    private void ProcessDroppedObjects()
    {
        int modifiedRendererCount = 0;
        try
        {
            for (int i = 0; i < targetObjects.Count; i++)
            {
                GameObject obj = targetObjects[i];
                EditorUtility.DisplayProgressBar(
                    "Processing Objects",
                    $"Processing: {obj.name}",
                    (float)(i + 1) / targetObjects.Count);

                //Get renderers from the object AND all its children
                Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);

                foreach (Renderer renderer in renderers)
                {
                    //Skip if there are no materials assigned
                    if (renderer.sharedMaterials == null || renderer.sharedMaterials.Length == 0)
                    {
                        continue;
                    }

                    bool rendererModified = false;
                    foreach (Material mat in renderer.sharedMaterials)
                    {
                        if (mat == null /*|| mat.GetFloat("_SurfaceType") == 1f*/) continue;

                        //Apply all modifications to the material asset
                        mat.shader = targetShader;
                        mat.SetFloat("_MinDistance", minDistance);
                        mat.SetFloat("_MaxDistance", maxDistance);

                        mat.SetFloat("_AlphaCutoffEnable", 1);     // 1 enables the "Alpha Clipping" checkbox
                        //mat.SetFloat("_RenderQueueType", 2);       // 2 corresponds to the "AlphaTest" render queue
                        //mat.SetFloat("_UseShadowThreshold", 1);    // Optional but recommended: ensures shadows use the cutout

                        EditorUtility.SetDirty(mat);
                        rendererModified = true;
                    }
                    if (rendererModified) modifiedRendererCount++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "Processing Complete",
                $"Finished processing {targetObjects.Count} object(s).\n" +
                $"Modified materials on {modifiedRendererCount} renderers.",
                "OK");
        }
    }
}

#endif