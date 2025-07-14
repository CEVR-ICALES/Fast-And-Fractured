using UnityEngine;

namespace DestroyIt
{
    public class PowerSource : MonoBehaviour
    {
        public bool hasPower = true;
        public bool cutPowerOnRapidTilt = true;
        public float tiltThreshold = 1.5f;

        void Update()
        {
            // Cut power when object is tilted suddenly (angular velocity goes up)
            if (cutPowerOnRapidTilt && hasPower && GetComponent<Rigidbody>() != null && GetComponent<Rigidbody>().angularVelocity.magnitude > tiltThreshold)
                Destroy(this);
        }
    }
}