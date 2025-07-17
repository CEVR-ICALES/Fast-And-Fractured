using UnityEngine;
using Utilities;
namespace FastAndFractured {
    public class ParabolicImpulse : MonoBehaviour
    {
        [SerializeField]
        private float initialVy_impulse = 100f;
        [SerializeField]
        private float angle = 45f;
        [SerializeField]
        private float start_impulse_time = 0.75f;
        [SerializeField]
        private float countdown = 0.5f;
        [SerializeField]
        private Vector3 launchPosition = Vector3.zero;
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
                //characterCar.Rb.linearVelocity = Vector3.zero;
                float moveSpeed = ((launchPosition + transform.position).magnitude - other.transform.position.magnitude)/start_impulse_time;
                Vector3 direction = (launchPosition + transform.position) - other.transform.position;
                TimerSystem.Instance.CreateTimer(start_impulse_time, onTimerDecreaseUpdate: (float time) =>
                {
                    MoveCarToLaunchPosition(other.transform,moveSpeed,direction);
                }, onTimerDecreaseComplete: () =>
                {
                    CarImpulse(characterCar, currentVelocity);
                });
            }
        }

        private void MoveCarToLaunchPosition(Transform car,float moveSpeed, Vector3 direction)
        {
            car.position = car.position + direction * moveSpeed * Time.deltaTime;
        }

        private void CarImpulse(PhysicsBehaviour car,Vector3 startingVelocity)
        {
            float startingVelocityX = startingVelocity.x;
            float startingVelocityZ = startingVelocity.z;

            float forwardVelocityMagnitude = Mathf.Sqrt(Mathf.Pow(startingVelocityX, 2) + Mathf.Pow(startingVelocityZ, 2) + 2 * startingVelocityZ * startingVelocityX * Mathf.Cos(45));
            float initialSpeedNeeded = forwardVelocityMagnitude / Mathf.Cos(angle * Mathf.Deg2Rad);
            //float startingVelocityUp = initialSpeedNeeded * Mathf.Sin(angle * Mathf.Deg2Rad);

            car.Rb.linearVelocity = forwardVelocityMagnitude * car.transform.forward + initialVy_impulse * Vector3.up;

        }
    }
}
