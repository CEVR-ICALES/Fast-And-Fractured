using StateMachine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = nameof(IsDashFinishedAction), menuName = "EnemyStateMachine/Actions/IsDashFinishedAction")]
[Obsolete("Use the decision instead")]
public class IsDashFinishedAction : StateMachine.Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        if (brain.IsDashFinished())
        {
            FinishAction();
        }
    }
}
