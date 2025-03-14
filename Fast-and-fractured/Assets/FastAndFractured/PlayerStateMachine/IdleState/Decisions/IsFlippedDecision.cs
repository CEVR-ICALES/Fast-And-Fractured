using StateMachine;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(IsFlippedDecision), menuName = "PlayerStateMachine/Decisions/IsFlippedDecision")]
public class IsFlippedDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        // to do
        return false;
    }
}
