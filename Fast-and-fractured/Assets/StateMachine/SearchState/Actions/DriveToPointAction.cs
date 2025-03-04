using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;

[CreateAssetMenu(fileName = "DriveToPoint", menuName = "EnemyStateMachine/Actions/DriveToPoint")]

public class DriveToPointAction : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        brain.GoToPosition();
    }
}
