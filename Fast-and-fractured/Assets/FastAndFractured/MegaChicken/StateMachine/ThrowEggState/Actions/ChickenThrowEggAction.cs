using FastAndFractured;
using UnityEngine;
using Utilities;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "ChickenThrowEggAction", menuName = "MegaChickenStateMachine/Actions/ChickenThrowEggAction")]
    public class ChickenThrowEggAction : Action
    {
        public override void Act(Controller controller)
        {
            controller.GetBehaviour<ChickenBrain>().StartThrowing();
        }
    }
}
