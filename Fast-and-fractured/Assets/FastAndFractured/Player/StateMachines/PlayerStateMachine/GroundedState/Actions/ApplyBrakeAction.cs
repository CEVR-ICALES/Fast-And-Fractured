using FastAndFractured;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "ApplyBrakeAction", menuName = "PlayerStateMachine/Actions/ApplyBrakeAction")]
    public class ApplyBrakeAction : Action
    {
        public override void Act(Controller controller)
        {
            PlayerInputController playerInputController = controller.GetBehaviour<PlayerInputController>();
            if (playerInputController != null && playerInputController.IsMovementInputsBlocked)
                return;

            // Read the input actually driving this car, not the local PlayerInputController: in
            // multiplayer the server read false/(0,0) for a client's car every tick, cancelling
            // that client's brake and drift on the authoritative simulation.
            CarMovementController carMovement = controller.GetBehaviour<CarMovementController>();
            IInputProvider input = carMovement.InputProvider;
            if (input == null) return;
            carMovement.HandleBrakingInput(input.IsBraking, input.MoveInput);
        }
    }
}
