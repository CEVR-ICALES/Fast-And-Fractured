using FMODUnity;
using UnityEngine;
using FMOD.Studio;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Utilities
{
    public class SoundManager : AbstractSingleton<SoundManager>
    {
        private float _masterVolume = 1f;

        private float _sfxVolume = 1f;

        private float _musicVolume = 1f;

        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider generalVolumeSlider;

        private Dictionary<EventReference, Queue<EventInstance>> _eventPool = new Dictionary<EventReference, Queue<EventInstance>>();
        private Dictionary<EventReference, EventInstance> _activeEvents = new Dictionary<EventReference, EventInstance>();

        protected override void Awake()
        {
            base.Awake();
        }

        #region Event Pooling Methods
        /// <summary>
        /// Retrieves an EventInstance from the pool if available; otherwise, create a new one
        /// </summary>
        /// <param name="eventReference">FMOD event reference</param>
        /// <returns>EventInstance to be used</returns>
        private EventInstance GetPooledEventInstance(EventReference eventReference)
        {
            if (_eventPool.TryGetValue(eventReference, out Queue<EventInstance> eventQueue) && eventQueue.Count > 0)
                return eventQueue.Dequeue();

            return RuntimeManager.CreateInstance(eventReference);
        }

        /// <summary>
        /// Stops an EventInstance and returns it to the pool for future reuse
        /// </summary>
        /// <param name="eventReference">FMOD event reference</param>
        /// <param name="instance">EventInstance to be returned to the pool</param>
        private void ReturnEventInstanceToPool(EventReference eventReference, EventInstance instance)
        {
            if (!_eventPool.ContainsKey(eventReference))
                _eventPool[eventReference] = new Queue<EventInstance>();

            instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            instance.setParameterByName("Volume", 0f);
            _eventPool[eventReference].Enqueue(instance);
        }
        #endregion

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
        /// Plays a looping sound event and keeps track of it
        /// </summary>
        /// <param name="eventReference">Path of the FMOD event</param>
        /// <param name="soundInstance">Created FMOD EventInstance reference</param>
        public void PlaySound(EventReference eventReference, out EventInstance soundInstance)
        {
            soundInstance = RuntimeManager.CreateInstance(eventReference);
            _activeEvents[eventReference] = soundInstance;
            soundInstance.start();
        }

        /// <summary>
        /// Plays a 3D sound event at a specific world position
        /// </summary>
        /// <param name="eventReference">Path of the FMOD event</param>
        /// <param name="position">Position in the world space</param>
        public void PlaySound3D(EventReference eventReference, Vector3 position)
        {
            EventInstance soundInstance = RuntimeManager.CreateInstance(eventReference);
            _activeEvents[eventReference] = soundInstance;
            soundInstance.set3DAttributes(RuntimeUtils.To3DAttributes(position));
            soundInstance.start();
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
                ReturnEventInstanceToPool(eventReference, instance);
                _activeEvents.Remove(eventReference);
            }
        }

        /// <summary>
        /// Stops all currently playing sounds
        /// </summary>
        public void StopAllSounds()
        {
            foreach (var pair in _activeEvents)
            {
                ReturnEventInstanceToPool(pair.Key, pair.Value);
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

        /// <summary>
        /// Resumes a paused sound event
        /// </summary>
        /// <param name="eventReference">Path of the FMOD event</param>
        public void ResumeAudio(EventReference eventReference)
        {
            if (_activeEvents.TryGetValue(eventReference, out EventInstance instance))
                instance.setPaused(false);
        }
        #endregion

        #region Volume Methods
        /// <summary>
        /// Sets the volume of the specified FMOD parameter
        /// </summary>
        /// <param name="parameterName">Name of the FMOD parameter</param>
        /// <param name="value">New volume value (0.0 - 1.0) </param>
        public void SetVolume(string parameterName, float value)
        {
            RuntimeManager.StudioSystem.setParameterByName(parameterName, value);
        }

        public void SetSFXVolume(float value)
        {
            SetVCAVolume("vca:/SFX", value);
        }

        public void SetMusicVolume(float value)
        {
            SetVCAVolume("vca:/Music", value);
        }

        public void SetGeneralVolume(float value)
        {
            SetVCAVolume("vca:/General", value);
        }

        public void SetVCAVolume(string vcaPath, float value)
        {
            VCA sfxVCA = RuntimeManager.GetVCA(vcaPath);
            sfxVCA.setVolume(value);
        }

        public void MuteAllSounds()
        {
            SetGeneralVolume(0);
            SetMusicVolume(0);
            SetSFXVolume(0);

            //If the sliders are assigned, set their values to 0
            if (sfxVolumeSlider != null && musicVolumeSlider != null && generalVolumeSlider != null)
            {
                generalVolumeSlider.value = 0;
                musicVolumeSlider.value = 0;
                sfxVolumeSlider.value = 0;
            }
        }

        #region Slider Volume Methods
        public void UpdateSFXVolume()
        {
            _sfxVolume = sfxVolumeSlider.value;
            SetSFXVolume(sfxVolumeSlider.value);
        }

        public void UpdateMusicVolume()
        {
            _musicVolume = musicVolumeSlider.value;
            SetMusicVolume(musicVolumeSlider.value);
        }

        public void UpdateGeneralVolume()
        {
            _masterVolume = generalVolumeSlider.value;
            SetGeneralVolume(generalVolumeSlider.value);
        }
        #endregion
        #endregion
    }
}