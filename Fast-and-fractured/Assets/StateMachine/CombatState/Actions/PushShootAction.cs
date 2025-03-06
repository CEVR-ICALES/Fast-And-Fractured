using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;
[CreateAssetMenu(fileName = "PushShoot", menuName = "EnemyStateMachine/Actions/PushShoot")]
public class PushShootAction : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        brain.UseUniqueAbility();
    }

}
