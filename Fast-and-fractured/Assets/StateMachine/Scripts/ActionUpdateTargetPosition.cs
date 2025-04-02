using FastAndFractured;
using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(ActionUpdateTargetPosition), menuName = "StateMachine/ActionUpdateTargetPosition")]
public class ActionUpdateTargetPosition : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        brain.UpdateTargetPosition();
    }
}
