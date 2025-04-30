using FastAndFractured;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(MakeDashDecision), menuName = "EnemyStateMachine/Decisions/MakeDashDecision")]
    public class MakeDashDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();
            return brain.IsDashReady();
        }
    }
}