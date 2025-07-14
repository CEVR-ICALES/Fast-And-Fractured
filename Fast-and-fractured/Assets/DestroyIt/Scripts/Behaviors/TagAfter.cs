using UnityEngine;

namespace DestroyIt
{
    /// <summary>
    /// This script tags all colliders on the object after X seconds. It is used to tag colliders on destroyed object
    /// debris with minimal performance impact.
    /// </summary>
    public class TagAfter : MonoBehaviour
    {
        public float seconds = 1f;   // seconds to wait before tagging all colliders under this game object.
        public Tag tagWith; // what to tag all colliders under this game object with.

        private float _timeLeft;
        private bool _isInitialized;

        void Start()
        {
            _timeLeft = seconds;
            _isInitialized = true;
        }

        void OnEnable()
        {
            _timeLeft = seconds;
        }

        void Update()
        {
            if (!_isInitialized) return;

            _timeLeft -= Time.deltaTime;
            if (_timeLeft <= 0)
            {
                Collider[] colls = this.gameObject.GetComponentsInChildren<Collider>();
                for (int i = 0; i < colls.Length; i++)
                    colls[i].gameObject.AddTag(tagWith);

                Destroy(this);
            }

        }
    }
}
