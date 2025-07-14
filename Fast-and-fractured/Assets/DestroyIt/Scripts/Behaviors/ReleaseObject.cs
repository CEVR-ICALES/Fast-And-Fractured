using UnityEngine;

namespace DestroyIt
{
    public class ReleaseObject : MonoBehaviour
    {
        public GameObject objectToRelease;
        public Vector3 angularVelocity;
        public Vector3 forceToAdd;

        private Vector3 _velocityLastUpdate;

        void Start()
        {
            _velocityLastUpdate = GetComponent<Rigidbody>().velocity;
        }

        void FixedUpdate()
        {
            Vector3 velocityChange = (GetComponent<Rigidbody>().velocity - _velocityLastUpdate) / GetComponent<Rigidbody>().mass;

            if (velocityChange.magnitude > .3f && objectToRelease != null)
                Release();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (collision.relativeVelocity.magnitude > 2f && objectToRelease != null)
                Release();
        }

        private void Release()
        {
            objectToRelease.GetComponent<Rigidbody>().isKinematic = false;
            objectToRelease.GetComponent<Rigidbody>().angularVelocity = angularVelocity;
            objectToRelease.GetComponent<Rigidbody>().WakeUp();
            if (forceToAdd != Vector3.zero)
                objectToRelease.GetComponent<Rigidbody>().AddForce(forceToAdd);
            Destroy(this);
        }
    }
}