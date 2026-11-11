namespace Utilities.Audio
{
    public static class AudioServices
    {
        // The registering host owns disposal; temporary test overrides must not dispose the previous service.
        public static IAudioService Current { get; set; }
        public static AudioCueCatalog Catalog { get; set; }

        public static AudioBackendReference Resolve(AudioCueId cueId) => Catalog != null ? Catalog.ResolveBinding(cueId) : default;

        public static IAudioVoice Play(AudioCueId cueId, AudioPlaybackOptions options)
        {
            var reference = Resolve(cueId);
            return !reference.IsEmpty && Current != null && Current.IsReady ? Current.Play(reference, options) : null;
        }

        public static System.IDisposable SuspendCue(AudioCueId cueId)
        {
            var reference = Resolve(cueId);
            return !reference.IsEmpty && Current != null && Current.IsReady ? Current.SuspendCue(reference) : null;
        }
    }
}
