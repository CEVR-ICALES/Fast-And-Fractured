using UnityEngine;
using UnityEngine.UI;
using Utilities.Audio;
using Utilities.Managers.PauseSystem;

namespace Utilities
{
    public partial class SoundManager : AbstractSingleton<SoundManager>, IPausable
    {
        [SerializeField] private Slider generalVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Toggle muteToggle;

        private const string GENERAL_VOLUME_STRING = "GeneralVolume";
        private const string MUSIC_VOLUME_STRING = "MusicVolume";
        private const string SFX_VOLUME_STRING = "SFXVolume";
        private const string MUTE_ALL_STRING = "MuteAll";
        private const float DEFAULT_VOLUME_SLIDER_VALUE = 0.5f;

        private IAudioService _service;
        private bool _ownsService;

        protected override void Awake()
        {
            base.Awake();
            if (ExistingInstance != this || !enabled) return;
            _ownsService = AudioServices.Current == null;
            _service = AudioServices.Current ?? CreateDefaultAudioService();
            AudioServices.Current = _service;
            if (AudioServices.Catalog == null) AudioServices.Catalog = Resources.Load<AudioCueCatalog>("GameAudioCatalog");
        }

        private void Start()
        {
            if (generalVolumeSlider != null)
            {
                generalVolumeSlider.value = PlayerPrefs.GetFloat(GENERAL_VOLUME_STRING, DEFAULT_VOLUME_SLIDER_VALUE);
                UpdateGeneralVolume();
            }
            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.value = PlayerPrefs.GetFloat(MUSIC_VOLUME_STRING, DEFAULT_VOLUME_SLIDER_VALUE);
                UpdateMusicVolume();
            }
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.value = PlayerPrefs.GetFloat(SFX_VOLUME_STRING, DEFAULT_VOLUME_SLIDER_VALUE);
                UpdateSFXVolume();
            }
            if (muteToggle != null)
            {
                muteToggle.isOn = PlayerPrefs.GetInt(MUTE_ALL_STRING, 0) == 1;
                ToggleMuteAllSounds();
            }
        }

        private void OnEnable() => PauseManager.TryRegisterPausable(this);
        private void OnDisable() => PauseManager.TryUnregisterPausable(this);
        private void Update() => _service?.Tick();

        protected override void Deconstruct()
        {
            _service?.StopAll();
            if (_ownsService)
            {
                _service?.Dispose();
                if (ReferenceEquals(AudioServices.Current, _service)) AudioServices.Current = null;
            }
            _service = null;
            base.Deconstruct();
        }

        public void StopAllSounds() => _service?.StopAll();
        public void PauseAllSounds() => _service?.SetPaused(true);
        public void ResumeAllSounds() => _service?.SetPaused(false);
        public void OnPause() => PauseAllSounds();
        public void OnResume() => ResumeAllSounds();
        public void SetSFXVolume(float value) => _service?.SetGroupVolume(AudioGroup.Sfx, value);
        public void SetMusicVolume(float value) => _service?.SetGroupVolume(AudioGroup.Music, value);
        public void SetGeneralVolume(float value) => _service?.SetGroupVolume(AudioGroup.General, value);

        public void ToggleMuteAllSounds()
        {
            if (muteToggle == null) return;
            PlayerPrefs.SetInt(MUTE_ALL_STRING, muteToggle.isOn ? 1 : 0);
            if (muteToggle.isOn)
            {
                SetGeneralVolume(0);
                SetMusicVolume(0);
                SetSFXVolume(0);
                return;
            }
            UpdateGeneralVolume();
            UpdateMusicVolume();
            UpdateSFXVolume();
            PlayerPrefs.Save();
        }

        public void UpdateSFXVolume()
        {
            if (sfxVolumeSlider == null || (muteToggle != null && muteToggle.isOn)) return;
            SetSFXVolume(sfxVolumeSlider.value);
            PlayerPrefs.SetFloat(SFX_VOLUME_STRING, sfxVolumeSlider.value);
            PlayerPrefs.Save();
        }

        public void UpdateMusicVolume()
        {
            if (musicVolumeSlider == null || (muteToggle != null && muteToggle.isOn)) return;
            SetMusicVolume(musicVolumeSlider.value);
            PlayerPrefs.SetFloat(MUSIC_VOLUME_STRING, musicVolumeSlider.value);
            PlayerPrefs.Save();
        }

        public void UpdateGeneralVolume()
        {
            if (generalVolumeSlider == null || (muteToggle != null && muteToggle.isOn)) return;
            SetGeneralVolume(generalVolumeSlider.value);
            PlayerPrefs.SetFloat(GENERAL_VOLUME_STRING, generalVolumeSlider.value);
            PlayerPrefs.Save();
        }
    }
}
