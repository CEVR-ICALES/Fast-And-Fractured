using UnityEngine;
using FastAndFractured;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(IsChickenLayingEggDecision), menuName = "MegaChickenStateMachine/Decisions/IsChickenLayingEggDecision")]
    public class IsChickenLayingEggDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            return controller.GetBehaviour<ChickenBrain>().IsLayingEgg;
        }
    }
}
