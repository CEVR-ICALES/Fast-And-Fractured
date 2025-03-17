using StateMachine;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(IsPushShootInputDecision), menuName = "PlayerShootingStateMachine/Decisions/IsPushShootInputDecision")]
public class IsPushShootInputDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        return controller.GetBehaviour<PlayerInputController>().IsPushShootMode;
    }
}
