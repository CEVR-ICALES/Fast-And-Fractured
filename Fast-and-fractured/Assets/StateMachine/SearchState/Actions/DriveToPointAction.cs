using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FastAndFractured;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(DriveToPointAction), menuName = "EnemyStateMachine/Actions/DriveToPointAction")]
    public class DriveToPointAction : Action
    {
        public override void Act(Controller controller)
        {
            EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

            brain.GoToPosition();
        }
    }
}

