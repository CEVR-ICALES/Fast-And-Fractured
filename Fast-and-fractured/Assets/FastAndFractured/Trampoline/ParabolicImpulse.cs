using UnityEngine;
using Utilities;
namespace FastAndFractured {
    public class ParabolicImpulse : MonoBehaviour
    {
        [SerializeField]
        private bool fixedVY = false;
        [SerializeField]
        private float initialVy_impulse = 100f;
        [Range(1.2f, 15f)]
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

            Vector3 forwardVelocity = new Vector3(startingVelocity.x,0, startingVelocity.z);
            float initialSpeedNeeded = forwardVelocity.magnitude / Mathf.Cos(angle * Mathf.Deg2Rad);
            float startingVelocityUp = initialSpeedNeeded * Mathf.Sin(angle * Mathf.Deg2Rad);
            
            car.Rb.linearVelocity = forwardVelocity + (fixedVY ? initialVy_impulse : startingVelocityUp) * Vector3.up;
        }
    }
}
