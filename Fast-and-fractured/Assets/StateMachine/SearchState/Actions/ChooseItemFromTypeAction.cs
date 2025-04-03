using Enums;
using FastAndFractured;
using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(ChooseItemFromTypeAction), menuName = "EnemyStateMachine/Actions/ChooseItemFromTypeAction")]
public class ChooseItemFromTypeAction : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        brain.ChooseItemFromType();
    }
}
