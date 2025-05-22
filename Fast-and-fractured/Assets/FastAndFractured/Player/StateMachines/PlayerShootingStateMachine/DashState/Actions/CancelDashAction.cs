using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;
using FastAndFractured;
    [CreateAssetMenu(fileName = nameof(CancelDashAction), menuName = "PlayerShootingStateMachine/Actions/CancelDashAction")]
public class CancelDashAction : Action
{
    public override void Act(Controller controller)
    {
        PlayerInputController playerInput = controller.GetBehaviour<PlayerInputController>();
        CarMovementController movement = controller.GetBehaviour<CarMovementController>();
        if (playerInput.IsBraking)
        {
            movement.CancelDash();
            FinishAction();
        }
    }

}
