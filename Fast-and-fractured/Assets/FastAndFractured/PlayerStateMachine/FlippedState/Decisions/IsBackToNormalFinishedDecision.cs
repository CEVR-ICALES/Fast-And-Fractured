using StateMachine;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(IsBackToNormalFinishedDecision), menuName = "PlayerStateMachine/Decisions/IsBackToNormalFinishedDecision")]
public class IsBackToNormalFinishedDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        // to do
        return true;
    }
}