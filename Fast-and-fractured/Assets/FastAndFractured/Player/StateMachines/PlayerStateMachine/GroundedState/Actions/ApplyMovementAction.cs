using FastAndFractured;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "ApplyMovementAction", menuName = "PlayerStateMachine/Actions/ApplyMovementAction")]

    public class ApplyMovementAction : Action
    {
        PlayerInputController playerInputController;
        CarMovementController carMovementController;
        PhysicsBehaviour physicsBehaviour;
        Vector2 moveInput;

        public override void Act(Controller controller)
        {
            playerInputController = controller.GetBehaviour<PlayerInputController>();
            carMovementController = controller.GetBehaviour<CarMovementController>();
            physicsBehaviour = controller.GetBehaviour<PhysicsBehaviour>();
            moveInput = playerInputController.MoveInput;

            carMovementController.HandleSteeringInput(moveInput);
            
            if (playerInputController.IsAccelerating != 0)
            {
                carMovementController.HandleAccelerateInput(playerInputController.IsAccelerating);
            }
            
            if (playerInputController.IsReversing != 0)
            {
                carMovementController.HandleAccelerateInput(-playerInputController.IsReversing);
            }

            if (playerInputController.IsAccelerating == 0 && playerInputController.IsReversing == 0 && moveInput.y == 0)
            {
                HandleAutoDeceleration();
            }
        }

        void HandleAutoDeceleration()
        {
            carMovementController.HandleAccelerateInput(0f);
            carMovementController.HandleSteeringInput(new Vector2(moveInput.x, 0f));

            if (physicsBehaviour.Rb.velocity.magnitude > 0.1f)
            {
                Vector3 decelerationForce = -physicsBehaviour.Rb.velocity.normalized * 10f;
                physicsBehaviour.Rb.AddForce(decelerationForce, ForceMode.Acceleration);
            }
            else
            {
                physicsBehaviour.Rb.velocity = Vector3.zero;
            }
        }

    }
}