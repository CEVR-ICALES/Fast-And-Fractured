using Utilities;
using FMODUnity;
using FMOD.Studio;
using UnityEngine;
using System.Collections.Generic;

namespace Utilities
{
    public class SoundManager : AbstractSingleton<SoundManager>
    {
        [Range(0f, 1f)]
        public float masterVolume = 1f;

        private Dictionary<string, Queue<EventInstance>> _eventPool = new Dictionary<string, Queue<EventInstance>>();
        private Dictionary<string, EventInstance> _activeEvents = new Dictionary<string, EventInstance>();

        protected override void Awake()
        {
            base.Awake();
        }

        private void Start()
        {
            SetSFXVolume(masterVolume);
        }

        private void Update()
        {
            SetSFXVolume(masterVolume);
        }

        #region Event Pooling Methods
        /// <summary>
        /// Retrieves an EventInstance from the pool if available; otherwise, create a new one
        /// </summary>
        /// <param name="eventPath">Path of the FMOD event</param>
        /// <returns>EventInstance to be used</returns>
        private EventInstance GetPooledEventInstance(string eventPath)
        {
            if (_eventPool.TryGetValue(eventPath, out Queue<EventInstance> eventQueue) && eventQueue.Count > 0)
                return eventQueue.Dequeue();

            return RuntimeManager.CreateInstance(eventPath);
        }

        /// <summary>
        /// Stops an EventInstance and returns it to the pool for future reuse
        /// </summary>
        /// <param name="eventPath">Path of the FMOD event</param>
        /// <param name="instance">EventInstance to be returned to the pool</param>
        private void ReturnEventInstanceToPool(string eventPath, EventInstance instance)
        {
            if (!_eventPool.ContainsKey(eventPath))
                _eventPool[eventPath] = new Queue<EventInstance>();

            instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            instance.setParameterByName("Volume", 0f);
            _eventPool[eventPath].Enqueue(instance);
        }
        #endregion

        #region Play Sounds Methods
        /// <summary>
        /// Plays a one-shot sound at a specific world position
        /// </summary>
        /// <param name="eventPath">Path of the FMOD event</param>
        /// <param name="worldPosition">The world position to play the sound at</param>
        public void PlayOneShot(string eventPath, Vector3 worldPosition)
        {
            RuntimeManager.PlayOneShot(eventPath, worldPosition);
        }

        /// <summary>
        /// Plays a looping sound event and keeps track of it
        /// </summary>
        /// <param name="eventPath">Path of the FMOD event</param>
        /// <param name="soundInstance">Created FMOD EventInstance reference</param>
        public void PlaySound(string eventPath, out EventInstance soundInstance)
        {
            soundInstance = RuntimeManager.CreateInstance(eventPath);
            _activeEvents[eventPath] = soundInstance;
            soundInstance.start();
        }

        /// <summary>
        /// Plays a 3D sound event at a specific world position
        /// </summary>
        /// <param name="eventPath">Path of the FMOD event</param>
        /// <param name="position">Position in the world space</param>
        public void PlaySound3D(string eventPath, Vector3 position)
        {
            EventInstance soundInstance = RuntimeManager.CreateInstance(eventPath);
            _activeEvents[eventPath] = soundInstance;
            soundInstance.set3DAttributes(RuntimeUtils.To3DAttributes(position));
            soundInstance.start();
        }
        #endregion

        #region Stop Sounds Methods
        /// <summary>
        /// Stops a currently playing sound event
        /// </summary>
        /// <param name="eventPath">Path of the FMOD event</param>
        public void StopSound(string eventPath)
        {
            if (_activeEvents.TryGetValue(eventPath, out EventInstance instance))
            {
                ReturnEventInstanceToPool(eventPath, instance);
                _activeEvents.Remove(eventPath);
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
        /// <param name="eventPath">Path of the FMOD event</param>
        public void PauseAudio(string eventPath)
        {
            if (_activeEvents.TryGetValue(eventPath, out EventInstance instance))
                instance.setPaused(true);
        }

        /// <summary>
        /// Resumes a paused sound event
        /// </summary>
        /// <param name="eventPath">Path of the FMOD event</param>
        public void ResumeAudio(string eventPath)
        {
            if (_activeEvents.TryGetValue(eventPath, out EventInstance instance))
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
        #endregion
    }
}