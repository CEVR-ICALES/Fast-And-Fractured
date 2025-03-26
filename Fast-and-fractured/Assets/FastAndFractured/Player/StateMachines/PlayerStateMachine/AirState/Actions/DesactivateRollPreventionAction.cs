using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FastAndFractured;
namespace StateMachine {
    [CreateAssetMenu(fileName = "DesactiveRollPrevention", menuName = "PlayerStateMachine/Actions/ApplyRollPreventionAction")]
    public class DesativateRollPreventionAction : Action
    {
        public override void Act(Controller controller)
        {
            controller.GetBehaviour<RollPrevention>().ToggleRollPrevention(false, controller.GetBehaviour<PhysicsBehaviour>().Rb, controller.GetBehaviour<PlayerInputController>().MoveInput.magnitude);
        }
    }
}
