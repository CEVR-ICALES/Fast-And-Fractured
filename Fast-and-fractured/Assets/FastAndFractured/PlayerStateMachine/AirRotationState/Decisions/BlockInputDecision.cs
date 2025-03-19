using StateMachine;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(BlockInputDecision), menuName = "PlayerStateMachine/Decisions/BlockInputDecision")]
public class BlockInputDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        // to do
        return false;
    }
}
