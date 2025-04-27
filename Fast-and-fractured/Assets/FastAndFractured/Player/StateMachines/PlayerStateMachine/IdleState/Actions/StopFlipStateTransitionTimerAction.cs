using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FastAndFractured;

namespace StateMachine {
    [CreateAssetMenu(fileName = nameof(StopFlipStateTransitionTimerAction), menuName = "PlayerStateMachine/Actions/StopFlipStateTransitionTimerAction")]
    public class StopFlipStateTransitionTimerAction : Action
    {
        public override void Act(Controller controller)
        {
            CarMovementController carMovementController = controller.GetBehaviour<CarMovementController>();
            if (!carMovementController.IsFlipped)
            {
                controller.GetBehaviour<CarMovementController>().StopFlippedTimer();
            }
        }
    }
}
