using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using UnityEngine;
using Utilities.Audio;

namespace FastAndFractured
{
    public static class GameplayAudio
    {
        private const float PERSONAL_FEEDBACK_COOLDOWN_SECONDS = 0.15f;
        private static readonly Dictionary<AudioCueId, float> _personalTimes = new Dictionary<AudioCueId, float>();

        public static bool CanPlay => Application.isPlaying && !(InstanceFinder.IsServerStarted && !InstanceFinder.IsClientStarted)
            && (InstanceFinder.PredictionManager == null || !InstanceFinder.PredictionManager.IsReconciling);
        public static bool IsReady => CanPlay && AudioServices.Current != null && AudioServices.Current.IsReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _personalTimes.Clear();

        public static bool IsLocal(StatsController stats)
        {
            if (stats == null) return false;
            if (!InstanceFinder.IsClientStarted && !InstanceFinder.IsServerStarted) return stats.IsPlayer;
            var owner = stats.GetComponentInParent<NetworkObject>();
            return owner != null && owner.IsOwner;
        }

        public static AudioCueId EngineCueFor(string character)
        {
            if (string.IsNullOrEmpty(character)) return default;
            switch (character)
            {
                case "Josefino":
                case "MariaAntonia": return GameAudioCues.VehicleMovementEventsGolfCartEngine;
                case "Pepe": return GameAudioCues.VehicleMovementEventsTractorEngineSound;
                case "Carme": return GameAudioCues.VehicleMovementEventsQuadBikeEngine;
                default: return default;
            }
        }

        public static AudioCueId BounceCue(Enums.Pooltype type) =>
            type == Enums.Pooltype.PUSH_BULLET_GOLF || type == Enums.Pooltype.PUSH_BULLET_SARTENAZO
                ? GameAudioCues.CommonBounceMetal : GameAudioCues.CommonBounceSoft;

        public static IAudioVoice Play(AudioCueId cueId, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (!CanPlay) return null;
            var options = AudioPlaybackOptions.At(position, volume);
            options.Pitch = pitch;
            return AudioServices.Play(cueId, options);
        }

        public static void PlayPersonal(AudioCueId cueId, StatsController recipient)
        {
            if (!IsLocal(recipient) || !CanPlay) return;
            if (_personalTimes.TryGetValue(cueId, out float last) && Time.unscaledTime - last < PERSONAL_FEEDBACK_COOLDOWN_SECONDS) return;
            if (Play(cueId, recipient.transform.position) != null) _personalTimes[cueId] = Time.unscaledTime;
        }

        public static IAudioVoice StartAttached(AudioCueId cueId, GameObject source, float volume = 1f, AudioParameter[] parameters = null)
        {
            if (!CanPlay || source == null) return null;
            var options = AudioPlaybackOptions.At(source.transform.position, volume);
            options.Follow = source;
            options.LoopPolicy = AudioLoopPolicy.RequireLoop;
            options.Parameters = parameters;
            return AudioServices.Play(cueId, options);
        }

        public static void Stop(ref IAudioVoice voice)
        {
            voice?.Stop();
            voice = null;
        }
    }
}
