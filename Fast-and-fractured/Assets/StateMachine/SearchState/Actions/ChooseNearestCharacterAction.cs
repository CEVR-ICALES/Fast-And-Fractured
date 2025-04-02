using FastAndFractured;
using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(ChooseNearestCharacterAction), 
    menuName = "EnemyStateMachine/Actions/ChooseNearestCharacterAction")]
public class ChooseNearestCharacterAction : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        brain.ChooseNearestCharacter();
    }
}
