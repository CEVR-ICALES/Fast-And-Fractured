using System;
using System.Collections.Generic;
using System.Linq;
using FMODUnity;
using UnityEditor;
using UnityEngine;
using Utilities.Audio.Editor;

namespace Utilities.Audio.Fmod.Editor
{
    [InitializeOnLoad]
    public static class FmodCatalogSynchronizer
    {
        private const double CACHE_CHECK_INTERVAL_SECONDS = 1;
        private static DateTime _lastCacheTime;
        private static double _nextCheck;

        static FmodCatalogSynchronizer() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup < _nextCheck || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            _nextCheck = EditorApplication.timeSinceStartup + CACHE_CHECK_INTERVAL_SECONDS;
            DateTime cacheTime = EventManager.CacheTime;
            if (cacheTime == DateTime.MinValue || cacheTime == _lastCacheTime) return;
            _lastCacheTime = cacheTime;
            foreach (string guid in AssetDatabase.FindAssets("t:AudioCueCatalog", new[] { "Assets" }))
            {
                var catalog = AssetDatabase.LoadAssetAtPath<AudioCueCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if (catalog == null || catalog.BackendId != "fmod") continue;
                if (!Synchronize(catalog)) continue;
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
            }
        }

        public static bool Synchronize(AudioCueCatalog catalog) => Synchronize(catalog,
            EventManager.Events.Select(e => new AudioBackendReference(e.Guid.ToString(), e.Path.Substring(FmodAudioEditorBridge.EVENT_PREFIX.Length))));

        public static bool Synchronize(AudioCueCatalog catalog, IEnumerable<AudioBackendReference> snapshot)
        {
            if (catalog == null || catalog.BackendId != "fmod") return false;
            var resources = snapshot.Where(r => !string.IsNullOrEmpty(r.StableId)).ToArray();
            var byId = resources.GroupBy(r => r.StableId, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var byPath = resources.GroupBy(r => r.Path, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            bool changed = false;
            foreach (var entry in catalog.Entries)
            {
                if (entry == null) continue;
                AudioBackendReference current;
                if (!string.IsNullOrEmpty(entry.backendStableId))
                {
                    if (!byId.TryGetValue(entry.backendStableId, out current)) continue;
                }
                else
                {
                    string path = entry.backendCueId ?? "";
                    if (path.StartsWith("event:/", StringComparison.Ordinal)) path = path.Substring(FmodAudioEditorBridge.EVENT_PREFIX.Length);
                    if (string.IsNullOrEmpty(path) || !byPath.TryGetValue(path, out current)) continue;
                }
                if (entry.backendStableId == current.StableId && entry.backendCueId == current.Path) continue;
                entry.backendStableId = current.StableId;
                entry.backendCueId = current.Path;
                changed = true;
            }
            if (changed) catalog.Invalidate();
            return changed;
        }
    }
}
