using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FastAndFractured;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "ApplyAirRotation", menuName = "PlayerStateMachine/Actions/ApplyAirRotation")]
    public class ApplyAirRotationAction : Action
    {
        private bool _isAirRotationToggled = false;
        private bool _wasBrakingLastFrame = true;
        PlayerInputController _playerInputController;
        public override void Act(Controller controller)
        {
            Debug.Log(_isAirRotationToggled);
            _playerInputController = controller.GetBehaviour<PlayerInputController>();
            HandleIsBrakingInput(_playerInputController.IsBraking);
            
            if(_isAirRotationToggled)
            {
                controller.GetBehaviour<CarMovementController>().HandleInputOnAir(_playerInputController.MoveInput);
            }
           
        }

        private void HandleIsBrakingInput(bool isBraking)
        {
            if(!_wasBrakingLastFrame && isBraking == true)
            {
                _isAirRotationToggled = !_isAirRotationToggled;
            }

            _wasBrakingLastFrame = isBraking;

        }
    }
}

