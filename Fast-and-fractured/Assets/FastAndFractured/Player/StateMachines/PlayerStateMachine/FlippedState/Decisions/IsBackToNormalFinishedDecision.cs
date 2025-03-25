using FastAndFractured;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(IsBackToNormalFinishedDecision), menuName = "PlayerStateMachine/Decisions/IsBackToNormalFinishedDecision")]
    public class IsBackToNormalFinishedDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            // to do
            return true;
        }
    }
}
