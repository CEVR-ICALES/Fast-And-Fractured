using UnityEngine;
using Utilities;
namespace FastAndFractured {
    public class ParabolicImpulse : MonoBehaviour
    {
        [Tooltip("If is set to false, the Vy form the impulse will be calculated based on the Vx of the object and the angle given." +
            "if is set to true, you will use the initialVy_impulse value")]
        [SerializeField]
        private bool fixedVY = false;
        [SerializeField]
        private float initialVy_impulse = 100f;
        [Tooltip("The multiplicator value added to the final Vx speed of the impulse.")]
        [Range(1.2f, 15f)]
        [SerializeField]
        private float maxVelocityMultiplicatorFactor = 2.5f;
        [SerializeField]
        private float angle = 45f;
        [SerializeField]
        private float countdown = 0.5f;
        private bool isOnCountdown = false;
        [SerializeField]
        private float maxThrowableWeightForCars = 2000f;
        [SerializeField]
        private float forwardSpeedLimit = 50f;

        private const float GENERIC_MASS = 5;

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
            if (!isOnCountdown)
            {
                if (other.TryGetComponent<PhysicsBehaviour>(out var characterCar))
                {
                    var characterCarMovementController = characterCar.GetComponent<CarMovementController>();
                    Vector3 currentVelocity = characterCar.Rb.linearVelocity;
                    ImpulseRigydbody(characterCar.Rb, currentVelocity, maxThrowableWeightForCars);
                }
                else if (other.TryGetComponent<ICanBeImpulseByTrampoline>(out var canBeImpulseByTrampoline))
                {
                    Rigidbody rb = canBeImpulseByTrampoline.GetRigidbody();
                    ImpulseRigydbody(rb, rb.linearVelocity, GENERIC_MASS);
                }
            }
        }

        private void ImpulseRigydbody(Rigidbody rb, Vector3 startingVelocity, float maxWeightReference)
        {
            isOnCountdown = true;
            float startingVelocityX = startingVelocity.x;
            float startingVelocityZ = startingVelocity.z;

            Vector3 forwardVelocity = new Vector3(startingVelocity.x, 0, startingVelocity.z);
            forwardVelocity *= maxVelocityMultiplicatorFactor - (1 - (rb.mass/maxWeightReference));
            float forwardVelocityMagnitude = Mathf.Clamp(forwardVelocity.magnitude, 0, forwardSpeedLimit);
            float initialSpeedNeeded = forwardVelocityMagnitude / Mathf.Cos(angle * Mathf.Deg2Rad);
            float startingVelocityUp = initialSpeedNeeded * Mathf.Sin(angle * Mathf.Deg2Rad);

            rb.linearVelocity = (forwardVelocity.normalized * forwardVelocityMagnitude) + (fixedVY ? initialVy_impulse : startingVelocityUp) * Vector3.up;
            TimerSystem.Instance.CreateTimer(countdown, onTimerDecreaseComplete: () =>
            {
                isOnCountdown = false;
            });
        }
    }
}
