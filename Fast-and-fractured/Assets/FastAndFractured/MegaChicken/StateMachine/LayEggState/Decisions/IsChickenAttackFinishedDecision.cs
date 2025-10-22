using UnityEngine;
using FastAndFractured;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(IsChickenAttackFinishedDecision), menuName = "MegaChickenStateMachine/Decisions/IsChickenAttackFinishedDecision")]
    public class IsChickenAttackFinishedDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            return controller.GetBehaviour<ChickenBrain>().IsIdle;
        }
    }
}
