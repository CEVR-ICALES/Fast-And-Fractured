using UnityEngine;
using Utilities;

namespace FastAndFractured
{
    public class PlayerInputProvider : MonoBehaviour, IInputProvider
    {
        private const float CONTROLLER_MAX_VALUE_THRESHOLD = 0.4f;
        private const float MAX_AXIS_VALUE = 1f;
        private const float AXIS_VECTOR_MAGNITUDE = 1f;

        private PlayerInputController _input => PlayerInputController.Instance;
        private CameraHolder _cameraHolder;

        public Vector2 MoveInput
        {
            get
            {
                if (_input == null) return Vector2.zero;
                if (!_input.IsUsingController) return _input.MoveInput;

                float moveInputX = _input.MoveInput.x >= CONTROLLER_MAX_VALUE_THRESHOLD ? MAX_AXIS_VALUE : _input.MoveInput.x;
                float acceleration = _input.IsAccelerating >= CONTROLLER_MAX_VALUE_THRESHOLD ? MAX_AXIS_VALUE : _input.IsAccelerating;
                float reversing = _input.IsReversing >= CONTROLLER_MAX_VALUE_THRESHOLD ? MAX_AXIS_VALUE : _input.IsReversing;
                float moveY = acceleration - reversing;
                return Vector2.ClampMagnitude(new Vector2(moveInputX, moveY), AXIS_VECTOR_MAGNITUDE);
            }
        }

        public bool IsBraking => _input != null && _input.IsBraking;
        public bool IsDashing => _input != null && _input.IsDashing;
        public float IsAccelerating => _input != null ? _input.IsAccelerating : 0f;
        public float IsReversing => _input != null ? _input.IsReversing : 0f;
        public Vector3 AimDirection => _cameraHolder?.CameraToHold.transform.forward ?? transform.forward;
        public bool IsNormalShooting => _input?.IsShooting ?? false;
        public bool IsPushShooting => _input?.IsPushShootMode ?? false;
        public bool FirePushAction => _input?.IsPushShooting ?? false;
        public bool IsThrowingMine => _input?.IsThrowingMine ?? false;

        public void Initialize()
        {
            _input.BindActions();
            _cameraHolder = GetComponentInParent<CameraHolder>();
        }
    }
}
