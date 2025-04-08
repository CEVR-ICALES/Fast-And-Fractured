using FastAndFractured;
using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(ChooseNearestDangerZoneAction),
    menuName = "EnemyStateMachine/Actions/ChooseNearestDangerZoneAction")]
public class ChooseNearestDangerZoneAction : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        brain.ChooseNearestCharacter();
    }
}
