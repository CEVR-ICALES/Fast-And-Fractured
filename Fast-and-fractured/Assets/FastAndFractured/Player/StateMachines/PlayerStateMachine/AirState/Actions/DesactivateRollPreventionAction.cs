using System;
using System.Collections;
using System.Collections.Generic;
using FastAndFractured;
using UnityEngine;
namespace StateMachine
{
    [CreateAssetMenu(fileName = "DesactivateRollPreventionAction", menuName = "PlayerStateMachine/Actions/DesactivateRollPreventionAction")]
    public class DesactivateRollPreventionAction : Action
    {
        public override void Act(Controller controller)
        {
            controller.GetBehaviour<ApplyForceByState>().ToggleRollPrevention(false, controller.GetBehaviour<PlayerInputController>().MoveInput.magnitude);
        }
    }
}
