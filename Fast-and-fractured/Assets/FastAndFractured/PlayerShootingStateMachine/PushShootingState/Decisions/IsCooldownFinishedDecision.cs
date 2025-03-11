using StateMachine;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(IsCooldownFinishedDecision), menuName = "PlayerShootingStateMachine/Decisions/IsCooldownFinishedDecision")]
public class IsCooldownFinishedDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        // to do
        return true;
    }
}
