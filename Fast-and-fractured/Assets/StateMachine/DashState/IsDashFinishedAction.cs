using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = nameof(IsDashFinishedAction), menuName = "EnemyStateMachine/Actions/IsDashFinishedAction")]

public class IsDashFinishedAction : Action
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
