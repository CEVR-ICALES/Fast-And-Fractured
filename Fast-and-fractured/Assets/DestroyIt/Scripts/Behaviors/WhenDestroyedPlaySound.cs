using UnityEngine;

namespace DestroyIt
{
    /// <summary>
    /// This is a helper script that listens to a Destructible object's DestroyedEvent and plays a sound when the object is destroyed.
    /// Put this script on a GameObject that has the Destructible script.
    /// </summary>
    public class WhenDestroyedPlaySound : MonoBehaviour
    {
        public AudioClip clip;
        private Destructible _destObj;

        private void Start()
        {
            // Try to get the Destructible script on the object. If found, attach the OnDestroyed event listener to the DestroyedEvent.
            _destObj = gameObject.GetComponent<Destructible>();
            if (_destObj != null)
                _destObj.DestroyedEvent += OnDestroyed;
        }

        private void OnDisable()
        {
            // Unregister the event listener when disabled/destroyed. Very important to prevent memory leaks due to orphaned event listeners!
            if (_destObj == null) return;
            _destObj.DestroyedEvent -= OnDestroyed;
        }

        /// <summary>When the Destructible object is destroyed, the code in this method will run.</summary>
        private void OnDestroyed()
        {
            // Create a new game object to play the audio clip, and position it at the destroyed game object's location.
            var audioObj = new GameObject("Audio Source");
            audioObj.transform.position = _destObj.transform.position;
            var audioSource = audioObj.AddComponent<AudioSource>();
            var destroyAfter = audioObj.AddComponent<DestroyAfter>();
            destroyAfter.seconds = 5f; // Destroy the audio source object after X seconds.
            audioSource.PlayOneShot(clip); // Play the audio clip.
        }
    }
}