using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Utilities.Audio.Editor
{
    public static class AudioReferenceWriter
    {
        public static string Apply(AudioReferenceRecord record, AudioReferenceValue value, bool editOrigin, bool saveClosedScene, string displayName = null)
        {
            if (record.IsCode) throw new InvalidOperationException("C# usages are read-only. Edit the catalog entry instead.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before editing persistent references.");
            var adapter = AudioReferenceAdapters.Find(record.AdapterId);
            if (adapter == null) throw new InvalidOperationException("The reference adapter is unavailable.");
            if (!adapter.Validate(value, out string error)) throw new InvalidOperationException(error);
            var location = editOrigin && record.InheritedOrigin != null ? record.InheritedOrigin : record.Location;
            string path = location.AssetPath;
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0)
                throw new InvalidOperationException("The source is read-only.");
            if (!editOrigin && !string.IsNullOrEmpty(record.DependencyHash) && AssetDatabase.GetAssetDependencyHash(path).ToString() != record.DependencyHash)
                throw new InvalidOperationException("The asset changed after indexing. Refresh the search.");

            if (path.EndsWith(".unity", StringComparison.Ordinal)) return ApplyScene(record, location, adapter, value, saveClosedScene, displayName);
            if (path.EndsWith(".prefab", StringComparison.Ordinal)) return ApplyPrefab(record, location, adapter, value, displayName);
            var target = AudioReferenceObjects.Resolve(location);
            Write(target, location, adapter, record.Value.Fingerprint, value, record.IsCatalog, displayName);
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
            return "Reference saved in " + path;
        }

        private static string ApplyPrefab(AudioReferenceRecord record, AudioReferenceLocation location, IAudioReferenceAdapter adapter, AudioReferenceValue value, string displayName)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == location.AssetPath)
            {
                var target = AudioReferenceObjects.Resolve(location, new[] { stage.prefabContentsRoot });
                Write(target, location, adapter, record.Value.Fingerprint, value, record.IsCatalog, displayName);
                EditorSceneManager.MarkSceneDirty(stage.scene);
                return "Reference changed in the open Prefab Stage. Save it from that Stage. Undo is available.";
            }
            var root = PrefabUtility.LoadPrefabContents(location.AssetPath);
            try
            {
                var target = AudioReferenceObjects.Resolve(location, new[] { root });
                Write(target, location, adapter, record.Value.Fingerprint, value, record.IsCatalog, displayName, false);
                PrefabUtility.SaveAsPrefabAsset(root, location.AssetPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return "Reference saved in " + location.AssetPath + ". The temporary context has been unloaded.";
        }

        private static string ApplyScene(AudioReferenceRecord record, AudioReferenceLocation location, IAudioReferenceAdapter adapter, AudioReferenceValue value, bool saveClosed, string displayName)
        {
            Scene scene = SceneManager.GetSceneByPath(location.AssetPath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened && !saveClosed) throw new InvalidOperationException("Confirm opening and saving the closed scene.");
            if (opened) scene = EditorSceneManager.OpenScene(location.AssetPath, OpenSceneMode.Additive);
            try
            {
                var target = AudioReferenceObjects.Resolve(location, scene.GetRootGameObjects());
                Write(target, location, adapter, record.Value.Fingerprint, value, record.IsCatalog, displayName, !opened);
                EditorSceneManager.MarkSceneDirty(scene);
                if (opened && !EditorSceneManager.SaveScene(scene)) throw new IOException("The scene could not be saved.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            return opened ? "Reference saved only in " + location.AssetPath + ". Undo does not persist after unloading." : "Reference changed in the open scene. It remains dirty, with Undo. Other changes have not been saved.";
        }

        private static void Write(Object target, AudioReferenceLocation location, IAudioReferenceAdapter adapter, string expected, AudioReferenceValue value, bool catalog, string displayName, bool recordUndo = true)
        {
            if (target == null) throw new InvalidOperationException("The owner no longer exists. Refresh the index.");
            using (var so = new SerializedObject(target))
            {
                var property = so.FindProperty(location.PropertyPath);
                if (property == null || !adapter.Matches(property)) throw new InvalidOperationException("The property or its type changed. Refresh the index.");
                if (adapter.Read(property).Fingerprint != expected) throw new InvalidOperationException("The reference changed after indexing and has not been overwritten.");
                int undoGroup = -1;
                if (recordUndo)
                {
                    Undo.FlushUndoRecordObjects();
                    Undo.IncrementCurrentGroup();
                    undoGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Change audio reference");
                    Undo.RecordObject(target, "Change audio reference");
                }
                adapter.Write(property, value);
                if (catalog && displayName != null)
                {
                    string prefix = location.PropertyPath.Substring(0, location.PropertyPath.LastIndexOf('.'));
                    var name = so.FindProperty(prefix + ".displayName");
                    if (name != null) name.stringValue = displayName;
                }
                if (recordUndo)
                {
                    so.ApplyModifiedProperties();
                    Undo.FlushUndoRecordObjects();
                    Undo.CollapseUndoOperations(undoGroup);
                    Undo.IncrementCurrentGroup();
                }
                else so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            if (target is AudioCueCatalog cueCatalog) cueCatalog.Invalidate();
        }
    }
}
