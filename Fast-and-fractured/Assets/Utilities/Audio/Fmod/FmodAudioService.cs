using System;
using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace Utilities.Audio.Fmod
{
    public sealed class FmodAudioService : IAudioService
    {
        private readonly Dictionary<AudioBackendReference, EventReference> _events = new Dictionary<AudioBackendReference, EventReference>();
        private readonly HashSet<AudioBackendReference> _missing = new HashSet<AudioBackendReference>();
        private readonly List<FmodAudioVoice> _voices = new List<FmodAudioVoice>();
        private readonly List<EventInstance> _external = new List<EventInstance>();
        private readonly Dictionary<FMOD.GUID, FmodAudioVoice> _latest = new Dictionary<FMOD.GUID, FmodAudioVoice>();
        private readonly Dictionary<AudioBackendReference, CueGate> _gates = new Dictionary<AudioBackendReference, CueGate>();
        private bool _disposed;

        public bool IsReady => !_disposed && RuntimeManager.HaveAllBanksLoaded;
        public int ActiveVoiceCount => _voices.Count;

        public static EventReference ResolveReference(AudioBackendReference resource)
        {
            if (!string.IsNullOrEmpty(resource.StableId)) return new EventReference { Guid = FMOD.GUID.Parse(resource.StableId) };
            string path = resource.Path ?? "";
            return RuntimeManager.PathToEventReference(path.StartsWith("event:/", StringComparison.Ordinal) ? path : "event:/" + path);
        }

        public IAudioVoice Play(AudioBackendReference resource, AudioPlaybackOptions options)
        {
            if (resource.IsEmpty || !IsReady || _missing.Contains(resource)) return null;
            try
            {
                if (!_events.TryGetValue(resource, out var reference))
                {
                    reference = ResolveReference(resource);
                    _events.Add(resource, reference);
                }
                return PlayNative(reference, options);
            }
            catch (EventNotFoundException) { return Missing(resource); }
            catch (FormatException) { return Missing(resource); }
        }

        private IAudioVoice Missing(AudioBackendReference resource)
        {
            if (_missing.Add(resource)) Debug.LogWarning("Audio resource not found: " + resource.StableId + " (" + resource.Path + ")");
            return null;
        }

        public FmodAudioVoice PlayNative(EventReference reference, AudioPlaybackOptions options)
        {
            if (reference.IsNull || !IsReady) return null;
            var description = RuntimeManager.GetEventDescription(reference);
            if (options.LoopPolicy == AudioLoopPolicy.RequireLoop)
            {
                description.isOneshot(out bool oneShot);
                if (oneShot)
                {
                    Debug.LogWarning("Audio cue requires an authored loop: " + reference.Guid);
                    return null;
                }
            }
            var instance = RuntimeManager.CreateInstance(reference);
            instance.set3DAttributes(RuntimeUtils.To3DAttributes(options.Position));
            instance.setVolume(Mathf.Max(0f, options.Volume));
            instance.setPitch(options.Pitch > 0f ? options.Pitch : 1f);
            if (options.Follow != null) RuntimeManager.AttachInstanceToGameObject(instance, options.Follow);
            if (options.Parameters != null)
                foreach (var parameter in options.Parameters)
                {
                    if (parameter.Label != null) instance.setParameterByNameWithLabel(parameter.Name, parameter.Label);
                    else instance.setParameterByName(parameter.Name, parameter.Value);
                }
            var voice = new FmodAudioVoice(this, instance, reference.Guid);
            _voices.Add(voice);
            _latest[reference.Guid] = voice;
            instance.start();
            return voice;
        }

        public IDisposable SuspendCue(AudioBackendReference resource)
        {
            if (!IsReady || resource.IsEmpty) return null;
            if (!_gates.TryGetValue(resource, out var gate))
            {
                try { gate = new CueGate(RuntimeManager.GetEventDescription(ResolveReference(resource))); }
                catch (EventNotFoundException) { Missing(resource); return null; }
                catch (FormatException) { Missing(resource); return null; }
                _gates.Add(resource, gate);
            }
            gate.Users++;
            gate.Capture();
            return new CueGateLease(this, resource);
        }

        private void ReleaseGate(AudioBackendReference resource)
        {
            if (!_gates.TryGetValue(resource, out var gate) || --gate.Users > 0) return;
            gate.Restore();
            _gates.Remove(resource);
        }

        private sealed class CueGate
        {
            private readonly EventDescription _description;
            private readonly HashSet<EventInstance> _paused = new HashSet<EventInstance>();
            public int Users;
            public CueGate(EventDescription description) { _description = description; }
            public void Capture()
            {
                if (!_description.isValid() || _description.getInstanceList(out EventInstance[] instances) != FMOD.RESULT.OK) return;
                foreach (var instance in instances)
                {
                    if (!instance.isValid()) continue;
                    instance.getPaused(out bool paused);
                    if (!paused) { _paused.Add(instance); instance.setPaused(true); }
                }
            }
            public void Restore()
            {
                foreach (var instance in _paused) if (instance.isValid()) instance.setPaused(false);
                _paused.Clear();
            }
        }

        private sealed class CueGateLease : IDisposable
        {
            private FmodAudioService _owner;
            private readonly AudioBackendReference _cueId;
            public CueGateLease(FmodAudioService owner, AudioBackendReference cueId) { _owner = owner; _cueId = cueId; }
            public void Dispose()
            {
                if (_owner == null) return;
                _owner.ReleaseGate(_cueId);
                _owner = null;
            }
        }

        public void RegisterExternal(EventInstance instance)
        {
            if (!_disposed && instance.isValid() && !_external.Contains(instance)) _external.Add(instance);
        }

        public void UnregisterExternal(EventInstance instance) => _external.Remove(instance);

        public void StopNative(EventInstance instance)
        {
            for (int i = _voices.Count - 1; i >= 0; i--)
                if (_voices[i].NativeInstance.Equals(instance)) { _voices[i].Stop(); return; }
        }

        public void StopLatest(EventReference reference)
        {
            if (_latest.TryGetValue(reference.Guid, out var voice)) voice.Stop();
        }

        public void PauseLatest(EventReference reference, bool paused)
        {
            if (_latest.TryGetValue(reference.Guid, out var voice)) voice.SetPaused(paused);
        }

        internal void Release(FmodAudioVoice voice, bool stop)
        {
            if (!_voices.Remove(voice)) return;
            if (_latest.TryGetValue(voice.EventId, out var latest) && ReferenceEquals(latest, voice)) _latest.Remove(voice.EventId);
            voice.Release(stop);
        }

        public void Tick()
        {
            if (_disposed) return;
            foreach (var gate in _gates.Values) gate.Capture();
            for (int i = _voices.Count - 1; i >= 0; i--)
                if (!_voices[i].IsAlive) Release(_voices[i], false);
            for (int i = _external.Count - 1; i >= 0; i--)
            {
                var instance = _external[i];
                if (!instance.isValid()) { _external.RemoveAt(i); continue; }
                instance.getPlaybackState(out PLAYBACK_STATE state);
                if (state == PLAYBACK_STATE.STOPPED) _external.RemoveAt(i);
            }
        }

        public void SetGroupVolume(AudioGroup group, float volume)
        {
            SetVcaVolume(group == AudioGroup.Sfx ? "vca:/SFX" : group == AudioGroup.Music ? "vca:/Music" : "vca:/General", volume);
        }

        public void SetVcaVolume(string path, float volume)
        {
            if (!_disposed) RuntimeManager.GetVCA(path).setVolume(Mathf.Clamp01(volume));
        }

        public void SetPaused(bool paused)
        {
            foreach (var voice in _voices) voice.SetPaused(paused);
            foreach (var instance in _external)
                if (instance.isValid()) instance.setPaused(paused);
        }

        public void StopAll()
        {
            for (int i = _voices.Count - 1; i >= 0; i--) _voices[i].Stop();
            _latest.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            StopAll();
            foreach (var gate in _gates.Values) gate.Restore();
            _gates.Clear();
            _external.Clear();
            _events.Clear();
            _missing.Clear();
            _disposed = true;
        }
    }

    public sealed class FmodAudioVoice : IAudioVoice
    {
        private const float MINIMUM_PITCH = 0.01f;
        private readonly FmodAudioService _owner;
        private bool _released;
        public EventInstance NativeInstance { get; }
        internal FMOD.GUID EventId { get; }

        internal FmodAudioVoice(FmodAudioService owner, EventInstance instance, FMOD.GUID eventId)
        {
            _owner = owner;
            NativeInstance = instance;
            EventId = eventId;
        }

        public bool IsAlive
        {
            get
            {
                if (_released || !NativeInstance.isValid()) return false;
                return NativeInstance.getPlaybackState(out PLAYBACK_STATE state) == FMOD.RESULT.OK && state != PLAYBACK_STATE.STOPPED;
            }
        }

        public void SetVolume(float volume) { if (IsAlive) NativeInstance.setVolume(Mathf.Max(0f, volume)); }
        public void SetPitch(float pitch) { if (IsAlive) NativeInstance.setPitch(Mathf.Max(MINIMUM_PITCH, pitch)); }
        public void SetPaused(bool paused) { if (IsAlive) NativeInstance.setPaused(paused); }
        public bool TrySetParameter(string name, float value) => IsAlive && NativeInstance.setParameterByName(name, value) == FMOD.RESULT.OK;
        public bool TrySetParameterLabel(string name, string label) => IsAlive && NativeInstance.setParameterByNameWithLabel(name, label) == FMOD.RESULT.OK;
        public void Stop() => _owner.Release(this, true);

        internal void Release(bool stop)
        {
            if (_released) return;
            _released = true;
            if (!NativeInstance.isValid()) return;
            if (stop) NativeInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            NativeInstance.release();
        }
    }
}
