using UnityEngine;
using FastAndFractured;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(IsMineShootInputDecision), menuName = "PlayerShootingStateMachine/Decisions/IsMineShootInputDecision")]
    public class IsMineShootInputDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            return controller.GetBehaviour<PlayerInputController>().IsThrowingMine;
        }
    }
}
