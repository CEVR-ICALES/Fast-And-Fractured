using FastAndFractured;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(IsFlippedDecision), menuName = "PlayerStateMachine/Decisions/IsFlippedDecision")]
    public class IsFlippedDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            return controller.GetBehaviour<CarMovementController>().IsFlipped;
        }
    }

}
