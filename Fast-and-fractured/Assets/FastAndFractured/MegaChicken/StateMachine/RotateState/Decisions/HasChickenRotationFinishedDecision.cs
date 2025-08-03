using UnityEngine;
using FastAndFractured;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(HasChickenRotationFinishedDecision), menuName = "MegaChickenStateMachine/Decisions/HasChickenRotationFinishedDecision")]
    public class HasChickenRotationFinishedDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            return controller.GetBehaviour<ChickenBrain>().IsIdle;
        }
    }
}
