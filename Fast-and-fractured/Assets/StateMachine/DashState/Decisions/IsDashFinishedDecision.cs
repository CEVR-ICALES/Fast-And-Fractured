using FastAndFractured;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(IsDashFinishedDecision), menuName = "EnemyStateMachine/Decisions/IsDashFinishedDecision")]
    public class IsDashFinishedDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();
            return brain.IsDashFinished();
        }
    }
}
