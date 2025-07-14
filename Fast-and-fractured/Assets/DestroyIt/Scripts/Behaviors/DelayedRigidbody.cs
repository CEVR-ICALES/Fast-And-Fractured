using UnityEngine;

namespace DestroyIt
{
    /// <summary>
    /// This script is used to add a Rigidbody component to a gameobject through code. It allows you to start with an object that has no Rigidbody
    /// and add one at a later time by calling Initialize() on this script. The script deletes itself after adding the Rigidbody.
    /// </summary>
    public class DelayedRigidbody : MonoBehaviour
    {
        public float mass = 1f;
        public float drag = 0f;
        public float angularDrag = 0.05f;
        public float delaySeconds = 0f;
        public bool reenableColliders = true;

        /// <summary>This is called whenever you want to add a rigidbody to the game object.</summary>
        public void Initialize()
        {
            Invoke("AddRigidbody", delaySeconds);
        }

        public void AddRigidbody()
        {
            // Add a rigidbody component if it doesn't already exist.
            if (gameObject.GetComponent<Rigidbody>() == null)
            {
                Rigidbody rbody = gameObject.AddComponent<Rigidbody>();
                rbody.mass = mass;
                rbody.drag = drag;
                rbody.angularDrag = angularDrag;
            }

            if (reenableColliders)
            {
                Collider[] colliders = gameObject.GetComponentsInChildren<Collider>();
                foreach(Collider coll in colliders)
                    coll.enabled = true;
            }

            // Deletes this script when finished.
            Destroy(this);
        }
    }
}