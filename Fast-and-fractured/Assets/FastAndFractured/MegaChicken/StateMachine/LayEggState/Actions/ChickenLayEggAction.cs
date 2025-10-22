using FastAndFractured;
using UnityEngine;
using Utilities;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "ChickenLayEggAction", menuName = "MegaChickenStateMachine/Actions/ChickenLayEggAction")]
    public class ChickenLayEggAction : Action
    {
        public override void Act(Controller controller)
        {
            controller.GetBehaviour<ChickenBrain>().ThrowEgg();
        }
    }
}
