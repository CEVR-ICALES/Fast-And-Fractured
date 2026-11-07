using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Utilities.Audio.Editor
{
    public sealed class AudioReferenceValue
    {
        public string Text = "";
        public string StableId = "";
        public Object Asset;
        public string Fingerprint = "";
        public string Display = "";
    }

    public interface IAudioReferenceAdapter
    {
        string Id { get; }
        bool Matches(SerializedProperty property);
        AudioReferenceValue Read(SerializedProperty property);
        bool Validate(AudioReferenceValue value, out string error);
        void Write(SerializedProperty property, AudioReferenceValue value);
    }

    public static class AudioReferenceAdapters
    {
        private static readonly List<IAudioReferenceAdapter> _items = new List<IAudioReferenceAdapter>();
        public static Func<IReadOnlyList<string>> BackendCues;
        public static Func<string, bool> BackendCueExists;
        public static Func<string, IAudioEditorPreview> BeginCuePreview;
        public static Func<AudioBackendReference, IAudioEditorPreview> BeginResourcePreview;
        public static Func<string, AudioBackendReference> ResolveBackendReference;
        public static Func<AudioBackendReference, bool> BackendReferenceExists;
        public static string BackendProviderId;
        public static Func<AudioCueCatalog, bool> SynchronizeCatalogBindings;

        public static void Register(IAudioReferenceAdapter adapter)
        {
            _items.RemoveAll(a => a.Id == adapter.Id);
            _items.Insert(0, adapter);
        }

        public static IAudioReferenceAdapter Find(string id) => _items.Find(a => a.Id == id);
        public static IAudioReferenceAdapter Match(SerializedProperty property) => _items.Find(a => a.Matches(property));
        public static IReadOnlyList<string> CueChoices => BackendCues != null ? BackendCues() : Array.Empty<string>();
    }

    [Serializable]
    public sealed class AudioReferenceLocation
    {
        public string AssetPath;
        public string GlobalId;
        public long LocalId;
        public string Hierarchy;
        public int ComponentIndex = -1;
        public string ObjectType;
        public string ObjectName;
        public string PropertyPath;
    }

    public sealed class AudioReferenceRecord
    {
        public AudioReferenceLocation Location;
        public AudioReferenceLocation InheritedOrigin;
        public string AdapterId;
        public AudioReferenceValue Value;
        public string DisplayName;
        public string CueId;
        public string DependencyHash;
        public bool IsCatalog;
        public bool IsCode;
        public int Line;
        public string SearchText => (DisplayName + " " + CueId + " " + Value.Display + " " + Location.AssetPath + " " + Location.ObjectType + " " + Location.ObjectName + " " + Location.PropertyPath).ToLowerInvariant();
    }

    [InitializeOnLoad]
    public static class BuiltinAudioReferenceAdapters
    {
        static BuiltinAudioReferenceAdapters()
        {
            AudioReferenceAdapters.Register(new ClipAdapter());
            AudioReferenceAdapters.Register(new CueAdapter());
        }

        private sealed class ClipAdapter : IAudioReferenceAdapter
        {
            public string Id => "unity-clip";
            public bool Matches(SerializedProperty p) => p.propertyType == SerializedPropertyType.ObjectReference && (p.objectReferenceValue is AudioClip || p.type.Contains("AudioClip"));
            public AudioReferenceValue Read(SerializedProperty p)
            {
                var clip = p.objectReferenceValue;
                string path = clip != null ? AssetDatabase.GetAssetPath(clip) : "";
                string fingerprint = "";
                if (clip != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string guid, out long id)) fingerprint = guid + ":" + id;
                return new AudioReferenceValue { Asset = clip, Fingerprint = fingerprint, Display = clip != null ? clip.name + " (" + path + ")" : "Unassigned" };
            }
            public bool Validate(AudioReferenceValue value, out string error)
            {
                error = value.Asset != null && !(value.Asset is AudioClip) ? "The reference must be an AudioClip." : null;
                return error == null;
            }
            public void Write(SerializedProperty p, AudioReferenceValue value) => p.objectReferenceValue = value.Asset;
        }

        private sealed class CueAdapter : IAudioReferenceAdapter
        {
            public string Id => "cue-id";
            public bool Matches(SerializedProperty p)
            {
                if (p.propertyType == SerializedPropertyType.Generic && p.type == nameof(AudioCueId)) return true;
                if (p.propertyType != SerializedPropertyType.String) return false;
                string name = p.name.ToLowerInvariant();
                return name.EndsWith("cue") || name.Contains("cueid") || name == "audiocue";
            }
            private static SerializedProperty StableId(SerializedProperty p)
            {
                if (p.name != "backendCueId") return null;
                int dot = p.propertyPath.LastIndexOf('.');
                return dot >= 0 ? p.serializedObject.FindProperty(p.propertyPath.Substring(0, dot) + ".backendStableId") : null;
            }
            public AudioReferenceValue Read(SerializedProperty p)
            {
                if (p.propertyType == SerializedPropertyType.Generic) p = p.FindPropertyRelative("value");
                string stableId = StableId(p)?.stringValue ?? "";
                var reference = new AudioBackendReference(stableId, p.stringValue);
                bool missing = !string.IsNullOrEmpty(stableId) && AudioReferenceAdapters.BackendReferenceExists != null && !AudioReferenceAdapters.BackendReferenceExists(reference);
                return new AudioReferenceValue { Text = p.stringValue, StableId = stableId, Fingerprint = stableId + "|" + p.stringValue, Display = (missing ? "Missing: " : "") + p.stringValue };
            }
            public bool Validate(AudioReferenceValue value, out string error) { error = null; return true; }
            public void Write(SerializedProperty p, AudioReferenceValue value)
            {
                if (p.propertyType == SerializedPropertyType.Generic) p = p.FindPropertyRelative("value");
                var stableId = StableId(p);
                if (stableId != null && p.serializedObject.targetObject is AudioCueCatalog catalog && catalog.BackendId == AudioReferenceAdapters.BackendProviderId && AudioReferenceAdapters.ResolveBackendReference != null)
                {
                    var reference = string.IsNullOrEmpty(value.Text) ? default : AudioReferenceAdapters.ResolveBackendReference(value.Text);
                    if (!string.IsNullOrEmpty(value.Text) && reference.IsEmpty) throw new InvalidOperationException("The selected backend resource does not exist.");
                    p.stringValue = reference.Path ?? "";
                    stableId.stringValue = reference.StableId ?? "";
                }
                else
                {
                    p.stringValue = value.Text ?? "";
                    if (stableId != null) stableId.stringValue = value.StableId ?? "";
                }
            }
        }
    }
}
