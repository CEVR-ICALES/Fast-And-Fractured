using UnityEngine;
using FastAndFractured;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(IsChickenGoingToJumpDecision), menuName = "MegaChickenStateMachine/Decisions/IsChickenGoingToJumpDecision")]
    public class IsChickenGoingToJumpDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            return controller.GetBehaviour<ChickenBrain>().IsJumping;
        }
    }
}
