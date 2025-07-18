using UnityEngine;
using Utilities;
namespace FastAndFractured {
    public class ParabolicImpulse : MonoBehaviour
    {
        [SerializeField]
        private bool fixedVY = false;
        [SerializeField]
        private float initialVy_impulse = 100f;
        [Range(1.2f, 5f)]
        [SerializeField]
        private float maxVelocityMultiplicatorFactor = 2.5f;
        [SerializeField]
        private float angle = 45f;
        [SerializeField]
        private float countdown = 0.5f;
        [SerializeField]
        private float maxThrowableWeight = 2000f;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<PhysicsBehaviour>(out var characterCar))
            {
                var characterCarMovementController = characterCar.GetComponent<CarMovementController>();
                Vector3 currentVelocity = characterCar.Rb.linearVelocity;
                CarImpulse(characterCar, currentVelocity);
            }
        }

        private void CarImpulse(PhysicsBehaviour car,Vector3 startingVelocity)
        {
            float startingVelocityX = startingVelocity.x;
            float startingVelocityZ = startingVelocity.z;

            float forwardVelocityMagnitude = Mathf.Sqrt(Mathf.Pow(startingVelocityX, 2) + Mathf.Pow(startingVelocityZ, 2) + 2 * startingVelocityZ * startingVelocityX * Mathf.Cos(45));
            forwardVelocityMagnitude *= maxVelocityMultiplicatorFactor - (1 - (car.Rb.mass/maxThrowableWeight));
            float initialSpeedNeeded = forwardVelocityMagnitude / Mathf.Cos(angle * Mathf.Deg2Rad);
            float startingVelocityUp = initialSpeedNeeded * Mathf.Sin(angle * Mathf.Deg2Rad);
            
            car.Rb.linearVelocity = forwardVelocityMagnitude * car.transform.forward + (fixedVY ? initialVy_impulse : startingVelocityUp) * Vector3.up;
        }
    }
}
