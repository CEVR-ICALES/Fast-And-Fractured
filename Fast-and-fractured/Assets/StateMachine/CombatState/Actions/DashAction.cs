using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;
[CreateAssetMenu(fileName = nameof(BaseShootAction), menuName = "EnemyStateMachine/Actions/DashAction")]
public class DashAction : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        brain.Dash();
    }

}
