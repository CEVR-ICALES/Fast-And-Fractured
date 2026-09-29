using FastAndFractured;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "ApplyMovementAction", menuName = "PlayerStateMachine/Actions/ApplyMovementAction")]

    public class ApplyMovementAction : Action
    {
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
            if (speed > 0.1f)
            {
                // Never more than would stop the car within one physics step. A flat 10 m/s^2
                // removes 0.2 m/s per 50 Hz step - more than the 0.1 m/s stop threshold - so a car
                // at rest with a little suspension motion overshot zero every step and vibrated
                // (measured in multiplayer: a parked car oscillating at 0.3-1 m/s).
                float deceleration = Mathf.Min(10f, speed / Time.fixedDeltaTime);
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
