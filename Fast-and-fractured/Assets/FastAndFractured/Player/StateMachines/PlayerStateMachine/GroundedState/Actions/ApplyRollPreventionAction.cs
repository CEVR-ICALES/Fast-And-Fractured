using FastAndFractured;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "ApplyRollPreventionAction", menuName = "PlayerStateMachine/Actions/ApplyRollPreventionAction")]

    public class ApplyRollPreventionAction : Action
    {
        public override void Act(Controller controller)
        {
            CarMovementController carMovement = controller.GetBehaviour<CarMovementController>();
            // The input actually driving this car. In multiplayer the local PlayerInputController
            // belongs to whoever runs this peer, so the server read (0,0) for a client's car while
            // that client read its real input - a different downward force every physics step.
            float inputMagnitude = carMovement.InputProvider != null ? carMovement.InputProvider.MoveInput.magnitude : 0f;

            if (!carMovement.IsInWall())
            {
                if (!controller.GetBehaviour<PhysicsBehaviour>().HasBeenPushed)
                {
                    controller.GetBehaviour<ApplyForceByState>().ToggleRollPrevention(true, inputMagnitude);
                }
            }
            else
            {
                controller.GetBehaviour<ApplyForceByState>().ToggleRollPrevention(false, inputMagnitude);
            }
        }
    }
}
