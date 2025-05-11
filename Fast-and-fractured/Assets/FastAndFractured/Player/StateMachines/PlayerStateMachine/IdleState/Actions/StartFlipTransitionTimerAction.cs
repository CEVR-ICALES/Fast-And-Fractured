using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FastAndFractured;

namespace StateMachine {
    [CreateAssetMenu(fileName = nameof(StartFlipTransitionTimerAction), menuName = "PlayerStateMachine/Actions/StartFlipTransitionTimerAction")]
    public class StartFlipTransitionTimerAction : Action
    {
        public override void Act(Controller controller)
        {
            CarMovementController carMovementController = controller.GetBehaviour<CarMovementController>();
            PhysicsBehaviour physicsBehaviour = controller.GetBehaviour<PhysicsBehaviour>();
            if (carMovementController.IsInFlipCase()||physicsBehaviour.IsTouchingGround)
            {
               carMovementController.StartIsFlippedTimer();
            }
            else
            {
                carMovementController.StopFlippedTimer();
            }
        }
    }
}
