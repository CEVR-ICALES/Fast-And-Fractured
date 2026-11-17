using UnityEngine;
using Utilities.Audio;

namespace FastAndFractured
{
    [DisallowMultipleComponent]
    public sealed class ChickenAudioController : MonoBehaviour
    {
        private const float MINIMUM_FRAME_SECONDS = 0.001f;
        private ChickenAudioSettings _settings;
        private IAudioVoice _voice;
        private IAudioVoice _steps;
        private Vector3 _lastPosition;
        private float _nextCry;
        private float _enabledAt;

        private void OnEnable()
        {
            _settings = AudioPresentationProfile.Load().Chicken;
            _lastPosition = transform.position;
            _enabledAt = Time.time;
        }

        private void LateUpdate()
        {
            if (!GameplayAudio.CanPlay) return;
            if (_voice == null || !_voice.IsAlive) _voice = GameplayAudio.StartAttached(GameAudioCues.SpecialAbilityEventsSmallChickenSoundSFX, gameObject, _settings.VoiceVolume);
            float speed = Vector3.ProjectOnPlane(transform.position - _lastPosition, Vector3.up).magnitude / Mathf.Max(Time.deltaTime, MINIMUM_FRAME_SECONDS);
            _lastPosition = transform.position;
            bool moving = Time.time - _enabledAt > _settings.StepsDelaySeconds && speed > _settings.MinimumWalkingSpeed;
            if (moving && (_steps == null || !_steps.IsAlive)) _steps = GameplayAudio.StartAttached(GameAudioCues.SpecialAbilityEventsChickenStepsSFX, gameObject, _settings.StepsVolume);
            else if (!moving && _steps != null) GameplayAudio.Stop(ref _steps);
        }

        public void Cry()
        {
            if (Time.time < _nextCry) return;
            GameplayAudio.Play(GameAudioCues.CommonChickenCry, transform.position);
            _nextCry = Time.time + (_settings ?? AudioPresentationProfile.Load().Chicken).CryCooldownSeconds;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!FishNet.InstanceFinder.IsClientStarted && !FishNet.InstanceFinder.IsServerStarted && collision.collider.GetComponentInParent<StatsController>() != null)
                Cry();
        }

        private void OnDisable()
        {
            GameplayAudio.Stop(ref _voice);
            GameplayAudio.Stop(ref _steps);
        }
    }
}
