using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace DestroyIt
{
    [CustomEditor(typeof(ObjectPool))]
    public class ObjectPoolEditor : Editor
    {
        private Texture deleteButton;
        private Texture lockOff;
        private Texture lockOn;
        private readonly string[] delimiter = {":|:"}; // The complex delimiter for loading/saving Object Pool to file. Should be something you won't use in your prefab names.

        private void OnEnable()
        {
            deleteButton = Resources.Load("UI_Textures/delete-16x16") as Texture;
            lockOff = Resources.Load("UI_Textures/lock-off-16x16") as Texture;
            lockOn = Resources.Load("UI_Textures/lock-on-16x16") as Texture;
        }

        public override void OnInspectorGUI()
        {
            ObjectPool objectPool = target as ObjectPool;

            List<PoolEntry> changeEntries = new List<PoolEntry>();
            if (objectPool != null && objectPool.prefabsToPool != null)
                changeEntries = objectPool.prefabsToPool.ToList();
            List<PoolEntry> removeEntries = new List<PoolEntry>();
            GUIStyle style = new GUIStyle();
            style.padding.top = 2;

            if (changeEntries.Count > 0)
            {
                EditorGUILayout.LabelField("Prefab | Count | Pooled Only");
                List<string> previouslyUsedNames = new List<string>();

                foreach(PoolEntry entry in changeEntries)
                {
                    // Remove duplicate entries
                    if (entry != null && entry.Prefab != null)
                    {
                        if (previouslyUsedNames.Contains(entry.Prefab.name))
                        {
                            Debug.LogWarning("Prefab \"" + entry.Prefab.name + "\" already exists in Object Pool (item #" + (previouslyUsedNames.IndexOf(entry.Prefab.name) + 1) + ").");
                            removeEntries.Add(entry);
                            continue;
                        }
                        previouslyUsedNames.Add(entry.Prefab.name);
                    }

                    EditorGUILayout.BeginHorizontal();

                    entry.Prefab = EditorGUILayout.ObjectField(entry.Prefab, typeof(GameObject), false) as GameObject;
                    entry.Count = EditorGUILayout.IntField(entry.Count, GUILayout.Width(20));
                    
                    Texture currentLock = lockOff;
                    if (entry.OnlyPooled)
                        currentLock = lockOn;

                    if (GUILayout.Button(currentLock, style, GUILayout.Width(16)))
                        entry.OnlyPooled = !entry.OnlyPooled;
                    
                    if (GUILayout.Button(deleteButton, style, GUILayout.Width(16)))
                        removeEntries.Add(entry); // flag for removal
                    
                    EditorGUILayout.EndHorizontal();
                }

                // Remove entries flagged for removal
                foreach (PoolEntry entry in removeEntries)
                    changeEntries.Remove(entry);
            }

            // Add entries button
            EditorGUILayout.Separator();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(30)))
                changeEntries.Add(new PoolEntry{ Prefab = null, Count = 1 }); 
            EditorGUILayout.LabelField(" Add a prefab to the pool.");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Separator();

            // Suppress Warnings checkbox
            EditorGUILayout.BeginHorizontal();
            objectPool.suppressWarnings = EditorGUILayout.Toggle(objectPool.suppressWarnings, GUILayout.Width(16));
            EditorGUILayout.LabelField("Suppress Warnings");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Separator();

            // IMPORT from file
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Import", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                string path = EditorUtility.OpenFilePanel("Import Object Pool Save File", SceneManager.GetActiveScene().path.SceneFolder(), "txt");
			    if (path.Length != 0 && File.Exists(path)) 
                {
                    string[] lines = File.ReadAllLines(path);
                    if (EditorUtility.DisplayDialog("Add Items to Object Pool?", "Are you sure you want to import " + lines.Length + " items into the Object Pool from " + Path.GetFileName(path) + "?", "Add Items", "Cancel"))
                    {
                        int itemsModified = 0;
                        int itemsAdded = 0;
                        for (int i = 0; i < lines.Length; i++)
                        {
                            string[] parts = lines[i].Split(delimiter, StringSplitOptions.None);

                            string assetPath = AssetDatabase.GUIDToAssetPath(parts[1]);
                            GameObject prefab = AssetDatabase.LoadAssetAtPath(assetPath, typeof(GameObject)) as GameObject;
                            if (prefab == null)
                            {
                                Debug.LogWarning("Could not find \"" + parts[0] + "\" prefab.");
                                continue;
                            }
                            int count = Convert.ToInt32(parts[2]);
                            bool onlyPooled = Convert.ToBoolean(parts[3]);

                            PoolEntry existingEntry = changeEntries.Find(x => x.Prefab == prefab);
                            // Update existing entries
                            if (existingEntry != null)
                            {
                                bool updatedExisting = false;
                                if (existingEntry.Count < count)
                                {
                                    existingEntry.Count = count;
                                    updatedExisting = true;
                                }
                                if (onlyPooled && !existingEntry.OnlyPooled)
                                {
                                    existingEntry.OnlyPooled = onlyPooled;
                                    updatedExisting = true;
                                }
                                if (updatedExisting)
                                    itemsModified++;
                            }
                            else // Add new entries
                            { 
                                changeEntries.Add(new PoolEntry { Prefab = prefab, Count = count, OnlyPooled = onlyPooled });
                                itemsAdded++;
                            }
                        }
                        Debug.Log(String.Format("{0} imported into Object Pool. Results: {1} new items added, {2} existing items updated.", Path.GetFileName(path), itemsAdded, itemsModified));
                    }
                }
            }

            // CLEAR object pool
            EditorGUILayout.Space();
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(70)) &&
                EditorUtility.DisplayDialog("Clear Object Pool?", "Are you sure you want to remove all objects from the object pool?", "Clear", "Cancel"))
            {
                changeEntries = new List<PoolEntry>();
                Debug.Log("Object Pool cleared.");
            }

            // SAVE to file
            EditorGUILayout.Space();
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                string saveFilePath = EditorUtility.SaveFilePanel("Save Object File To", SceneManager.GetActiveScene().path.SceneFolder(), SceneManager.GetActiveScene().name.Replace(" ", "") + "ObjectPool.txt", "txt");
                //string saveFilePath = SceneManager.GetActiveScene().path.SceneFolder() + "/ObjectPool-SaveFile.txt"; //"Assets/DestroyIt - Core/ObjectPool-SaveFile.txt";
                if (saveFilePath.Length != 0)
                { 
                    if (objectPool.prefabsToPool != null && objectPool.prefabsToPool.Count > 0)
                    {
                        string[] lines = new string[objectPool.prefabsToPool.Count];
                        for (int i = 0; i < objectPool.prefabsToPool.Count; i++)
                        {
                            string assetPath = AssetDatabase.GetAssetPath(objectPool.prefabsToPool[i].Prefab.GetInstanceID());
                            string assetId = AssetDatabase.AssetPathToGUID(assetPath);
                            lines[i] = String.Format("{1}{0}{2}{0}{3}{0}{4}", string.Join("", delimiter), objectPool.prefabsToPool[i].Prefab.name, assetId, objectPool.prefabsToPool[i].Count, objectPool.prefabsToPool[i].OnlyPooled);
                        }
                        File.WriteAllLines(saveFilePath, lines);
                        Debug.Log(lines.Length + " object Pool entries saved to: " + saveFilePath + ".");
                    }
                    else
                        Debug.Log("Object Pool is empty. Nothing to save.");
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Separator();

            // Save changes back to object pool
            objectPool.prefabsToPool = changeEntries;
            if (GUI.changed && !Application.isPlaying)
            {
                EditorUtility.SetDirty(objectPool);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
        }
    }
}