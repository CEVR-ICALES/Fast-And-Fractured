using Game;
using StateMachine;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(IsUniqueAbilityActive), menuName = "UniqueAbility/Decisions/IsUniqueAbilityActive")]
public class IsUniqueAbilityActive :Decision
{
    public override bool Decide(Controller controller)
    {
        return !controller.GetBehaviour<BaseUniqueAbility>().IsAbilityActive;
    }
}