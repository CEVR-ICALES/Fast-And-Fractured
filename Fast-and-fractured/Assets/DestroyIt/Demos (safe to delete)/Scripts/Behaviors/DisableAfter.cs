using UnityEngine;

namespace DestroyIt
{
    public class DisableAfter : MonoBehaviour
    {
        public float seconds;   // seconds to wait before disabling this game object.
        public bool removeScript;  // remove this script after disabled?

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
                this.gameObject.SetActive(false);
                if (removeScript)
                    Destroy(this);
            }
        }
    }
}
