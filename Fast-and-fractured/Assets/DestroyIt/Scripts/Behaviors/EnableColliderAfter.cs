using UnityEngine;

namespace DestroyIt
{
    /// <summary>
    /// This script is used in conjunction with Object Pooling to enhance performance, in particular with mobile devices.
    /// This script enables the colliders on a game object over a small amount of time, so the physics load on the CPU is spaced out.
    /// </summary>
    public class EnableColliderAfter : MonoBehaviour
    {
        public float seconds;   // seconds to wait before enabling the collider on this game object.
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
            {
                Collider[] colliders = gameObject.GetComponents<Collider>();
                for (int i = 0; i < colliders.Length; i++)
                    colliders[i].enabled = true;

                Destroy(this);
            }
        }
    }
}