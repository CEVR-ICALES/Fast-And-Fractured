using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DestroyIt
{
    [CustomEditor(typeof(StructuralSupport))]
    public class StructuralSupportEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            StructuralSupport strSupport = target as StructuralSupport;
            
            base.OnInspectorGUI();

            EditorGUILayout.Separator();
            
            if (GUILayout.Button("Add Supports",  GUILayout.Width(170)))
            {
                strSupport.AddStructuralSupport();
            }

            if (GUILayout.Button("Remove Supports", GUILayout.Width(170)))
            {
                strSupport.RemoveStructuralSupport();
            }

            EditorGUILayout.Separator();
            
            if (GUI.changed && !Application.isPlaying)
            {
                EditorUtility.SetDirty(strSupport);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
        }
    }
}