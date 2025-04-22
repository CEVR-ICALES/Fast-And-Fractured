using System.Collections;
using System.Collections.Generic;
using FastAndFractured;
using UnityEngine;
namespace StateMachine {
    [CreateAssetMenu(fileName = nameof(IsInsideSandstormDecision), menuName = "EnemyStateMachine/Decisions/IsInsideSandstormDecision")]
    public class IsInsideSandstormDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            return controller.GetBehaviour<EnemyAIBrain>().IsIAInsideSandstorm();
        }
    }
}
