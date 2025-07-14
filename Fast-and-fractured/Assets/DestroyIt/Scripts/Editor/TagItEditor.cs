using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
// ReSharper disable InconsistentNaming

namespace DestroyIt
{
    [CustomEditor(typeof(TagIt))]
    public class TagItEditor : Editor
    {
        private Texture deleteButton;
        private TagIt tagIt;

        public void OnEnable()
        {
            tagIt = target as TagIt;
            deleteButton = Resources.Load("UI_Textures/delete-16x16") as Texture;

            if (tagIt.tags == null)
                tagIt.tags = new List<Tag>();

            if (tagIt.tags.Count == 0)
                tagIt.tags.Add(Tag.Untagged);
        }

        public override void OnInspectorGUI()
        {
            string removeTagName = "";
            Tag changeTagFrom = Tag.Untagged;
            Tag changeTagTo = Tag.Untagged;
            List<string> tagOptions;
            GUIStyle style = new GUIStyle();
            style.padding.top = 2;

            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField("Tags on this game object:");
            foreach(Tag tag in tagIt.tags)
            {
                // Reset the list of tag options for each tag.
                tagOptions = Enum.GetNames(typeof(Tag)).ToList();
                tagOptions.Sort();

                // Remove the other tags this object already has.
                foreach (Tag t in tagIt.tags)
                {
                    if (t == tag) continue; // don't remove the current tag.
                    tagOptions.Remove(Enum.GetName(typeof(Tag), t));
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("", GUILayout.Width(15));
                // get the selected option
                int selectedIndex = tagOptions.IndexOf(Enum.GetName(typeof(Tag), tag));
                selectedIndex = EditorGUILayout.Popup(selectedIndex,tagOptions.ToArray());
                Tag newTag = (Tag)Enum.Parse(typeof(Tag), tagOptions[selectedIndex]);

                if (tag != newTag)
                {
                    changeTagFrom = tag;
                    changeTagTo = newTag;
                }

                if (GUILayout.Button(deleteButton, style, GUILayout.Width(16)))
                    removeTagName = tagOptions[selectedIndex]; // flag for removal
                EditorGUILayout.EndHorizontal();
            }

            // Change tag (if neccessary).
            if (changeTagFrom != changeTagTo)
            {
                int idx = tagIt.tags.IndexOf(changeTagFrom);
                tagIt.tags[idx] = changeTagTo;
            }

            // Remove selected tag (if any).
            if (tagIt.tags.Count > 1 && removeTagName != "")
            {
                Tag removeTag = (Tag)Enum.Parse(typeof(Tag), removeTagName);
                tagIt.tags.Remove(removeTag);
            }
            else if (tagIt.tags.Count == 1 && removeTagName != "") // if we are removing the last item, just remove the TagIt script.
                DestroyImmediate(tagIt.gameObject.GetComponent<TagIt>());

            // Add/Remove Buttons
            tagOptions = Enum.GetNames(typeof(Tag)).ToList();
            tagOptions.Sort();
            List<string> usedTags = new List<string>();
            foreach(Tag tag in tagIt.tags)
                usedTags.Add(Enum.GetName(typeof(Tag), tag));
            
            usedTags.Sort();
            bool showAddButton = tagIt.tags.Count < tagOptions.Count;
            if (showAddButton)
            {
                EditorGUILayout.Separator();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("", GUILayout.Width(15));
                if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(30)))
                {
                    tagOptions.RemoveAll(x => usedTags.Contains(x));
                    Tag firstAvailableTag = (Tag)Enum.Parse(typeof(Tag), tagOptions[0]);
                    tagIt.tags.Add(firstAvailableTag); // Add the first available tag.
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.Separator();
            EditorGUILayout.EndVertical();

            if (tagIt != null && GUI.changed && !Application.isPlaying)
            {
                EditorUtility.SetDirty(tagIt);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
        }
    }
}