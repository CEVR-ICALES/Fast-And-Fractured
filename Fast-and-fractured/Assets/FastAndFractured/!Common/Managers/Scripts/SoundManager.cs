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
        private Dictionary<EventReference, EventInstance> _activeEvents = new Dictionary<EventReference, EventInstance>();
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
            if (muteToggle != null)
                ToggleMuteAllSounds();
        }

        private void OnEnable()
        {
            PauseManager.Instance.RegisterPausable(this);
        }

        private void OnDisable()
        {
            PauseManager.Instance.UnregisterPausable(this);
        }

        #region Play Sounds Methods
        /// <summary>
        /// Plays a one-shot sound at a specific world position
        /// </summary>
        /// <param name="eventReference">Path of the FMOD event</param>
        /// <param name="worldPosition">The world position to play the sound at</param>
        public void PlayOneShot(EventReference eventReference, Vector3 worldPosition)
        {
            RuntimeManager.PlayOneShot(eventReference, worldPosition);
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
            VCA vca= RuntimeManager.GetVCA(vcaPath);
            vca.setVolume(value);
        }

        public void ToggleMuteAllSounds()
        {
            if (muteToggle.isOn)
            {
                _previousGeneralVolume = generalVolumeSlider != null ? generalVolumeSlider.value : _generalVolume;
                _previousMusicVolume = musicVolumeSlider != null ? musicVolumeSlider.value : _musicVolume;
                _previousSFXVolume = sfxVolumeSlider != null ? sfxVolumeSlider.value : _sfxVolume;

                generalVolumeSlider.value = 0;
                musicVolumeSlider.value = 0;
                sfxVolumeSlider.value = 0;

                UpdateGeneralVolume();
                UpdateMusicVolume();
                UpdateSFXVolume();
            }
            else
            {
                generalVolumeSlider.value = 0.5f;
                musicVolumeSlider.value = 0.5f;
                sfxVolumeSlider.value = 0.5f;

                UpdateGeneralVolume();
                UpdateMusicVolume();
                UpdateSFXVolume();

                if (generalVolumeSlider != null) generalVolumeSlider.value = _previousGeneralVolume;
                if (musicVolumeSlider != null) musicVolumeSlider.value = _previousMusicVolume;
                if (sfxVolumeSlider != null) sfxVolumeSlider.value = _previousSFXVolume;
            }
        }

        #region Slider Volume Methods
        public void UpdateSFXVolume()
        {
            if (_sfxVolume != 0)
                _previousSFXVolume = _sfxVolume;

            _sfxVolume = sfxVolumeSlider.value;
            SetSFXVolume(sfxVolumeSlider.value);
        }

        public void UpdateMusicVolume()
        {
            if (_musicVolume != 0)
                _previousMusicVolume = _musicVolume;

            _musicVolume = musicVolumeSlider.value;
            SetMusicVolume(musicVolumeSlider.value);
        }

        public void UpdateGeneralVolume()
        {
            if (_generalVolume != 0)
                _previousGeneralVolume = _generalVolume;

            _generalVolume = generalVolumeSlider.value;
            SetGeneralVolume(generalVolumeSlider.value);
        }
        #endregion
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