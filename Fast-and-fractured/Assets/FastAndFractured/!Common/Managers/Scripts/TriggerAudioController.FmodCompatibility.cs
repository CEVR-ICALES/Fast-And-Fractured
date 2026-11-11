using FMODUnity;
using UnityEngine;
using Utilities;
using Utilities.Audio;

public partial class TriggerAudioController
{
    [SerializeField] private EventReference audioEventReference;

    private IAudioVoice PlayLegacyAudio(AudioPlaybackOptions options) =>
        SoundManager.ExistingInstance != null ? SoundManager.ExistingInstance.PlayLegacyAudio(audioEventReference, options) : null;
}
