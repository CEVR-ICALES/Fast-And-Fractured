using StateMachine;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(IsShootInputDecision), menuName = "PlayerShootingStateMachine/Decisions/IsShootInputDecision")]
public class IsShootInputDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        // to do
        return true;
    }
}
