using FastAndFractured;
using UnityEngine;
using Utilities;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "CheckIfLayingEggEnded", menuName = "MegaChickenStateMachine/Actions/CheckIfLayingEggEnded")]
    public class CheckIfLayingEggEnded : Action
    {
        public override void Act(Controller controller)
        {
            controller.GetBehaviour<ChickenBrain>().CheckIfLayingEggEnded();
        }
    }
}
