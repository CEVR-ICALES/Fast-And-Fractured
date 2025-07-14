using UnityEngine;

namespace DestroyIt
{
    /// <summary>
    /// Place this script on your destroyed prefab, at the highest parent level.
    /// When your destroyed prefab spawns in, it will wait for a while and then reset itself to whatever game object you have assigned to resetToPrefab.
    /// </summary>
    public class ResetDestructible : MonoBehaviour
    {
        [Tooltip("The game object prefab you want to reset this destructible object to after it has been destroyed. (Usually a Pristine version of this destroyed object.)")]
        public GameObject resetToPrefab;

        [Tooltip("The minimum amount of time to wait (in seconds) before resetting the destructible object. (3600 seconds = 1 hour)")]
        public float minWaitSeconds = 30.0f;

        [Tooltip("The maximum amount of time to wait (in seconds) before resetting the destructible object. (3600 seconds = 1 hour)")]
        public float maxWaitSeconds = 45.0f;

        private float _timeLeft;
        private bool _isInitialized;

        void Start()
        {
            if (resetToPrefab == null)
            {
                Debug.LogError("ResetDestructible Script: You need to assign a prefab to the [resetToPrefab] field.");
                Destroy(this);
                return;
            }

            // Randomly determine when to reset the destroyed object, based on the min/max wait seconds.
            _timeLeft = maxWaitSeconds <= minWaitSeconds ? 0f : Random.Range(minWaitSeconds, maxWaitSeconds);

            Debug.Log($"[{gameObject.name}] will be reset in approximately {Mathf.RoundToInt(_timeLeft)} seconds.");

            _isInitialized = true;
        }

        void Update()
        {
            if (!_isInitialized) return;

            _timeLeft -= Time.deltaTime;
            if (_timeLeft <= 0)
            {
                // Spawn the resetToPrefab object after the required wait time.
                GameObject go = Instantiate(resetToPrefab, transform.position, transform.rotation, transform.parent);
                go.transform.localScale = transform.localScale; // in case the destroyed object was scaled in the scene
                Debug.Log($"[{gameObject.name}] has been reset to [{resetToPrefab.name}].");

                Destroy(gameObject); // remove this gameObject to cleanup the scene
            }
        }
    }
}