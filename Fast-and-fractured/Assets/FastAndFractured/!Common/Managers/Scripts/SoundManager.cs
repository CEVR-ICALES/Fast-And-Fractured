using FMODUnity;
using UnityEngine;
using FMOD.Studio;
using UnityEngine.UI;
using System.Collections.Generic;
using Utilities.Managers.PauseSystem;

namespace Utilities
{
    public class SoundManager : AbstractSingleton<SoundManager>, IPausable
    {
        #region Variables
        #region Volume Variables
        private float _generalVolume = 1f;
        private float _musicVolume = 1f;
        private float _sfxVolume = 1f;

        private float _previousGeneralVolume;
        private float _previousMusicVolume;
        private float _previousSFXVolume;
        #endregion

        #region Slider and Toggle Variables
        [SerializeField] private Slider generalVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;

        [SerializeField] private Toggle muteToggle;
        #endregion

        #region Dictionary Variables
        private List<EventReference> fmodEvents = new List<EventReference>();
        private Dictionary<EventReference, EventInstance> _activeEvents = new Dictionary<EventReference, EventInstance>();
        #endregion

        #region Constants
        private const string GENERAL_VOLUME_STRING = "GeneralVolume";
        private const string MUSIC_VOLUME_STRING = "MusicVolume";
        private const string SFX_VOLUME_STRING = "SFXVolume";

        private const string AMBIENCE_ZONE_PARAM_NAME = "AmbienceZone";
         
        private const string MUTE_ALL_STRING = "MuteAll";

        private const float DEFAULT_VOLUME_SLIDER_VALUE = 0.5f;
        #endregion

        #region Ambience Sounds Variables
        private EventInstance _ambienceInstance;
        [SerializeField] private EventReference ambienceEventReference;
        #endregion

        EventReference musicGameLoopReference;
        #endregion

        protected override void Awake()
        {
            base.Awake();
            musicGameLoopReference.Path = "event:/MusicEvents/GameLoopMusicEvent";
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

            InitializeAmbience();
        }

        private void OnEnable()
        {
            PauseManager.Instance?.RegisterPausable(this);
        }

        private void OnDisable()
        {
            PauseManager.Instance?.UnregisterPausable(this);
        }

        #region Play Sounds Methods
        /// <summary>
        /// Plays a one-shot sound at a specific world position
        /// </summary>
        /// <param name="eventReference">Path of the FMOD event</param>
        /// <param name="worldPosition">The world position to play the sound at</param>
        public void PlayOneShot(EventReference eventReference, Vector3 worldPosition)
        {
            PlaySound3D(eventReference, worldPosition);
        }

        /// <summary>
        /// Plays a 3D sound event at a specific world position
        /// </summary>
        /// <param name="eventReference">Path of the FMOD event</param>
        /// <param name="position">Position in the world space</param>
        public EventInstance PlaySound3D(EventReference eventReference, Vector3 position)
        {
            EventInstance soundInstance = RuntimeManager.CreateInstance(eventReference);
            soundInstance.set3DAttributes(RuntimeUtils.To3DAttributes(position));
            soundInstance.start();
            _activeEvents[eventReference] = soundInstance;

            return soundInstance;
        }
        #endregion

        #region Stop Sounds Methods
        /// <summary>
        /// Stops a currently playing sound event
        /// </summary>
        /// <param name="eventReference">Path of the FMOD event</param>
        public void StopSound(EventReference eventReference)
        {
            if (_activeEvents.TryGetValue(eventReference, out EventInstance instance))
            {
                instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                instance.release();
                _activeEvents.Remove(eventReference);
            }
        }

        /// <summary>
        /// Stops all currently playing sounds
        /// </summary>
        public void StopAllSounds()
        {
            foreach (var instance in _activeEvents.Values)
            {
                instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                instance.release();
            }
            _activeEvents.Clear();
        }
        #endregion

        #region Pause and Resume Methods
        /// <summary>
        /// Pauses a playing sound event
        /// </summary>
        /// <param name="eventReference">Path of the FMOD event</param>
        public void PauseAudio(EventReference eventReference)
        {
            if (_activeEvents.TryGetValue(eventReference, out EventInstance instance))
                instance.setPaused(true);
        }

        public void PauseAllSounds()
        {
            foreach (EventInstance instance in _activeEvents.Values)
            {
                instance.setPaused(true);
            }
            ResumeAudio(musicGameLoopReference);
        }

        /// <summary>
        /// Resumes a paused sound event
        /// </summary>
        /// <param name="eventReference">Path of the FMOD event</param>
        public void ResumeAudio(EventReference eventReference)
        {
            if (_activeEvents.TryGetValue(eventReference, out EventInstance instance))
                instance.setPaused(false);
        }

        public void ResumeAllSounds()
        {
            foreach (EventInstance instance in _activeEvents.Values)
            {
                instance.setPaused(false);
            }
        }
        #endregion

        #region Volume Methods
        public void SetSFXVolume(float value) => SetVCAVolume("vca:/SFX", value);
        public void SetMusicVolume(float value) => SetVCAVolume("vca:/Music", value);
        public void SetGeneralVolume(float value) => SetVCAVolume("vca:/General", value);

        public void SetVCAVolume(string vcaPath, float value)
        {
            VCA vca = RuntimeManager.GetVCA(vcaPath);
            vca.setVolume(value);
        }

        public void ToggleMuteAllSounds()
        {
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

        #region Slider Volume Methods
        public void UpdateSFXVolume()
        {
            if (muteToggle.isOn) return;
            _sfxVolume = sfxVolumeSlider.value;
            SetSFXVolume(sfxVolumeSlider.value);
            PlayerPrefs.SetFloat(SFX_VOLUME_STRING, _sfxVolume);
            PlayerPrefs.Save();
        }

        public void UpdateMusicVolume()
        {
            if (muteToggle.isOn) return;
            _musicVolume = musicVolumeSlider.value;
            SetMusicVolume(musicVolumeSlider.value);
            PlayerPrefs.SetFloat(MUSIC_VOLUME_STRING, _musicVolume);
            PlayerPrefs.Save();
        }

        public void UpdateGeneralVolume()
        {
            if (muteToggle.isOn) return;
            _generalVolume = generalVolumeSlider.value;
            SetGeneralVolume(generalVolumeSlider.value);
            PlayerPrefs.SetFloat(GENERAL_VOLUME_STRING, _generalVolume);
            PlayerPrefs.Save();
        }
        #endregion
        #endregion

        #region Ambience Methods
        private void InitializeAmbience()
        {
            if (_ambienceInstance.isValid()) return;

            _ambienceInstance = RuntimeManager.CreateInstance(ambienceEventReference);
            _ambienceInstance.set3DAttributes(RuntimeUtils.To3DAttributes(Vector3.zero));
            _ambienceInstance.start();
            _ambienceInstance.release();
        }

        public void SetAmbienceZone(float zoneValue)
        {
            _ambienceInstance.setParameterByName(AMBIENCE_ZONE_PARAM_NAME, zoneValue);
        }
        #endregion

        public void OnPause()
        {
            PauseAllSounds();
        }

        public void OnResume()
        {
            ResumeAllSounds();
        }
    }
}