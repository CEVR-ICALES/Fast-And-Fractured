using UnityEngine;

namespace FastAndFractured { 
 
    public class PlayerInputProvider : MonoBehaviour, IInputProvider
    { 
        private PlayerInputController _input => PlayerInputController.Instance;

        public Vector2 MoveInput
        {
            get
            {
                if (_input == null) return Vector2.zero;

                if (_input.IsUsingController)
                {
                    float moveY = _input.IsAccelerating - _input.IsReversing;
                    return new Vector2(_input.MoveInput.x, moveY);
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