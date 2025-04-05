using FastAndFractured;
using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(GetClosestItemAwayFromTargetAction), menuName = "EnemyStateMachine/Actions/GetClosestItemAwayFromTargetAction")]
public class GetClosestItemAwayFromTargetAction : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        brain.ChooseNearestItemAwayFromTarget();
    }

}
