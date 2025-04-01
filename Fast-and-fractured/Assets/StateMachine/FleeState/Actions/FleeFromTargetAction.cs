using FastAndFractured;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(FleeFromTargetAction), menuName = "EnemyStateMachine/Actions/FleeFromTargetAction")]
    public class FleeFromTargetAction : Action
    {
        public override void Act(Controller controller)
        {
            EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();
            brain.RunAwayFromCurrentTarget();
        }
    }
}