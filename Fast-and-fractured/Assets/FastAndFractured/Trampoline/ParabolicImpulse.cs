using FastAndFractured.Utilities;
using UnityEngine;
using Utilities;
namespace FastAndFractured {
    public class ParabolicImpulse : MonoBehaviour
    {
        [SerializeField]
        private bool rangeDependent = true;
        [SerializeField]
        private float angle = 45f;
        [SerializeField]
        private float countdown = 0.5f;
        private bool isOnCountdown = false;

        [Header("Range Dependent values")]

        [SerializeField]
        private float range = 10f;
        [SerializeField]
        private float startSpeedMagnitudeToImpulse = 10f;
        [SerializeField]
        private landingCheck landindCheck;
        private float _landingTime;

        [Header("Speed dependent values")]

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
        private float forwardSpeedLimit = 50f;
        [SerializeField]
        private float maxThrowableWeightForCars = 2000f;




        private const float GENERIC_MASS = 5;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            landindCheck.onLanding.AddListener(SetLandingTime);
            landindCheck.StartChecking(ParabolicForce(landindCheck.GetRigidbody()), ForceMode.VelocityChange);
        }

        // Update is called once per frame
        void Update()
        {

        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isOnCountdown)
            {
                isOnCountdown = true;
                if (other.TryGetComponent<PhysicsBehaviour>(out var characterCar))
                {
                    if (!rangeDependent)
                    {
                        Vector3 currentVelocity = characterCar.Rb.linearVelocity;
                        ImpulseRigydbody(characterCar.Rb, currentVelocity, maxThrowableWeightForCars,characterCar);
                    }
                    else
                    {
                        ParabolicRangeMovement(characterCar.Rb,characterCar);
                    }
                }
                else if (other.TryGetComponent<ICanBeImpulseByTrampoline>(out var canBeImpulseByTrampoline))
                {
                    ICustomRigidbody rb = canBeImpulseByTrampoline.GetRigidbody();
                    if (!rangeDependent)
                    {
                        ImpulseRigydbody(rb, rb.linearVelocity, GENERIC_MASS);
                    }
                    else
                    {
                        ParabolicRangeMovement(rb);
                    }
                }
                TimerSystem.Instance.CreateTimer(countdown, onTimerDecreaseComplete: () =>
                {
                    isOnCountdown = false;
                });
            }
        }

        private void ImpulseRigydbody(ICustomRigidbody rb, Vector3 startingVelocity, float maxWeightReference)
        {
            Vector3 velocity = ImpulseForce(rb,startingVelocity,maxWeightReference);
            rb.AddForce(velocity,ForceMode.VelocityChange);
        }

        private void ImpulseRigydbody(Rigidbody rb, Vector3 startingVelocity, float maxWeightReference,PhysicsBehaviour physicsBehaviour)
        {
            Vector3 velocity = ImpulseForce(rb, startingVelocity, maxWeightReference);
            physicsBehaviour.ApplyImpulse(velocity, ForceMode.VelocityChange, false, _landingTime);
        }


        private Vector3 ImpulseForce(Rigidbody rb, Vector3 startingVelocity, float maxWeightReference)
        {
            Vector3 forwardVelocity = new Vector3(startingVelocity.x, 0, startingVelocity.z);
            forwardVelocity *= maxVelocityMultiplicatorFactor - (1 - (rb.mass / maxWeightReference));
            float forwardVelocityMagnitude = Mathf.Clamp(forwardVelocity.magnitude, 0, forwardSpeedLimit);
            float initialSpeedNeeded = forwardVelocityMagnitude / Mathf.Cos(angle * Mathf.Deg2Rad);
            float startingVelocityUp = initialSpeedNeeded * Mathf.Sin(angle * Mathf.Deg2Rad);
            return (forwardVelocity.normalized * forwardVelocityMagnitude) +(fixedVY ? initialVy_impulse : startingVelocityUp) * Vector3.up;
        }

        private void ParabolicRangeMovement(Rigidbody rb,PhysicsBehaviour physicsBehaviour)
        {
            Vector3 force = ParabolicForce(rb);
            physicsBehaviour.ApplyImpulse(force, ForceMode.VelocityChange, false, _landingTime);
        }

        private void ParabolicRangeMovement(Rigidbody rb)
        {
            Vector3 force = ParabolicForce(rb);
            rb.AddForce(force, ForceMode.VelocityChange);
        }

        private Vector3 ParabolicForce(Rigidbody rb)
        {
            float target_Velocity = Mathf.Sqrt((range * Mathf.Abs(Physics.gravity.y)) / Mathf.Sin(2 * angle * Mathf.Deg2Rad));

            float Vx = target_Velocity * Mathf.Cos(angle * Mathf.Deg2Rad);
            float Vy = target_Velocity * Mathf.Sin(angle * Mathf.Deg2Rad);
            Vector3 velocityVectorX = transform.forward * Vx;
            Vector3 velocity = velocityVectorX + transform.up * Vy;
            return velocity;
        }
        private void SetLandingTime(float landingTime)
        {
            _landingTime = landingTime;
        }
    }
}
