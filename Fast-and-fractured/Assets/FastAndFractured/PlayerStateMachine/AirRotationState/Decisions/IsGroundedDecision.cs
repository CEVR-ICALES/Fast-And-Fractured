using StateMachine;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(IsGroundedDecision), menuName = "PlayerStateMachine/Decisions/IsGroundedDecision")]
public class IsGroundedDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        // to do
        return true;
    }
}
