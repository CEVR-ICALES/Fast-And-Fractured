using System;
using System.Collections.Generic;
using System.Linq;
using FMODUnity;
using UnityEditor;
using Utilities.Audio.Editor;

namespace Utilities.Audio.Fmod.Editor
{
    [InitializeOnLoad]
    public sealed class FmodAudioEditorBridge : IAudioReferenceAdapter
    {
        internal const string EVENT_PREFIX = "event:/";
        private static string[] _choices;
        private static int _choiceCount = -1;
        private static DateTime _choiceVersion;
        static FmodAudioEditorBridge()
        {
            AudioReferenceAdapters.Register(new FmodAudioEditorBridge());
            AudioReferenceAdapters.BackendCues = Choices;
            AudioReferenceAdapters.BackendCueExists = value => string.IsNullOrEmpty(value) || EventManager.EventFromPath(FullPath(value)) != null;
            AudioReferenceAdapters.BackendProviderId = "fmod";
            AudioReferenceAdapters.SynchronizeCatalogBindings = FmodCatalogSynchronizer.Synchronize;
            AudioReferenceAdapters.BeginCuePreview = value => new FmodEditorPreview(ResolveBackend(value));
            AudioReferenceAdapters.BeginResourcePreview = value => new FmodEditorPreview(value);
            AudioReferenceAdapters.ResolveBackendReference = ResolveBackend;
            AudioReferenceAdapters.BackendReferenceExists = value => FindEvent(value) != null;
        }

        private sealed class FmodEditorPreview : IAudioEditorPreview
        {
            private FMOD.Studio.EventInstance _instance;
            private bool _disposed;

            public FmodEditorPreview(AudioBackendReference resource)
            {
                var reference = FindEvent(resource);
                if (reference == null) throw new InvalidOperationException("The event identity does not exist in the imported FMOD banks: " + resource);
                EditorUtils.LoadPreviewBanks();
                var system = EditorUtils.System;
                Check(system.getEventByID(reference.Guid, out var description));
                Check(description.createInstance(out _instance));
                try
                {
                    Check(system.getListenerAttributes(0, out var attributes));
                    Check(_instance.set3DAttributes(attributes));
                    Check(_instance.setVolume(1f));
                    Check(_instance.start());
                }
                catch { Dispose(); throw; }
            }

            public bool IsPlaying
            {
                get
                {
                    if (_disposed || !_instance.isValid()) return false;
                    return _instance.getPlaybackState(out var state) == FMOD.RESULT.OK && state != FMOD.Studio.PLAYBACK_STATE.STOPPED;
                }
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (!_instance.isValid()) return;
                _instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                _instance.release();
                _instance.clearHandle();
            }

            private static void Check(FMOD.RESULT result)
            {
                if (result != FMOD.RESULT.OK) throw new InvalidOperationException("FMOD preview: " + result);
            }
        }

        private static IReadOnlyList<string> Choices()
        {
            if (_choices == null || _choiceCount != EventManager.Events.Count || _choiceVersion != EventManager.CacheTime)
            {
                _choiceCount = EventManager.Events.Count;
                _choiceVersion = EventManager.CacheTime;
                _choices = EventManager.Events.Select(e => e.Path.StartsWith("event:/", StringComparison.Ordinal) ? e.Path.Substring(EVENT_PREFIX.Length) : e.Path).OrderBy(p => p).ToArray();
            }
            return _choices;
        }

        public static AudioBackendReference ResolveBackend(string value)
        {
            if (string.IsNullOrEmpty(value)) return default;
            var e = Guid.TryParse(value, out var guid) ? EventManager.EventFromGUID(new FMOD.GUID(guid)) : EventManager.EventFromPath(FullPath(value));
            return e != null ? new AudioBackendReference(e.Guid.ToString(), e.Path.Substring(EVENT_PREFIX.Length)) : default;
        }

        public static EditorEventRef FindEvent(AudioBackendReference value)
        {
            if (!string.IsNullOrEmpty(value.StableId)) return Guid.TryParse(value.StableId, out var guid) ? EventManager.EventFromGUID(new FMOD.GUID(guid)) : null;
            return string.IsNullOrEmpty(value.Path) ? null : EventManager.EventFromPath(FullPath(value.Path));
        }

        private static string FullPath(string value) => value.StartsWith("event:/", StringComparison.Ordinal) ? value : "event:/" + value;
        public string Id => "fmod-event";
        public bool Matches(SerializedProperty p) => p.propertyType == SerializedPropertyType.Generic && p.type == "EventReference" && p.FindPropertyRelative("Guid") != null;

        public AudioReferenceValue Read(SerializedProperty p)
        {
            var guid = p.FindPropertyRelative("Guid");
            var id = new FMOD.GUID { Data1 = guid.FindPropertyRelative("Data1").intValue, Data2 = guid.FindPropertyRelative("Data2").intValue, Data3 = guid.FindPropertyRelative("Data3").intValue, Data4 = guid.FindPropertyRelative("Data4").intValue };
            string path = p.FindPropertyRelative("Path")?.stringValue ?? "";
            var e = EventManager.EventFromGUID(id);
            string display = e != null ? e.Path : path;
            return new AudioReferenceValue { Text = display, Fingerprint = id + "|" + path, Display = string.IsNullOrEmpty(display) ? "Unassigned" : display };
        }

        public bool Validate(AudioReferenceValue value, out string error)
        {
            error = !string.IsNullOrEmpty(value.Text) && EventManager.EventFromPath(FullPath(value.Text)) == null ? "The event does not exist in the imported FMOD banks." : null;
            return error == null;
        }

        public void Write(SerializedProperty p, AudioReferenceValue value)
        {
            var e = string.IsNullOrEmpty(value.Text) ? null : EventManager.EventFromPath(FullPath(value.Text));
            var id = e != null ? e.Guid : default;
            var guid = p.FindPropertyRelative("Guid");
            guid.FindPropertyRelative("Data1").intValue = id.Data1;
            guid.FindPropertyRelative("Data2").intValue = id.Data2;
            guid.FindPropertyRelative("Data3").intValue = id.Data3;
            guid.FindPropertyRelative("Data4").intValue = id.Data4;
            var path = p.FindPropertyRelative("Path");
            if (path != null) path.stringValue = e != null ? e.Path : "";
        }
    }
}
