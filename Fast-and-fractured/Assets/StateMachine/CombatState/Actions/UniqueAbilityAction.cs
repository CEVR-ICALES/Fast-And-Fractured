using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;
[CreateAssetMenu(fileName = "UniqueAbility", menuName = "EnemyStateMachine/Actions/UniqueAbility")]
public class UniqueAbilityAction : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        brain.NormalShoot();
    }

}
