using Game;
using UnityEngine;
namespace StateMachine
{
    [CreateAssetMenu(fileName = "ApplyMovementAction", menuName = "PlayerStateMachine/Actions/ApplyMovementAction")]

    public class ApplyMovementAction : Action
    {
        public override void Act(Controller controller)
        {
            PlayerInputController playerInputController = controller.GetBehaviour<PlayerInputController>();
            CarMovementController carMovementController = controller.GetBehaviour<CarMovementController>();
            Vector2 moveInput = playerInputController.MoveInput;

            carMovementController.HandleSteeringInput(moveInput); // maybe we will need to send the current input device when calling these functions

            carMovementController.HandleAccelerateInput(playerInputController.IsAccelerating);
            carMovementController.HandleDeaccelerateInput(playerInputController.IsReversing);
        }
    }
}