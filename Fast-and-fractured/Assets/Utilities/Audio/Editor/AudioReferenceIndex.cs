using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Utilities.Audio.Editor
{
    /// <summary>Indexes one asset per step, retaining stable locations rather than loaded scene or prefab objects.</summary>
    public sealed class AudioReferenceIndex
    {
        private const string EVENT_PREFIX = "event:/";
        private const string AUDIO_CLIP_FILE_ID_HINT = "8300000";
        private const int MAXIMUM_PREFAB_SOURCE_DEPTH = 32;

        public readonly List<AudioReferenceRecord> Records = new List<AudioReferenceRecord>();
        public readonly List<string> Errors = new List<string>();
        private readonly Queue<string> _pending = new Queue<string>();
        private readonly HashSet<string> _cueIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _cueNames = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _clipGuids = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _codeIds = new Dictionary<string, string>(StringComparer.Ordinal);
        private int _total;
        public bool Running => _pending.Count > 0;
        public bool Cancelled { get; private set; }
        public float Progress => _total == 0 ? 1f : 1f - (float)_pending.Count / _total;
        public string CurrentPath { get; private set; }
        public int Skipped { get; private set; }

        public void Begin(AudioCueCatalog catalog)
        {
            Records.Clear(); Errors.Clear(); _pending.Clear(); _cueIds.Clear(); _cueNames.Clear(); _clipGuids.Clear(); Skipped = 0;
            Cancelled = false; CurrentPath = null; _codeIds.Clear();
            foreach (string id in AudioReferenceAdapters.CueChoices) _cueIds.Add(id);
            if (catalog != null)
            {
                foreach (var entry in catalog.Entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                    _cueIds.Add(entry.id);
                    _cueNames[entry.id] = entry.displayName;
                    if (!string.IsNullOrEmpty(entry.codeName)) _codeIds[entry.codeName] = entry.id;
                }
                ScanAssetObjects(AssetDatabase.GetAssetPath(catalog));
            }
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" })) _clipGuids.Add(guid);
            foreach (string path in AssetDatabase.GetAllAssetPaths().OrderBy(p => p, StringComparer.Ordinal))
            {
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("/FMOD/Cache/") || path.EndsWith("GameAudioCues.g.cs", StringComparison.Ordinal) || path == AssetDatabase.GetAssetPath(catalog)) continue;
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".prefab" || ext == ".unity" || ext == ".asset" || ext == ".playable" || ext == ".anim" || ext == ".controller" || ext == ".overridecontroller" || ext == ".cs") _pending.Enqueue(path);
            }
            _total = _pending.Count;
        }

        public void Cancel()
        {
            if (_pending.Count > 0) Cancelled = true;
            _pending.Clear();
        }

        public void Step()
        {
            if (!Running) return;
            CurrentPath = _pending.Dequeue();
            Application.logMessageReceived += CaptureIssue;
            try
            {
                if (CurrentPath.EndsWith(".cs", StringComparison.Ordinal)) ScanCode(CurrentPath);
                else if (!MightContainAudio(CurrentPath)) Skipped++;
                else if (CurrentPath.EndsWith(".unity", StringComparison.Ordinal)) ScanScene(CurrentPath);
                else ScanAssetObjects(CurrentPath);
            }
            catch (Exception e) { Errors.Add(CurrentPath + ": " + e.GetType().Name + " — " + e.Message); }
            finally { Application.logMessageReceived -= CaptureIssue; }
        }

        private void CaptureIssue(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception) return;
            string message = CurrentPath + ": " + condition;
            if (!Errors.Contains(message)) Errors.Add(message);
        }

        public void ScanSingle(string path)
        {
            if (path.EndsWith(".cs", StringComparison.Ordinal)) ScanCode(path);
            else if (path.EndsWith(".unity", StringComparison.Ordinal)) ScanScene(path);
            else ScanAssetObjects(path);
        }

        private bool MightContainAudio(string path)
        {
            using (var reader = new StreamReader(path))
            {
                string line = reader.ReadLine();
                if (line == null || !line.StartsWith("%YAML", StringComparison.Ordinal)) return true;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains("event:/") || line.Contains("Guid:") || line.Contains(AUDIO_CLIP_FILE_ID_HINT) || line.Contains("m_audioClip") || line.Contains("backendCueId") || line.IndexOf("cueId", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (line.IndexOf("guid:", StringComparison.Ordinal) >= 0)
                        foreach (string guid in _clipGuids) if (line.Contains(guid)) return true;
                    foreach (string id in _cueIds) if (!string.IsNullOrEmpty(id) && line.Contains(id)) return true;
                }
            }
            return false;
        }

        private void ScanCode(string path)
        {
            int lineNumber = 0;
            foreach (string line in File.ReadLines(path))
            {
                lineNumber++;
                foreach (Match symbol in Regex.Matches(line, @"\bGameAudioCues\.([A-Za-z_][A-Za-z0-9_]*)"))
                {
                    if (!_codeIds.TryGetValue(symbol.Groups[1].Value, out var id)) continue;
                    Records.Add(new AudioReferenceRecord { IsCode = true, Line = lineNumber, CueId = id, DisplayName = NameFor(id), AdapterId = "code", Value = new AudioReferenceValue { Text = id, Display = symbol.Value }, Location = new AudioReferenceLocation { AssetPath = path, ObjectName = Path.GetFileName(path), ObjectType = "C# typed audio cue", PropertyPath = "Line " + lineNumber } });
                }
                foreach (Match match in Regex.Matches(line, "\"([^\"\\\\]*(?:\\\\.[^\"\\\\]*)*)\""))
                {
                    string id = match.Groups[1].Value;
                    if (id.StartsWith(EVENT_PREFIX, StringComparison.Ordinal)) id = id.Substring(EVENT_PREFIX.Length);
                    if (!_cueIds.Contains(id)) continue;
                    Records.Add(new AudioReferenceRecord { IsCode = true, Line = lineNumber, CueId = id, DisplayName = NameFor(id), AdapterId = "code", Value = new AudioReferenceValue { Text = id, Display = id }, Location = new AudioReferenceLocation { AssetPath = path, ObjectName = Path.GetFileName(path), ObjectType = "C# literal", PropertyPath = "Line " + lineNumber } });
                }
            }
        }

        private string NameFor(string id) => _cueNames.TryGetValue(id, out var name) && !string.IsNullOrEmpty(name) ? name : id;

        private void ScanScene(string path)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool preview = !scene.IsValid() || !scene.isLoaded;
            if (preview) scene = EditorSceneManager.OpenPreviewScene(path);
            try { foreach (var root in scene.GetRootGameObjects()) ScanHierarchy(root, path); }
            finally
            {
                if (preview)
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                    EditorUtility.UnloadUnusedAssetsImmediate(true);
                }
            }
        }

        private void ScanAssetObjects(string path)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root != null) { ScanHierarchy(root, path); return; }
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset != null && !(asset is MonoScript)) ScanObject(asset, path);
        }

        private void ScanHierarchy(GameObject root, string path)
        {
            foreach (var component in root.GetComponentsInChildren<Component>(true))
                if (component is MonoBehaviour || component is AudioSource || component is UnityEngine.Playables.PlayableDirector) ScanObject(component, path);
        }

        private void ScanObject(Object target, string path)
        {
            using (var so = new SerializedObject(target))
            {
                if (target is AudioCueCatalog catalog)
                {
                    var entries = so.FindProperty("entries");
                    for (int i = 0; i < entries.arraySize; i++)
                    {
                        var entry = entries.GetArrayElementAtIndex(i);
                        var property = entry.FindPropertyRelative("backendCueId");
                        var adapter = AudioReferenceAdapters.Find("cue-id");
                        Add(target, path, property, adapter, entry.FindPropertyRelative("id").stringValue, true, entry.FindPropertyRelative("displayName").stringValue);
                    }
                    return;
                }
                var p = so.GetIterator();
                bool children = true;
                while (p.Next(children))
                {
                    children = true;
                    var adapter = AudioReferenceAdapters.Match(p);
                    if (adapter == null && p.propertyType == SerializedPropertyType.String && _cueIds.Contains(p.stringValue)) adapter = AudioReferenceAdapters.Find("cue-id");
                    if (adapter == null) continue;
                    Add(target, path, p, adapter, adapter.Id == "cue-id" ? adapter.Read(p).Text : "", false, null);
                    children = false;
                }
            }
        }

        private void Add(Object target, string path, SerializedProperty p, IAudioReferenceAdapter adapter, string cueId, bool catalog, string displayName)
        {
            if (adapter == null) return;
            var record = new AudioReferenceRecord { Location = AudioReferenceObjects.Locate(target, path, p.propertyPath), AdapterId = adapter.Id, Value = adapter.Read(p), CueId = cueId, IsCatalog = catalog, DisplayName = !string.IsNullOrEmpty(displayName) ? displayName : !string.IsNullOrEmpty(cueId) ? NameFor(cueId) : target.name + " / " + p.displayName, DependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString() };
            if (target is Component && PrefabUtility.IsPartOfPrefabInstance(target) && !p.prefabOverride)
            {
                Object source = PrefabUtility.GetCorrespondingObjectFromSource(target);
                int depth = 0;
                while (source != null && depth++ < MAXIMUM_PREFAB_SOURCE_DEPTH)
                {
                    string sourcePath = AssetDatabase.GetAssetPath(source);
                    if (!string.IsNullOrEmpty(sourcePath) && sourcePath != path) record.InheritedOrigin = AudioReferenceObjects.Locate(source, sourcePath, p.propertyPath);
                    using (var sourceSo = new SerializedObject(source))
                    {
                        var sourceProperty = sourceSo.FindProperty(p.propertyPath);
                        if (sourceProperty == null || sourceProperty.prefabOverride) break;
                    }
                    source = PrefabUtility.GetCorrespondingObjectFromSource(source);
                }
            }
            Records.Add(record);
        }
    }

    public static class AudioReferenceObjects
    {
        public static AudioReferenceLocation Locate(Object target, string path, string propertyPath)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out string guid, out long id);
            var result = new AudioReferenceLocation { AssetPath = path, LocalId = id, ObjectType = target.GetType().FullName, ObjectName = target.name, PropertyPath = propertyPath, GlobalId = GlobalObjectId.GetGlobalObjectIdSlow(target).ToString() };
            if (target is Component component)
            {
                var indices = new List<int>();
                for (var t = component.transform; t != null; t = t.parent) indices.Insert(0, t.GetSiblingIndex());
                result.Hierarchy = string.Join("/", indices);
                result.ComponentIndex = Array.IndexOf(component.GetComponents<Component>(), component);
            }
            return result;
        }

        public static Object Resolve(AudioReferenceLocation location, GameObject[] roots = null)
        {
            if (roots != null && roots.Length > 0 && GlobalObjectId.TryParse(location.GlobalId, out var liveId))
            {
                var live = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(liveId) as Component;
                if (live != null && live.gameObject.scene == roots[0].scene && live.GetType().FullName == location.ObjectType) return live;
            }
            if (roots == null)
            {
                if (GlobalObjectId.TryParse(location.GlobalId, out var global))
                {
                    var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(global);
                    if (obj != null && obj.GetType().FullName == location.ObjectType) return obj;
                }
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(location.AssetPath))
                {
                    if (obj == null) continue;
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out long id);
                    if (id == location.LocalId && obj.GetType().FullName == location.ObjectType) return obj;
                }
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(location.AssetPath);
                if (prefab != null) roots = new[] { prefab };
            }
            if (roots == null || string.IsNullOrEmpty(location.Hierarchy)) return null;
            int[] indices = location.Hierarchy.Split('/').Select(int.Parse).ToArray();
            int rootIndex = roots.Length == 1 && location.AssetPath.EndsWith(".prefab", StringComparison.Ordinal) ? 0 : indices[0];
            if (rootIndex < 0 || rootIndex >= roots.Length) return null;
            var transform = roots[rootIndex].transform;
            for (int i = 1; i < indices.Length; i++)
            {
                if (indices[i] < 0 || indices[i] >= transform.childCount) return null;
                transform = transform.GetChild(indices[i]);
            }
            var components = transform.GetComponents<Component>();
            if (location.ComponentIndex < 0 || location.ComponentIndex >= components.Length) return null;
            var component = components[location.ComponentIndex];
            return component != null && component.GetType().FullName == location.ObjectType && component.name == location.ObjectName ? component : null;
        }
    }
}
