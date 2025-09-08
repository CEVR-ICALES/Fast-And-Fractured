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
            if (physicsBehaviour.Rb.linearVelocity.magnitude > 0.1f)
            {
                Vector3 decelerationForce = -physicsBehaviour.Rb.linearVelocity.normalized * 10f;
                physicsBehaviour.Rb.AddForce(decelerationForce, ForceMode.Acceleration);
            }
            else
            {
                physicsBehaviour.Rb.linearVelocity = Vector3.zero;
            }
        }
    }
}