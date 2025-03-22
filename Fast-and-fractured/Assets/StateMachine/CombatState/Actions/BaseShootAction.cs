using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;
[CreateAssetMenu(fileName = nameof(BaseShootAction), menuName = "EnemyStateMachine/Actions/BaseShootAction")]
public class BaseShootAction : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        brain.NormalShoot();
    }

}
