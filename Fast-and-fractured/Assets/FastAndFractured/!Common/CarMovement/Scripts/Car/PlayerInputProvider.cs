    using UnityEngine;
using Utilities;

namespace FastAndFractured { 
 
        public class PlayerInputProvider : MonoBehaviour, IInputProvider
        { 
            private PlayerInputController _input => PlayerInputController.Instance;
        private CameraHolder _cameraHolder;

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
