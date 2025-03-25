using Game;
using StateMachine;
using UnityEngine;
[CreateAssetMenu(fileName = nameof(IsUniqueAbilityOnCooldownDecision), menuName = "UniqueAbility/Decisions/IsUniqueAbilityOnCooldownDecision")]
public class IsUniqueAbilityOnCooldownDecision : Decision
{ 
    public override bool Decide(Controller controller)
    {
        return controller.GetBehaviour<BaseUniqueAbility>().IsOnCooldown;
    }
}
