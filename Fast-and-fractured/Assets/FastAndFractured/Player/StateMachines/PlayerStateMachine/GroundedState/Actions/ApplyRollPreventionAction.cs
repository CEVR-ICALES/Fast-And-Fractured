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
                    controller.GetBehaviour<RollPrevention>().ToggleRollPrevention(true, controller.GetBehaviour<PhysicsBehaviour>().Rb, controller.GetBehaviour<PlayerInputController>().MoveInput.magnitude);
                }
            }
            else
            {
                Debug.Log("In Wall");
                controller.GetBehaviour<RollPrevention>().ToggleRollPrevention(false, controller.GetBehaviour<PhysicsBehaviour>().Rb, controller.GetBehaviour<PlayerInputController>().MoveInput.magnitude);
            }
        }
    }
}
