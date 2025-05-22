using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FastAndFractured;

namespace StateMachine {
    [CreateAssetMenu(fileName = nameof(StartFlipTransitionTimerAction), menuName = "PlayerStateMachine/Actions/StartFlipTransitionTimerAction")]
    public class StartFlipTransitionTimerAction : Action
    {
        private const float DECRESE_TIME_FACTOR_IF_TOUCHING_GROUND = 0.75f;
        public override void Act(Controller controller)
        {
            CarMovementController carMovementController = controller.GetBehaviour<CarMovementController>();
            PhysicsBehaviour physicsBehaviour = controller.GetBehaviour<PhysicsBehaviour>();
            if (carMovementController.IsInFlipCase()||physicsBehaviour.IsTouchingGround)
            {
               carMovementController.StartIsFlippedTimer(physicsBehaviour.IsTouchingGround ? DECRESE_TIME_FACTOR_IF_TOUCHING_GROUND : 1);
            }
            else
            {
                carMovementController.StopFlippedTimer();
            }
        }
    }
}
