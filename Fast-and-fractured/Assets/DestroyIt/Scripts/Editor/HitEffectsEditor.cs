using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DestroyIt
{
    [CustomEditor(typeof(HitEffects))]
    public class HitEffectsEditor : Editor
    {
        private Texture deleteButton;
        private HitEffects hitEffects;

        public void OnEnable()
        {
            hitEffects = target as HitEffects;
            deleteButton = Resources.Load("UI_Textures/delete-16x16") as Texture;

            if (hitEffects.effects == null)
                hitEffects.effects = new List<HitEffect>();

            //Default to Everything mask
            if (hitEffects.effects.Count == 0)
                hitEffects.effects.Add(new HitEffect() {hitBy = (HitBy)(-1)}); // -1 == "Everything"
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("When hit by:", GUILayout.Width(100));
            EditorGUILayout.LabelField("Use particle:", GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();
            for (int i=0; i < hitEffects.effects.Count; i++)
            {
                HitEffect hitEffect = hitEffects.effects[i];
                EditorGUILayout.BeginHorizontal();
                hitEffect.hitBy = (HitBy) EditorGUILayout.EnumFlagsField(hitEffect.hitBy, GUILayout.Width(100));
                hitEffect.effect = EditorGUILayout.ObjectField(hitEffect.effect, typeof(GameObject), false) as GameObject;
                
                if (GUILayout.Button("-", GUILayout.Width(22)))
                {
                    if (hitEffects.effects.Count > 1)
                        hitEffects.effects.Remove(hitEffect);
                    else
                        Debug.Log("Cannot remove the last remaining Hit Effect. Remove the script entirely if you don't want a hit effect to occur.");
                }
                EditorGUILayout.EndHorizontal(); 
            }
            
            // Add Button
            EditorGUILayout.Separator();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", GUILayout.Width(15));
            if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(30)))
                hitEffects.effects.Add(new HitEffect() {hitBy = (HitBy)(-1)}); // Add the first available tag.
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Separator();
            EditorGUILayout.EndVertical();
            
            if (hitEffects != null && GUI.changed && !Application.isPlaying)
            {
                EditorUtility.SetDirty(hitEffects);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
        }
    }
}