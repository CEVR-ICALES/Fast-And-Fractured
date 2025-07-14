using UnityEngine;

namespace DestroyIt
{
    /// <summary>This script will intantiate the specified explosion prefab after [seconds].</summary>
    public class ExplodeAfter : MonoBehaviour
    {
        [Tooltip("Prefab to instantiate when time runs out.")]
        public GameObject explosionPrefab;
        [Tooltip("Seconds to wait before explosion.")]
        public float seconds = 5f;

        private float _timeLeft;
        private bool _isInitialized;

        public void Start()
        {
            _timeLeft = seconds;
            _isInitialized = true;
        }

        public void OnEnable()
        {
            _timeLeft = seconds;
        }

        public void Update()
        {
            if (!_isInitialized) return;

            _timeLeft -= Time.deltaTime;
            if (_timeLeft <= 0)
            {
                if (explosionPrefab != null)
                    Instantiate(explosionPrefab, this.transform.position, Quaternion.identity);

                Destroy(this.gameObject);
            }
        }
    }
}