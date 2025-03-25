using FastAndFractured;
using StateMachine;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(TriggerUniqueAbilityAction), menuName = "UniqueAbility/Actions/TriggerUniqueAbilityAction")]
public class TriggerUniqueAbilityAction : Action
{
    public override void Act(Controller controller)
    {
        controller.GetBehaviour<BaseUniqueAbility>().ActivateAbility();
    }
}
