using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using Enums;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.XInput;
using Utilities;
using static UnityEngine.InputSystem.DefaultInputActions;

namespace FastAndFractured
{
    public class PlayerInputController : AbstractSingleton<PlayerInputController>
    {
        public delegate void InputDeviceChanged(InputDeviceType deviceType);
        public static event InputDeviceChanged OnInputDeviceChanged;

        public PlayerInputAction InputActions { get => _inputActions; }
        private PlayerInputAction _inputActions;
        

        // Movement & Camera Inputs with private backing fields
        public Vector2 MoveInput => _moveInput;
        private Vector2 _moveInput = Vector2.zero;

        public Vector2 CameraInput => _cameraInput;
        private Vector2 _cameraInput = Vector2.zero;

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

        public bool IsResettingCamera { get { return _isResettingCamera; } set { _isResettingCamera = value; } }
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
            LevelController.Instance.charactersCustomStart.AddListener(BindActions);
        }

        private void BindActions()
        {
            _inputActions = new PlayerInputAction();

            _inputActions.Enable();

            // Movement Input
            _inputActions.PlayerInputActions.Movement.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
            _inputActions.PlayerInputActions.Movement.canceled += ctx => _moveInput = Vector2.zero;

            // Camera Input
            _inputActions.PlayerInputActions.CameraMove.performed += ctx => _cameraInput = ctx.ReadValue<Vector2>();
            _inputActions.PlayerInputActions.CameraMove.canceled += ctx => _cameraInput = Vector2.zero;

            // Action Inputs
            _inputActions.PlayerInputActions.Accelerate.performed += ctx => _isAccelerating = ctx.ReadValue<float>();
            _inputActions.PlayerInputActions.Accelerate.canceled += ctx => _isAccelerating = 0f;

            _inputActions.PlayerInputActions.Reverse.performed += ctx => _isReversing = ctx.ReadValue<float>();
            _inputActions.PlayerInputActions.Reverse.canceled += ctx => _isReversing = 0f;

            _inputActions.PlayerInputActions.Brake.performed += ctx => _isBraking = true;
            _inputActions.PlayerInputActions.Brake.canceled += ctx => _isBraking = false;

            _inputActions.PlayerInputActions.ShootingMode.started += ctx => ChangeShootMode();

            _inputActions.PlayerInputActions.Shoot.started += ctx => SetShootType();
            _inputActions.PlayerInputActions.Shoot.canceled += ctx => UnsetShootType();

            _inputActions.PlayerInputActions.SpecialAbility.performed += ctx => _isUsingAbility = true;
            _inputActions.PlayerInputActions.SpecialAbility.canceled += ctx => _isUsingAbility = false;

            _inputActions.PlayerInputActions.ThrowMine.started += ctx => ToggleBoolForSpecifiedTime(b => _isThrowingMine = b, false, 1f);
            _inputActions.PlayerInputActions.ThrowMine.canceled += ctx => _isThrowingMine = false;

            _inputActions.PlayerInputActions.Pause.started += ctx => _isPausing = true;
            _inputActions.PlayerInputActions.Pause.canceled += ctx => _isPausing = false;

            _inputActions.PlayerInputActions.ResetCamera.started += ctx => CameraBehaviours.Instance.ResetCameraPosition();

            _inputActions.PlayerInputActions.Dash.performed += ctx => _isDashing = true;
            _inputActions.PlayerInputActions.Dash.canceled += ctx => _isDashing = false;

            LoadInputSettings();
        }

        private void OnEnable()
        {
            if (_inputActions != null)
            {
                _inputActions.Enable();
            }
        }

        private void OnDisable()
        {
            if (_inputActions != null)
            {
                _inputActions.Disable();
            }
        }

        private void Start()
        {
        }

        private void Update()
        {
            CheckForInputDeviceChange();
        }

        private void LoadInputSettings()
        {
            var rebinds = PlayerPrefs.GetString("rebinds");
            if (!string.IsNullOrEmpty(rebinds))
                _inputActions.LoadBindingOverridesFromJson(rebinds);
        }

        private void CheckForInputDeviceChange()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.isPressed)
            {
                _currentInputDevice = InputDeviceType.KEYBOARD_MOUSE;
                _isUsingController = false;
                OnInputDeviceChanged?.Invoke(_currentInputDevice);
                Debug.Log("KEYBOARD DETECTED");
            }

            bool gamepadButtonPressed = false;
            if(Gamepad.current != null){
                gamepadButtonPressed = Gamepad.current.allControls.Any(x => x is ButtonControl button && x.IsPressed() && !x.synthetic);
            }
            
            if (gamepadButtonPressed)
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
                    Debug.Log("CONTROLLER DETECTED");

                    _isUsingController = true;
            }
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
                _inputActions.PlayerInputActions.Accelerate.Enable();
                _inputActions.PlayerInputActions.Movement.Enable();
                _inputActions.PlayerInputActions.Brake.Enable();
                _inputActions.PlayerInputActions.Reverse.Enable();
            }
            else
            {
                _inputActions.PlayerInputActions.Accelerate.Disable();
                _inputActions.PlayerInputActions.Movement.Disable();
                _inputActions.PlayerInputActions.Brake.Disable();
                _inputActions.PlayerInputActions.Reverse.Disable();
            }
        }

        private void ToggleShootingInputs(bool enable)
        {
            if (enable)
            {
                _inputActions.PlayerInputActions.Dash.Enable();
                //_inputActions.PlayerInputActions.Shoot.Enable();
                _inputActions.PlayerInputActions.ShootingMode.Enable();
                _inputActions.PlayerInputActions.ThrowMine.Enable();
                _inputActions.PlayerInputActions.SpecialAbility.Enable();
            }
            else
            {
                _inputActions.PlayerInputActions.Dash.Disable();
                //_inputActions.PlayerInputActions.Shoot.Disable();
                _inputActions.PlayerInputActions.ShootingMode.Disable();
                _inputActions.PlayerInputActions.ThrowMine.Disable();
                _inputActions.PlayerInputActions.SpecialAbility.Disable();
            }
        }

        private void ToggleBoolForSpecifiedTime(Action<bool> setBoolAction, bool boolResult, float timeTillReset)
        {
            setBoolAction(!boolResult);
            TimerSystem.Instance.CreateTimer(timeTillReset, onTimerDecreaseComplete: () =>
            {
                setBoolAction(boolResult);
            });
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
                _isThrowingMine = false;
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
