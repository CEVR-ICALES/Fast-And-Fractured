using FastAndFractured;
using UnityEngine;
using Utilities;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "RotateChickenAction", menuName = "MegaChickenStateMachine/Actions/RotateChickenAction")]
    public class RotateChickenAction : Action
    {
        public override void Act(Controller controller)
        {
            controller.GetBehaviour<ChickenBrain>().RotateChicken();
        }
    }
}
