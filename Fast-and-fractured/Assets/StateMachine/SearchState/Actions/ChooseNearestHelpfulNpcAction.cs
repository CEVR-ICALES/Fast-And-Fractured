using FastAndFractured;
using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(ChooseNearestHelpfulNpcAction),
    menuName = "EnemyStateMachine/Actions/ChooseNearestHelpfulNpcAction")]
public class ChooseNearestHelpfulNpcAction : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        brain.ChooseNearestHelpfulNpc();
    }
}
