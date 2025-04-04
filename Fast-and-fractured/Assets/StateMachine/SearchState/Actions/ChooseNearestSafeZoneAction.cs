using FastAndFractured;
using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(ChooseNearestSafeZoneAction),
    menuName = "EnemyStateMachine/Actions/ChooseNearestSafeZoneAction")]
public class ChooseNearestSafeZoneAction : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        brain.ChooseNearestSafeZone();
    }

}
