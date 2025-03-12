using StateMachine;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(DashEndingTimeDecision), menuName = "PlayerShootingStateMachine/Decisions/DashEndingTimeDecision")]
public class DashEndingTimeDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        // to do
        return true;
    }
}
