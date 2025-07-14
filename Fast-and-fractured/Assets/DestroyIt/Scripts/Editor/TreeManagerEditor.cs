using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DestroyIt
{
    [CustomEditor(typeof(TreeManager))]
    public class TreeManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            TreeManager treeManager = target as TreeManager;
            
            base.OnInspectorGUI();

            EditorGUILayout.Separator();
            
            if (GUILayout.Button("Update Trees", EditorStyles.toolbarButton, GUILayout.Width(160)))
                TreeManager.Instance.UpdateTrees();

            EditorGUILayout.Separator();
            
            if (GUI.changed && !Application.isPlaying)
            {
                EditorUtility.SetDirty(treeManager);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
        }
    }
}