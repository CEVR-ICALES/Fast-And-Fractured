using StateMachine;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(IsDashingDecision), menuName = "PlayerShootingStateMachine/Decisions/IsDashingDecision")]
public class IsDashingDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        // to do
        return true;
    }
}
