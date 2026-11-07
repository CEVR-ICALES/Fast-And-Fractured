using System;
using UnityEngine;

namespace Utilities.Audio
{
    public enum AudioGroup { General, Music, Sfx }
    public enum AudioLoopPolicy { Authored, RequireLoop }

    public interface IAudioService : IDisposable
    {
        bool IsReady { get; }
        IAudioVoice Play(AudioBackendReference reference, AudioPlaybackOptions options);
        void SetGroupVolume(AudioGroup group, float volume);
        IDisposable SuspendCue(AudioBackendReference reference);
        void SetPaused(bool paused);
        void StopAll();
        void Tick();
    }

    public interface IAudioVoice
    {
        bool IsAlive { get; }
        void SetVolume(float volume);
        void SetPitch(float pitch);
        void SetPaused(bool paused);
        bool TrySetParameter(string name, float value);
        bool TrySetParameterLabel(string name, string label);
        void Stop();
    }

    public struct AudioParameter
    {
        public string Name;
        public float Value;
        public string Label;
    }

    public struct AudioPlaybackOptions
    {
        public Vector3 Position;
        public GameObject Follow;
        public float Volume;
        public float Pitch;
        public AudioLoopPolicy LoopPolicy;
        public AudioParameter[] Parameters;

        public static AudioPlaybackOptions At(Vector3 position, float volume = 1f) => new AudioPlaybackOptions
        {
            Position = position,
            Volume = volume,
            Pitch = 1f
        };
    }
}
