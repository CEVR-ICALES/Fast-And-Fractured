using System.Collections;
using System.Collections.Generic;
using FastAndFractured;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(AllInteractablesAreNotInsideSandstormDecision), menuName = "EnemyStateMachine/Decisions/AllInteractablesAreNotInsideSandstormDecision")]
    public class AllInteractablesAreNotInsideSandstormDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            return!controller.GetBehaviour<EnemyAIBrain>().AreAllInteractablesInsideSandstorm();
        }
    }
}
