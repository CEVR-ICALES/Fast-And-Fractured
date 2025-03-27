using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.XInput;
using Utilities;
using Enums;

namespace FastAndFractured
{
    //public enum InputBlockTypes // this enum need to be added to the enum library
    //{
    //    ALL_MECHANICS,
    //    MOVEMENT_MECHANICS,
    //    SHOOTING_MECHANICS
    //}

    public class PlayerInputController : AbstractSingleton<PlayerInputController>
    {
        public delegate void InputDeviceChanged(InputDeviceType deviceType);
        public static event InputDeviceChanged OnInputDeviceChanged;

        PlayerInputAction inputActions;

        // Movement & Camera Inputs with private backing fields
        public Vector2 MoveInput => _moveInput;
        private Vector2 _moveInput;

        public Vector2 CameraInput => _cameraInput;
        private Vector2 _cameraInput;

        // Action Flags with private backing fields
        public float IsAccelerating => _isAccelerating;
        private float _isAccelerating;

        public bool IsBraking => _isBraking;
        private bool _isBraking;

        public float IsReversing => _isReversing;
        private float _isReversing;

        public bool IsShooting => _isShooting;
        private bool _isShooting;

        public bool IsPushShootMode { get { return _isPushShootMode; } set { _isPushShootMode = value; } }
        private bool _isPushShootMode = false;

        public bool IsPushShooting => _isPushShooting;
        private bool _isPushShooting;

        public bool IsUsingAbility => _isUsingAbility;
        private bool _isUsingAbility;

        public bool IsThrowingMine => _isThrowingMine;
        private bool _isThrowingMine;

        public bool IsPausing => _isPausing;
        private bool _isPausing;

        public bool IsResettingCamera => _isResettingCamera;
        private bool _isResettingCamera;

        public bool IsDashing => _isDashing;
        private bool _isDashing;

        public bool IsUsingController => _isUsingController;
        private bool _isUsingController;

        public bool IsAllMechanicsInputsBlocked => _isAllMechanicsInputsBlocked;
        private bool _isAllMechanicsInputsBlocked;

        public bool IsMovementInputsBlocked => _isMovementInputsBlocked;
        private bool _isMovementInputsBlocked;

        public bool IsShootingInputsBlocked => _isShootingInputsBlocked;
        private bool _isShootingInputsBlocked;

        public bool IsAbilityFinished => _isAbilityFinished;
        private bool _isAbilityFinished;

        private InputDeviceType _currentInputDevice = InputDeviceType.KEYBOARD_MOUSE;

        protected override void Awake()
        {
            base.Awake();
            inputActions = new PlayerInputAction();
        }

        private void OnEnable()
        {
            inputActions.Enable();

            // Movement Input
            inputActions.PlayerInputActions.Movement.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
            inputActions.PlayerInputActions.Movement.canceled += ctx => _moveInput = Vector2.zero;

            // Camera Input
            inputActions.PlayerInputActions.CameraMove.performed += ctx => _cameraInput = ctx.ReadValue<Vector2>();
            inputActions.PlayerInputActions.CameraMove.canceled += ctx => _cameraInput = Vector2.zero;

            // Action Inputs
            inputActions.PlayerInputActions.Accelerate.performed += ctx => _isAccelerating = ctx.ReadValue<float>();
            inputActions.PlayerInputActions.Accelerate.canceled += ctx => _isAccelerating = 0f;

            inputActions.PlayerInputActions.Reverse.performed += ctx => _isReversing = ctx.ReadValue<float>();
            inputActions.PlayerInputActions.Reverse.canceled += ctx => _isReversing = 0f;

            inputActions.PlayerInputActions.Brake.performed += ctx => _isBraking = true;
            inputActions.PlayerInputActions.Brake.canceled += ctx => _isBraking = false;

            inputActions.PlayerInputActions.ShootingMode.started += ctx => ChangeShootMode();

            inputActions.PlayerInputActions.Shoot.started += ctx => SetShootType();
            inputActions.PlayerInputActions.Shoot.canceled += ctx => UnsetShootType();

            inputActions.PlayerInputActions.SpecialAbility.performed += ctx => _isUsingAbility = true;
            inputActions.PlayerInputActions.SpecialAbility.canceled += ctx => _isUsingAbility = false;

            inputActions.PlayerInputActions.ThrowMine.performed += ctx => _isThrowingMine = true;
            inputActions.PlayerInputActions.ThrowMine.canceled += ctx => _isThrowingMine = false;

            inputActions.PlayerInputActions.Pause.performed += ctx => _isPausing = true;
            inputActions.PlayerInputActions.Pause.canceled += ctx => _isPausing = false;

            inputActions.PlayerInputActions.ResetCamera.performed += ctx => _isResettingCamera = true;
            inputActions.PlayerInputActions.ResetCamera.canceled += ctx => _isResettingCamera = false;

            inputActions.PlayerInputActions.Dash.performed += ctx => _isDashing = true;
            inputActions.PlayerInputActions.Dash.canceled += ctx => _isDashing = false;
        }

        private void OnDisable()
        {
            inputActions.Disable();
        }

        private void Update()
        {
            CheckForInputDeviceChange();
        }

        private void CheckForInputDeviceChange()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.isPressed)
            {
                _currentInputDevice = InputDeviceType.KEYBOARD_MOUSE;
                _isUsingController = false;
                OnInputDeviceChanged?.Invoke(_currentInputDevice);
            }

            if (Gamepad.current != null)
            {
                if (Gamepad.current is DualShockGamepad)
                {
                    _currentInputDevice = InputDeviceType.PS_CONTROLLER;
                    OnInputDeviceChanged?.Invoke(_currentInputDevice);
                }
                else if (Gamepad.current is XInputController)
                {
                    _currentInputDevice = InputDeviceType.XBOX_CONTROLLER;
                    OnInputDeviceChanged?.Invoke(_currentInputDevice);
                }

                _isUsingController = true;
            }
        }

        public void DisableInput()
        {
            inputActions.Disable();
        }

        public void EnableInput()
        {
            inputActions.Enable();
        }

        public void BlockInput(InputBlockTypes inputBlockType)
        {
            switch (inputBlockType)
            {
                case InputBlockTypes.ALL_MECHANICS:
                    ToggleMovementInputs(false);
                    ToggleShootingInputs(false);
                    _isAllMechanicsInputsBlocked = true;
                    break;

                case InputBlockTypes.MOVEMENT_MECHANICS:
                    ToggleMovementInputs(false);
                    _isMovementInputsBlocked = true;
                    break;

                case InputBlockTypes.SHOOTING_MECHANICS:
                    ToggleShootingInputs(false);
                    _isShootingInputsBlocked = true;
                    break;
            }
        }


        public void EnableInput(InputBlockTypes inputBlockType)
        {
            switch (inputBlockType)
            {
                case InputBlockTypes.ALL_MECHANICS:
                    ToggleMovementInputs(true);
                    ToggleShootingInputs(true);
                    _isAllMechanicsInputsBlocked = false;
                    break;

                case InputBlockTypes.MOVEMENT_MECHANICS:
                    ToggleMovementInputs(true);
                    _isMovementInputsBlocked = false;
                    break;

                case InputBlockTypes.SHOOTING_MECHANICS:
                    ToggleShootingInputs(true);
                    _isShootingInputsBlocked = false;
                    break;
            }
        }

        public void EnableInput(InputBlockTypes inputBlockType, float timeTillEnable)
        {
            if (_isUsingAbility)
            {
                _isAbilityFinished = false;
            }
            TimerSystem.Instance.CreateTimer(timeTillEnable, onTimerDecreaseComplete: () =>
            {
                if (!_isAbilityFinished)
                {
                    _isAbilityFinished = true;
                }
                EnableInput(inputBlockType);
            });

        }

        private void ToggleMovementInputs(bool enable)
        {
            if (enable)
            {
                inputActions.PlayerInputActions.Accelerate.Enable();
                inputActions.PlayerInputActions.Movement.Enable();
                inputActions.PlayerInputActions.Brake.Enable();
                inputActions.PlayerInputActions.Reverse.Enable();
            }
            else
            {
                inputActions.PlayerInputActions.Accelerate.Disable();
                inputActions.PlayerInputActions.Movement.Disable();
                inputActions.PlayerInputActions.Brake.Disable();
                inputActions.PlayerInputActions.Reverse.Disable();
            }
        }

        private void ToggleShootingInputs(bool enable)
        {
            if (enable)
            {
                inputActions.PlayerInputActions.Dash.Enable();
                inputActions.PlayerInputActions.Shoot.Enable();
                inputActions.PlayerInputActions.ShootingMode.Enable();
                inputActions.PlayerInputActions.ThrowMine.Enable();
                inputActions.PlayerInputActions.SpecialAbility.Enable();
            }
            else
            {
                inputActions.PlayerInputActions.Dash.Disable();
                inputActions.PlayerInputActions.Shoot.Disable();
                inputActions.PlayerInputActions.ShootingMode.Disable();
                inputActions.PlayerInputActions.ThrowMine.Disable();
                inputActions.PlayerInputActions.SpecialAbility.Disable();
            }
        }

        private void SetShootType()
        {
            if (_isPushShootMode)
            {
                _isPushShooting = true;
            }
            else
            {
                _isShooting = true;
            }
        }

        private void UnsetShootType()
        {
            if (_isPushShootMode)
            {
                _isPushShooting = false;
                _isPushShootMode = false;
            }
            else
            {
                _isShooting = false;
            }
        }

        private void ChangeShootMode()
        {
            if (!_isPushShootMode)
            {
                _isPushShootMode = true;
            }
            else
            {
                _isPushShootMode = false;
            }
        }

        public InputDeviceType GetCurrentInputDevice() => _currentInputDevice;
    }

}
