using FastAndFractured;
using UnityEngine;
using Enums;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "UnblockInputAction", menuName = "PlayerStateMachine/Actions/UnblockInputAction")]
    public class UnblockInputAction : Action
    {
        public InputBlockTypes InputBlockType;
        public override void Act(Controller controller)
        {
            controller.GetBehaviour<PlayerInputController>().EnableInput(InputBlockType);
        }
    }
}
