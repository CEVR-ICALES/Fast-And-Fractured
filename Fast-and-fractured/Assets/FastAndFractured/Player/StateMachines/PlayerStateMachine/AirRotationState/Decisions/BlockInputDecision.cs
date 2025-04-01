using FastAndFractured;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(BlockInputDecision), menuName = "PlayerStateMachine/Decisions/BlockInputDecision")]
    public class BlockInputDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            // to do
            return false;
        }
    }
}

