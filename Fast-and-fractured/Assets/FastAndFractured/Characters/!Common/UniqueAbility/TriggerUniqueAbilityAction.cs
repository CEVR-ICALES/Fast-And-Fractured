using System.Collections;
using System.Collections.Generic;
using Game;
using StateMachine;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(TriggerUniqueAbilityAction), menuName = "PlayerStateMachine/Actions/TriggerUniqueAbilityAction")]
public class TriggerUniqueAbilityAction : Action
{
    public override void Act(Controller controller)
    {
        controller.GetBehaviour<BaseUniqueAbility>().ActivateAbility();
    }
}
