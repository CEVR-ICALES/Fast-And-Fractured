using UnityEngine;
using FastAndFractured;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(IsChickenThrowingEggDecision), menuName = "MegaChickenStateMachine/Decisions/IsChickenThrowingEggDecision")]
    public class IsChickenThrowingEggDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            return controller.GetBehaviour<ChickenBrain>().IsThrowingEgg;
        }
    }
}
