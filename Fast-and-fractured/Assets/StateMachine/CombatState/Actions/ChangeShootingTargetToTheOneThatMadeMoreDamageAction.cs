using System.Collections;
using System.Collections.Generic;
using FastAndFractured;
using StateMachine;
using UnityEngine;
[CreateAssetMenu(fileName = nameof(ChangeShootingTargetToTheOneThatMadeMoreDamageAction), menuName = "EnemyStateMachine/Actions/ChangeShootingTargetToTheOneThatMadeMoreDamageAction")]
public class ChangeShootingTargetToTheOneThatMadeMoreDamageAction : Action
{
   
    public override void Act(Controller controller)
    {
        EnemyAIBrain enemyAIBrain = controller.GetBehaviour<EnemyAIBrain>();
        enemyAIBrain.ChangeShootingTargetToTheOneThatMadeMoreDamage();
        FinishAction();
    }
}
