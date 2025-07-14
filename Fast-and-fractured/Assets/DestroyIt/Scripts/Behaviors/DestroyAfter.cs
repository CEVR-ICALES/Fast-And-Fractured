using UnityEngine;

namespace DestroyIt
{
    /// <summary>This script will destroy the gameobject it is attached to after [seconds].</summary>
    public class DestroyAfter : MonoBehaviour
    {
        public float seconds;   // seconds to wait before destroying this game object.
        private float timeLeft;
        private bool isInitialized;

        void Start()
        {
            timeLeft = seconds;
            isInitialized = true;
        }

        void OnEnable()
        {
            timeLeft = seconds;
        }

        void Update()
        {
            if (!isInitialized) return;

            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0)
                Destroy(this.gameObject);
        }
    }
}