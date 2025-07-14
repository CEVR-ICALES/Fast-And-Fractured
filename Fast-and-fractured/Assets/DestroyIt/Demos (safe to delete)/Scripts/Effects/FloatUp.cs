using UnityEngine;

namespace DestroyIt
{
    public class FloatUp : MonoBehaviour
    {
        [Range(0f, 10f)]
        public float floatSpeed = 5f;

        private float checkFrequency = 0.05f; // The time (in seconds) this script checks for updates.
        private float nextUpdateCheck;

        // Use this for initialization
        void Start()
        {
            nextUpdateCheck = Time.time + checkFrequency;
        }

        // Update is called once per frame
        void Update()
        {
            if (Time.time > nextUpdateCheck)
            {
                // float up
                this.gameObject.transform.position = this.gameObject.transform.position + (Vector3.up * floatSpeed);

                // reset the counter
                nextUpdateCheck = Time.time + checkFrequency;
            }
        }
    }
}