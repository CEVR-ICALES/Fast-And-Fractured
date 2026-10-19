using FastAndFractured;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "ApplyMovementAction", menuName = "PlayerStateMachine/Actions/ApplyMovementAction")]

    public class ApplyMovementAction : Action
    {
        private const float STOP_SPEED = 0.1f;
        private const float AUTO_DECELERATION = 10f;

        CarMovementController carMovementController;
        PhysicsBehaviour physicsBehaviour;
       

        public override void Act(Controller controller)
        {
            carMovementController = controller.GetBehaviour<CarMovementController>();
            physicsBehaviour = controller.GetBehaviour<PhysicsBehaviour>();
            carMovementController.ProcessMovementInput();

            if (carMovementController.InputProvider.MoveInput.y == 0)
            {
                HandleAutoDeceleration();
            }
        }

        void HandleAutoDeceleration()
        {
            float speed = physicsBehaviour.Rb.linearVelocity.magnitude;
            if (speed > STOP_SPEED)
            {
                // Never more than would stop the car within one physics step, or a car at rest overshoots zero and vibrates.
                float deceleration = Mathf.Min(AUTO_DECELERATION, speed / Time.fixedDeltaTime);
                Vector3 decelerationForce = -physicsBehaviour.Rb.linearVelocity.normalized * deceleration;
                physicsBehaviour.Rb.AddForce(decelerationForce, ForceMode.Acceleration);
            }
            else
            {
                physicsBehaviour.Rb.linearVelocity = Vector3.zero;
            }
        }
    }
}
