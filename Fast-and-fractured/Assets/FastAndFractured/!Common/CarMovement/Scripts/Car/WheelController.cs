using UnityEngine;

namespace FastAndFractured
{
    public class WheelController : MonoBehaviour
    {
        [Header("References")]
        public WheelCollider wheelCollider;
        public Transform wheelMesh;

        [Header("Wheel Settings")]
        [SerializeField] private bool _isTractionWheel; //if this wheel generates accelaration or not
        [SerializeField] private float _steeringResistance; // resistance to prevent rolling over

        private void OnEnable()
        {
            wheelCollider = GetComponent<WheelCollider>();
        }
        public void ApplyMotorTorque(float torque)
        {
            if(_isTractionWheel)
            {
                wheelCollider.motorTorque = torque;
            }
        }

        public void ApplyBrakeTorque(float torque)
        {
            wheelCollider.brakeTorque = torque;
        }

        public void ApplySteering(float angle)
        {
            wheelCollider.steerAngle = angle;
        }

        public void UpdateWheelVisuals()
        {
            Vector3 position;
            Quaternion rotation;

            wheelCollider.GetWorldPose(out position, out rotation);
            if(wheelMesh != null)
            {
                wheelMesh.position = position;
                wheelMesh.rotation = rotation;
            }

        }

        public WheelGroundInfo GetGroundInfo() // returns angle and contact normal
        {
            WheelGroundInfo info = new WheelGroundInfo
            {
                isGrounded = wheelCollider.GetGroundHit(out WheelHit hit),
                slopeAngle = 0f,
                groundNormal = Vector3.up
            };

            if (info.isGrounded)
            {
                info.slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
                info.groundNormal = hit.normal;
            }

            return info;
        }

        //currently not being used
        public void ApplySteeringResistance(Rigidbody carRb)//each wheel inividually applies a little bit of force on the oposite direction of its movement
        {
            Vector3 steeringDirection = transform.right;//directionm of the steering force
            Vector3 tireWorldVelocity = carRb.GetPointVelocity(wheelCollider.transform.position);// wheel velocity

            float steeringVelocity = Vector3.Dot(steeringDirection, tireWorldVelocity);// velocity in the steering direction
            float desiredVelocityChange = -steeringVelocity * _steeringResistance;// calculate resistance
            float desiredAcceleration = desiredVelocityChange / Time.fixedDeltaTime;//acceleration needed to apply

            Vector3 resistanceForce = steeringDirection * (wheelCollider.mass * desiredAcceleration); // f = m * a + direction
            carRb.AddForceAtPosition(resistanceForce, wheelCollider.transform.position);
        }
    }
}

public struct WheelGroundInfo
{
    public bool isGrounded;
    public float slopeAngle;
    public Vector3 groundNormal;    
}

