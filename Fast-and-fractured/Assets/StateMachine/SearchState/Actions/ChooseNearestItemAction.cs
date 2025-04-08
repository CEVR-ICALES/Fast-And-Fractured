using FastAndFractured;
using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(ChooseNearestItemAction), menuName = "EnemyStateMachine/Actions/ChooseNearestItemAction")]
public class ChooseNearestItemAction : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        brain.ChooseNearestItem();
    }

}
