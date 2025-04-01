using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FastAndFractured;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(IsInFrontDecision), menuName = "EnemyStateMachine/Decisions/IsInFrontDecision")]
    public class IsInFrontDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();
            return brain.IsInFront();
        }
    }
}