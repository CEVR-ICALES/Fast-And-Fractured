using FastAndFractured;
using UnityEngine;
using Utilities;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "CheckIfJumpEndedAction", menuName = "MegaChickenStateMachine/Actions/CheckIfJumpEndedAction")]
    public class CheckIfJumpEndedAction : Action
    {
        public override void Act(Controller controller)
        {
            controller.GetBehaviour<ChickenBrain>().CheckIfJumpEnded();
        }
    }
}
