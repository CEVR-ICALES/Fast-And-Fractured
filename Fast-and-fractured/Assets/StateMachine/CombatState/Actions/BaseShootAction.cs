using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;
[CreateAssetMenu(fileName = "BaseShoot", menuName = "EnemyStateMachine/Actions/BaseShoot")]
public class BaseShootAction : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        brain.NormalShoot();
    }

}
