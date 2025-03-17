using Game;
using UnityEngine;
namespace StateMachine
{
    [CreateAssetMenu(fileName = "ApplyRollPreventionAction", menuName = "PlayerStateMachine/Actions/ApplyRollPreventionAction")]

    public class ApplyRollPreventionAction : Action
    {
        public override void Act(Controller controller)
        {
            controller.GetBehaviour<RollPrevention>().ApplyRollPrevention(controller.GetBehaviour<PhysicsBehaviour>().Rb, controller.GetBehaviour<PlayerInputController>().MoveInput.magnitude);
        }
    }
}
