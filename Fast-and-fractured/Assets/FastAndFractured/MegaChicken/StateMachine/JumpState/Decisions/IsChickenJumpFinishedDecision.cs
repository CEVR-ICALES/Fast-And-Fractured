using UnityEngine;
using FastAndFractured;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(IsChickenJumpFinishedDecision), menuName = "MegaChickenStateMachine/Decisions/IsChickenJumpFinishedDecision")]
    public class IsChickenJumpFinishedDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            return controller.GetBehaviour<ChickenBrain>().IsIdle;
        }
    }
}
