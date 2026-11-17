using System.Collections.Generic;
using FastAndFractured;
using UnityEngine;
using Utilities.Audio;

public partial class TriggerAudioController : MonoBehaviour
{
    [SerializeField] private bool isAmbienceZone;
    private IAudioVoice _voice;
    private AudioCueId _cueId;
    private float _volume = 1f;
    private StatsController _recipient;
    private bool _played;
    private readonly HashSet<Collider> _listeners = new HashSet<Collider>();

    public void ConfigureCue(AudioCueId cue, bool loop, float volume)
    {
        _cueId = cue;
        isAmbienceZone = loop;
        _volume = volume;
    }

    private void OnTriggerEnter(Collider other)
    {
        var stats = other.GetComponentInParent<StatsController>();
        if (!GameplayAudio.CanPlay || !GameplayAudio.IsLocal(stats) || !_listeners.Add(other)) return;
        _recipient = stats;
        TryPlay();
    }

    private void TryPlay()
    {
        if (_played || !GameplayAudio.CanPlay) return;
        var options = AudioPlaybackOptions.At(transform.position, _volume);
        options.LoopPolicy = isAmbienceZone ? AudioLoopPolicy.RequireLoop : AudioLoopPolicy.Authored;
        _voice = !_cueId.IsEmpty ? AudioServices.Play(_cueId, options) : PlayLegacyAudio(options);
        _played = _voice != null;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!_listeners.Remove(other) || _listeners.Count != 0) return;
        _played = false;
        if (isAmbienceZone) GameplayAudio.Stop(ref _voice);
    }

    private static bool Inactive(Collider collider) => collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy;

    private void LateUpdate()
    {
        if (_listeners.Count == 0) return;
        _listeners.RemoveWhere(Inactive);
        if (_listeners.Count > 0 && _recipient != null && _recipient.Endurance > 0)
        {
            TryPlay();
            return;
        }
        _listeners.Clear();
        _played = false;
        if (isAmbienceZone) GameplayAudio.Stop(ref _voice);
    }

    private void OnDisable()
    {
        _recipient = null;
        _played = false;
        _listeners.Clear();
        GameplayAudio.Stop(ref _voice);
    }
}
