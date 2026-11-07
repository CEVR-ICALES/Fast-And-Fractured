using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Utilities.Audio.Editor
{
    public interface IAudioEditorPreview : IDisposable
    {
        bool IsPlaying { get; }
    }

    [InitializeOnLoad]
    public sealed class AudioPreviewSession : IDisposable
    {
        private static readonly HashSet<AudioPreviewSession> _sessions = new HashSet<AudioPreviewSession>();
        private static AudioPreviewSession _active;
        private IAudioEditorPreview _playback;
        public static Func<AudioClip, IAudioEditorPreview> BeginClipPreview = clip => new UnityClipPreview(clip);
        public event Action Changed;
        public string Label { get; private set; }
        public string Error { get; private set; }
        public bool IsPlaying => _playback != null && _playback.IsPlaying;
        public static bool AnyPlaying => _active != null && _active.IsPlaying;

        static AudioPreviewSession()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += StopAll;
            EditorApplication.quitting += StopAll;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public AudioPreviewSession() => _sessions.Add(this);

        private static void OnPlayModeChanged(PlayModeStateChange state) => StopAll();

        private static void Update()
        {
            foreach (var session in _sessions.ToArray())
            {
                if (session._playback == null) continue;
                try { if (!session._playback.IsPlaying) session.Stop(); }
                catch (Exception e) { session.Stop(); session.Error = e.Message; session.Changed?.Invoke(); }
            }
        }

        public static void StopAll()
        {
            foreach (var session in _sessions.ToArray()) session.Stop();
        }

        public bool PlayCue(string cueId, AudioCueCatalog catalog, bool backendBinding = false)
        {
            if (!backendBinding && catalog != null) return PlayResource(catalog.ResolveBinding(new AudioCueId(cueId)));
            if (string.IsNullOrEmpty(cueId)) return Fail("No audio is assigned to this reference.");
            var reference = AudioReferenceAdapters.ResolveBackendReference != null ? AudioReferenceAdapters.ResolveBackendReference(cueId) : new AudioBackendReference(null, cueId);
            return PlayResource(reference);
        }

        public bool PlayResource(AudioBackendReference reference)
        {
            if (reference.IsEmpty) return Fail("No audio is assigned to this reference.");
            string label = string.IsNullOrEmpty(reference.Path) ? reference.StableId : reference.Path;
            return Start(() => AudioReferenceAdapters.BeginResourcePreview != null ? AudioReferenceAdapters.BeginResourcePreview(reference)
                : string.IsNullOrEmpty(reference.StableId) && AudioReferenceAdapters.BeginCuePreview != null ? AudioReferenceAdapters.BeginCuePreview(reference.Path)
                : throw new NotSupportedException("No editor preview provider is registered for this audio backend."), label);
        }

        public bool PlayClip(AudioClip clip)
        {
            if (clip == null) return Fail("No AudioClip is assigned.");
            return Start(() => BeginClipPreview(clip), clip.name);
        }

        private bool Start(Func<IAudioEditorPreview> start, string label)
        {
            Stop();
            _active?.Stop();
            Error = null;
            try
            {
                _playback = start();
                if (_playback == null) throw new InvalidOperationException("The audio preview could not be started.");
                _active = this;
                Label = label;
                Changed?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                Stop();
                return Fail((e.InnerException ?? e).Message);
            }
        }

        private bool Fail(string message)
        {
            Stop();
            Error = message;
            Changed?.Invoke();
            return false;
        }

        public void Stop()
        {
            var playback = _playback;
            _playback = null;
            Label = null;
            if (_active == this) _active = null;
            playback?.Dispose();
            Changed?.Invoke();
        }

        public void Dispose()
        {
            Stop();
            _sessions.Remove(this);
            Changed = null;
        }

        public static AudioClip ResolveClip(AudioReferenceValue value)
        {
            if (!string.IsNullOrEmpty(value.Fingerprint))
            {
                var parts = value.Fingerprint.Split(':');
                if (parts.Length == 2)
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(parts[0])))
                    {
                        if (!(asset is AudioClip clip)) continue;
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string guid, out long id);
                        if (id.ToString() == parts[1]) return clip;
                    }
            }
            return value.Asset as AudioClip;
        }

        private sealed class UnityClipPreview : IAudioEditorPreview
        {
            private const BindingFlags METHOD_FLAGS = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            private static readonly Type _audioUtil = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AudioUtil");
            private static readonly MethodInfo _play = _audioUtil?.GetMethod("PlayPreviewClip", METHOD_FLAGS, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            private static readonly MethodInfo _playing = _audioUtil?.GetMethod("IsPreviewClipPlaying", METHOD_FLAGS, null, Type.EmptyTypes, null);
            private static readonly MethodInfo _stopPreview = _audioUtil?.GetMethod("StopAllPreviewClips", METHOD_FLAGS, null, Type.EmptyTypes, null);
            private bool _disposed;

            public UnityClipPreview(AudioClip clip)
            {
                if (_play == null || _playing == null || _stopPreview == null) throw new NotSupportedException("AudioClip preview is not supported by this Unity Editor version.");
                _play.Invoke(null, new object[] { clip, 0, false });
            }

            public bool IsPlaying => !_disposed && (bool)_playing.Invoke(null, null);
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _stopPreview.Invoke(null, null);
            }
        }
    }
}
