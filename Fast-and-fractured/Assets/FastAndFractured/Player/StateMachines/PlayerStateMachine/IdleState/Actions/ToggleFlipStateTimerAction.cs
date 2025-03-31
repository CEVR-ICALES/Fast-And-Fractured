using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FastAndFractured;

namespace StateMachine {
    [CreateAssetMenu(fileName = nameof(ToggleFlipStateTimerAction), menuName = "PlayerStateMachine/Actions/ToggleFlipStateTimerAction")]
    public class ToggleFlipStateTimerAction : Action
    {
        bool _enterTimer = true;
        bool _exitTimer = true;
        public override void Act(Controller controller)
        {
            CarMovementController carMovementController = controller.GetBehaviour<CarMovementController>();
            if (carMovementController.IsInWall())
            {
                if (_enterTimer)
                {
                    carMovementController.StartIsFlippedTimer();
                    _enterTimer = false;
                    _exitTimer = true;
                }
            }
            else
            {
                if (_exitTimer)
                {
                    carMovementController.StopFlippedTimer();
                    _enterTimer = true;
                    _exitTimer = false;
                }
            }
        }
    }
}
