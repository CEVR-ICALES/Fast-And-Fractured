using StateMachine;
using UnityEngine;
[CreateAssetMenu(fileName = "ApplyBrakeAction", menuName = "PlayerStateMachine/Actions/ApplyBrakeAction")]


public class ApplyBrakeAction : Action
{
    public override void Act(Controller controller)
    {
        PlayerInputController playerInputController = controller.GetBehaviour<PlayerInputController>();
        controller.GetBehaviour<CarMovementController>().HandleBrakingInput(playerInputController.IsBraking, playerInputController.MoveInput);
    }
}
