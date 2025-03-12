using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;
[CreateAssetMenu(fileName = nameof(PushShootAction), menuName = "EnemyStateMachine/Actions/PushShootAction")]
public class PushShootAction : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        brain.PushShoot();
    }

}
