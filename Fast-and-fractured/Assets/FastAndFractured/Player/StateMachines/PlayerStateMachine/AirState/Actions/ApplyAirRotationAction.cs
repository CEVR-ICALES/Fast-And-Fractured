using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FastAndFractured;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "ApplyAirRotationAction", menuName = "PlayerStateMachine/Actions/ApplyAirRotation")]
    public class ApplyAirRotationAction : Action
    {
        // Per car: this action is a shared ScriptableObject, so state kept in plain fields was
        // shared by every car running the state machine.
        private class ToggleState
        {
            public bool IsAirRotationToggled;
            public bool WasBrakingLastFrame = true;
        }
        private readonly Dictionary<Controller, ToggleState> _states = new Dictionary<Controller, ToggleState>();

        public override void Act(Controller controller)
        {
            if (!_states.TryGetValue(controller, out ToggleState state))
            {
                state = new ToggleState();
                _states[controller] = state;
            }

            // The input actually driving this car, not the local PlayerInputController (see
            // ApplyBrakeAction).
            CarMovementController carMovement = controller.GetBehaviour<CarMovementController>();
            bool isBraking = carMovement.InputProvider != null && carMovement.InputProvider.IsBraking;

            if (!state.WasBrakingLastFrame && isBraking)
                state.IsAirRotationToggled = !state.IsAirRotationToggled;
            state.WasBrakingLastFrame = isBraking;

            if (state.IsAirRotationToggled)
                carMovement.ProcessAirRotationInput();
        }
    }
}

