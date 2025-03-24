using StateMachine;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(IsUniqueAbilityInputDecision), menuName = "PlayerShootingStateMachine/Decisions/IsUniqueAbilityInputDecision")]
public class IsUniqueAbilityInputDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        return controller.GetBehaviour<PlayerInputController>().IsUsingAbility;
    }
}
