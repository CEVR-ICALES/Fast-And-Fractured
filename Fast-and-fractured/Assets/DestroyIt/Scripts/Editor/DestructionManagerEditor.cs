using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DestroyIt
{
    [CustomEditor(typeof(DestructionManager))]
    public class DestructionManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DestructionManager destManager = target as DestructionManager;
            
            EditorGUILayout.Separator();
            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField(new GUIContent("Auto-Deactivate", "Provides options to gain better performance by deactivating Destructible scripts when they are far away from the player."), EditorStyles.boldLabel);
            
            GUILayout.BeginHorizontal();
            destManager.autoDeactivateDestructibles = EditorGUILayout.Toggle(destManager.autoDeactivateDestructibles, GUILayout.Width(20));
            EditorGUILayout.LabelField(new GUIContent("Destructible Objects", "If true, Destructible scripts will be deactivated on start, and will re-activate any time they are inside a trigger collider with the ActivateDestructibles script on it."));
            GUILayout.EndHorizontal();
            if (destManager.autoDeactivateDestructibles)
            {
                GUILayout.Label(new GUIContent("NOTE: You'll need to put a trigger collider with ActivateDestructibles script to re-activate Destructibles within range. See the ActivateDestructiblesArea prefab for an example."), EditorStyles.helpBox);
            }
            GUILayout.BeginHorizontal();
            destManager.autoDeactivateDestructibleTerrainObjects = EditorGUILayout.Toggle(destManager.autoDeactivateDestructibleTerrainObjects, GUILayout.Width(20));
            EditorGUILayout.LabelField(new GUIContent("Destructible Terrain Objects", "If true, Destructible terrain object scripts will be deactivated on start, and will activate any time they are inside a trigger collider with the ActivateDestructibles script on it."));
            GUILayout.EndHorizontal();

            if (destManager.autoDeactivateDestructibleTerrainObjects)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("", GUILayout.Width(15));
                destManager.destructibleTreesStayDeactivated = EditorGUILayout.Toggle(destManager.destructibleTreesStayDeactivated, GUILayout.Width(20));
                EditorGUILayout.LabelField(new GUIContent("Don't Reactivate Terrain Objects", "If true, Destructible terrain object scripts will not be re-activated by ActivateDestructibles scripts. Recommended to leave this true for performance, unless you need to move terrain objects during the game or use progressive damage textures on them."));
                GUILayout.EndHorizontal();
            }

            if (destManager.autoDeactivateDestructibles || destManager.autoDeactivateDestructibleTerrainObjects)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Deactivate After", "The time in seconds to automatically deactivate Destructible scripts when they are active and outside an ActivateDestructibles trigger area."), GUILayout.Width(100));
                destManager.deactivateAfter = EditorGUILayout.FloatField(destManager.deactivateAfter, GUILayout.Width(30));
                EditorGUILayout.LabelField("seconds", GUILayout.Width(50));
                GUILayout.EndHorizontal();
            }
            
            EditorGUILayout.Separator();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Separator();
            
            base.OnInspectorGUI();

            destManager.useCameraDistanceLimit = EditorGUILayout.Toggle(new GUIContent("Camera Distance Limit", "If true, destructible objects beyond the specified distance from the main camera will be destroyed in a more limiting (ie, higher performance) way, such as the fallback particle effect."), destManager.useCameraDistanceLimit);
            if (destManager.useCameraDistanceLimit)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("", GUILayout.Width(15));
                destManager.cameraDistanceLimit = EditorGUILayout.IntField("If distance to camera >", destManager.cameraDistanceLimit);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("", GUILayout.Width(15));
                EditorGUILayout.LabelField("Limit destruction to", GUILayout.Width(100));
                EditorGUILayout.EnumPopup(DestructionType.ParticleEffect);
                GUILayout.EndHorizontal();
            }

            if (GUI.changed && !Application.isPlaying)
            {
                EditorUtility.SetDirty(destManager);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
        }
    }
}