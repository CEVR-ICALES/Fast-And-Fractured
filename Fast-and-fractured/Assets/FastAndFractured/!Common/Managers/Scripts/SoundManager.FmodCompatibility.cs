using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using Utilities.Audio;
using Utilities.Audio.Fmod;

namespace Utilities
{
    public partial class SoundManager
    {
        private FmodAudioService Fmod => _service as FmodAudioService;
        private static IAudioService CreateDefaultAudioService() => new FmodAudioService();

        public void PlayOneShot(EventReference eventReference, Vector3 worldPosition) => PlaySound3D(eventReference, worldPosition);

        public EventInstance PlaySound3D(EventReference eventReference, Vector3 position)
        {
            if (FishNet.InstanceFinder.IsServerStarted && !FishNet.InstanceFinder.IsClientStarted) return default;
            var voice = Fmod?.PlayNative(eventReference, AudioPlaybackOptions.At(position));
            return voice != null ? voice.NativeInstance : default;
        }

        internal IAudioVoice PlayLegacyAudio(EventReference reference, AudioPlaybackOptions options)
        {
            if (FishNet.InstanceFinder.IsServerStarted && !FishNet.InstanceFinder.IsClientStarted) return null;
            return Fmod?.PlayNative(reference, options);
        }

        public void StopSound(EventInstance instance) => Fmod?.StopNative(instance);
        public void StopSound(EventReference reference) => Fmod?.StopLatest(reference);
        public void PauseAudio(EventReference reference) => Fmod?.PauseLatest(reference, true);
        public void ResumeAudio(EventReference reference) => Fmod?.PauseLatest(reference, false);
        internal void AddEventInstance(EventInstance instance) => Fmod?.RegisterExternal(instance);
        internal void RemoveEventInstance(EventInstance instance) => Fmod?.UnregisterExternal(instance);

        public void SetVCAVolume(string path, float volume)
        {
            if (path == "vca:/SFX") SetSFXVolume(volume);
            else if (path == "vca:/Music") SetMusicVolume(volume);
            else if (path == "vca:/General") SetGeneralVolume(volume);
            else Fmod?.SetVcaVolume(path, volume);
        }
    }
}
