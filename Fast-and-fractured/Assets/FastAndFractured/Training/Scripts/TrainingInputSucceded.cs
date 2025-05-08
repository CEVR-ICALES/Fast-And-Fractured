using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace FastAndFractured
{
    public enum InputMechanics
    {
        MOVING,
        MOVING_CAMERA,
        DASH,
        BRAKE,
        DRIFT,
        RECENTERING,
        NORMAL_SHOOT,
        PUSH_SHOOT,
        MINE_SHOOT,
        UNIQUE_ABILITY
    }
    public class TrainingInputSucceded : MonoBehaviour
    {
        private bool _inputPressed = false;
        public bool InputPressed {get=>_inputPressed;}
        private bool _isWaitingInput =  false;
        private Action _waitInputAction;

        [SerializeField]
        private InputMechanics inputMechanics;

        private void Start()
        {
            switch (inputMechanics)
            {
                case InputMechanics.MOVING:
                    _waitInputAction = () =>
                    {
                        SeeIfInputPressed(PlayerInputController.Instance.MoveInput!=Vector2.zero);
                    };
                    break;
                case InputMechanics.MOVING_CAMERA:
                    _waitInputAction = () =>
                    {
                        SeeIfInputPressed(PlayerInputController.Instance.CameraInput != Vector2.zero);
                    };
                    break;
                case InputMechanics.DASH:
                    _waitInputAction = () =>
                    {
                        SeeIfInputPressed(PlayerInputController.Instance.IsDashing);
                    };
                    break;
                case InputMechanics.BRAKE:
                    _waitInputAction = () =>
                    {
                        SeeIfInputPressed(PlayerInputController.Instance.IsBraking);
                    };
                    break;
                case InputMechanics.NORMAL_SHOOT:
                    _waitInputAction = () =>
                    {
                        SeeIfInputPressed(PlayerInputController.Instance.IsShooting);
                    };
                    break;
                case InputMechanics.DRIFT:
                    _waitInputAction = () =>
                    {
                        SeeIfInputPressed(PlayerInputController.Instance.IsBraking&&PlayerInputController.Instance.MoveInput.x!=0);
                    };
                    break;
                case InputMechanics.RECENTERING:
                    _waitInputAction = () =>
                    {
                        SeeIfInputPressed(PlayerInputController.Instance.IsResettingCamera);
                    };
                    break;
                case InputMechanics.PUSH_SHOOT:
                    _waitInputAction = () =>
                    {
                        SeeIfInputPressed(PlayerInputController.Instance.IsPushShooting);
                    };
                    break;
                case InputMechanics.MINE_SHOOT:
                    _waitInputAction = () =>
                    {
                        SeeIfInputPressed(PlayerInputController.Instance.IsThrowingMine);
                    }; break;
                case InputMechanics.UNIQUE_ABILITY:
                    _waitInputAction = () =>
                    {
                        SeeIfInputPressed(PlayerInputController.Instance.IsUsingAbility);
                    }; break;
            }

        }

        private void Update()
        {
            if (_isWaitingInput)
            {
                _waitInputAction();
            }
        }

        private void SeeIfInputPressed(bool input)
        {
            _inputPressed = input;
        }

        private void OnTriggerEnter(Collider other)
        {
            if(other.TryGetComponent(out StatsController character))
            {
                if (character.IsPlayer)
                {
                    _isWaitingInput = true;
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out StatsController character))
            {
                if (character.IsPlayer)
                {
                    _isWaitingInput = false;
                }
            }
        }
    }
}
