using UnityEngine;

namespace FastAndFractured { 
 
    public class PlayerInputProvider : MonoBehaviour, IInputProvider
    { 
        private const float CONTROLLER_MAX_VALUE_THRESHOLD = 0.4f;

        private const float MAX_AXIS_VALUE = 1f;

        private const float AXIS_VECTOR_MAGNITUDE = 1f;

        private PlayerInputController _input => PlayerInputController.Instance;

        public Vector2 MoveInput
        {
            get
            {
                if (_input == null) return Vector2.zero;
                if (_input.IsUsingController)
                {
                    float moveInputXThreshold = _input.MoveInput.x>=CONTROLLER_MAX_VALUE_THRESHOLD ? MAX_AXIS_VALUE : _input.MoveInput.x;
                    float accelerationThreshold = _input.IsAccelerating>=CONTROLLER_MAX_VALUE_THRESHOLD ? MAX_AXIS_VALUE : _input.IsAccelerating;
                    float reversingThreshold = _input.IsReversing>= CONTROLLER_MAX_VALUE_THRESHOLD ? MAX_AXIS_VALUE : _input.IsReversing;
                    float moveY = accelerationThreshold - reversingThreshold;
                    Vector2 moveInput = new Vector2(moveInputXThreshold, moveY);
                    Debug.Log("MovementVectorWIthoutClamp: " + moveInput);
                    Vector2 moveInputClamped = Vector2.ClampMagnitude(moveInput, AXIS_VECTOR_MAGNITUDE);
                    Debug.Log("MovementVector: " + moveInputClamped);
                    return moveInputClamped;
                }
                else
                {
                    return _input.MoveInput;
                }
            }
        }

        public bool IsBraking => _input != null && _input.IsBraking;
        public bool IsDashing => _input != null && _input.IsDashing;
    }
}