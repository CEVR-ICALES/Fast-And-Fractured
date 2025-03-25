using FastAndFractured;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(IsGroundedDecision), menuName = "PlayerStateMachine/Decisions/IsGroundedDecision")]
    public class IsGroundedDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            // to do
            return true;
        }
    }
}

