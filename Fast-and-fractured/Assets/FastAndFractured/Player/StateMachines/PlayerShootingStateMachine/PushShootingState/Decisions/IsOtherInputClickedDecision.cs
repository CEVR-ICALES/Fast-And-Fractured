using FastAndFractured;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(IsOtherInputClickedDecision), menuName = "PlayerShootingStateMachine/Decisions/IsOtherInputClickedDecision")]
    public class IsOtherInputClickedDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            // to do
            return true;
        }
    }
}

