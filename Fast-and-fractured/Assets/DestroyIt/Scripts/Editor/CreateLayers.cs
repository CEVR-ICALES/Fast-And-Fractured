using System;
using UnityEngine;
using UnityEditor;

namespace DestroyIt
{
    [InitializeOnLoad]
    public class CreateLayers
    {
        private static readonly string[] layersToCreate = {"DestroyItDebris"}; // put your layers here (comma-separated)
        private static SerializedObject tagManager;

        static CreateLayers()
        {
            for (int i = 0; i < layersToCreate.Length; i++)
            {
                int layer = LayerMask.NameToLayer(layersToCreate[i]);
                if (layer != -1) // Layer already exists, so exit.
                    return;

                // Layer doesn't exist, so create it.
                CreateLayer(layersToCreate[i]);
            }
        }

        static void CreateLayer(string layerName)
        {
            if (tagManager == null)
                tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            if (tagManager == null)
            {
                Debug.Log("Could not load asset 'ProjectSettings/TagManager.asset'.");
                return;
            }
             
            SerializedProperty layersProp = tagManager.FindProperty("layers");
            for (int i = 8; i <= 31; i++)
            {
                SerializedProperty sp = layersProp.GetArrayElementAtIndex(i);
                if (sp != null && String.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = layerName;
                    break;
                }
            }
             
            tagManager.ApplyModifiedProperties();
        }
    }
}
