using FastAndFractured;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "ApplyRollPreventionAction", menuName = "PlayerStateMachine/Actions/ApplyRollPreventionAction")]

    public class ApplyRollPreventionAction : Action
    {
        public override void Act(Controller controller)
        {
            if (!controller.GetBehaviour<CarMovementController>().IsInWall())
            {
                if (!controller.GetBehaviour<PhysicsBehaviour>().HasBeenPushed)
                {
                    controller.GetBehaviour<ApplyForceByState>().ToggleRollPrevention(true, controller.GetBehaviour<PlayerInputController>().MoveInput.magnitude);
                }
            }
            else
            {
                controller.GetBehaviour<ApplyForceByState>().ToggleRollPrevention(false, controller.GetBehaviour<PlayerInputController>().MoveInput.magnitude);
            }
        }
    }
}
