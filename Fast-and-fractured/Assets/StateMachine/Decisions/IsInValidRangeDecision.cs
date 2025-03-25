using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FastAndFractured;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(IsInValidRangeDecision), menuName = "EnemyStateMachine/Decisions/IsInValidRangeDecision")]

    public class IsInValidRangeDecision : Decision
    {
        [SerializeField] float distanceToCompare = 10f;
        public override bool Decide(Controller controller)
        {
            EnemyAIBrain brain = controller.GetComponent<EnemyAIBrain>();

            return brain.IsInValidRange(distanceToCompare);
        }
    }
}