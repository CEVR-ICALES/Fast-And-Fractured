using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.XInput;

public enum InputBlockTypes // this enum need to be added to the enum library
{
    ALL_MECHANICS,
    MOVEMENT_MECHANICS,
    SHOOTING_MECHANICS
}

public class PlayerInputController : MonoBehaviour
{
    public static PlayerInputController Instance { get; private set; }

    public delegate void InputDeviceChanged(INPUT_DEVICE_TYPE deviceType);
    public static event InputDeviceChanged OnInputDeviceChanged;

    PlayerInputAction inputActions;

    // Movement & Camera Inputs with private backing fields
    public Vector2 MoveInput => _moveInput;
    private Vector2 _moveInput;

    public Vector2 CameraInput => _cameraInput;
    private Vector2 _cameraInput;

    // Action Flags with private backing fields
    public bool IsAccelerating => _isAccelerating;
    private bool _isAccelerating;

    public bool IsBraking => _isBraking;
    private bool _isBraking;

    public bool IsReversing => _isReversing;
    private bool _isReversing;

    public bool IsShooting => _isShooting;
    private bool _isShooting;

    public bool IsAimingPushShoot => _isAimingPushShoot;
    private bool _isAimingPushShoot;

    public bool IsPushShootReleased => _isPushShootReleased;
    private bool _isPushShootReleased;

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

    private INPUT_DEVICE_TYPE _currentInputDevice = INPUT_DEVICE_TYPE.KeyboardMouse;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

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
        inputActions.PlayerInputActions.Accelerate.performed += ctx => _isAccelerating = true;
        inputActions.PlayerInputActions.Accelerate.canceled += ctx => _isAccelerating = false;

        inputActions.PlayerInputActions.Reverse.performed += ctx => _isReversing = true;
        inputActions.PlayerInputActions.Reverse.canceled += ctx => _isReversing = false;

        inputActions.PlayerInputActions.Brake.performed += ctx => _isBraking = true;
        inputActions.PlayerInputActions.Brake.canceled += ctx => _isBraking = false;

        inputActions.PlayerInputActions.RegularShoot.performed += ctx => _isShooting = true;
        inputActions.PlayerInputActions.RegularShoot.canceled += ctx => _isShooting = false;

        inputActions.PlayerInputActions.PushShoot.performed += ctx => OnStartAimingPushShoot();
        inputActions.PlayerInputActions.PushShoot.canceled += ctx => OnReleasedPushShoot();

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
            _currentInputDevice = INPUT_DEVICE_TYPE.KeyboardMouse;
            _isUsingController = false;
            OnInputDeviceChanged?.Invoke(_currentInputDevice);
        }

        if (Gamepad.current != null)
        {
            if (Gamepad.current is DualShockGamepad)
            {
                _currentInputDevice = INPUT_DEVICE_TYPE.PSController;
                OnInputDeviceChanged?.Invoke(_currentInputDevice);
            }
            else if (Gamepad.current is XInputController)
            {
                _currentInputDevice = INPUT_DEVICE_TYPE.XboxController;
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
                _isAllMechanicsInputsBlocked = true;
                break;

            case InputBlockTypes.MOVEMENT_MECHANICS:
                _isMovementInputsBlocked = true;
                break;

            case InputBlockTypes.SHOOTING_MECHANICS:
                _isShootingInputsBlocked = true;
                break;
        }
    }
    
    public void EnableInput(InputBlockTypes inputBlockType)
    {
        switch (inputBlockType)
        {
            case InputBlockTypes.ALL_MECHANICS:
                _isAllMechanicsInputsBlocked = false;
                break;

            case InputBlockTypes.MOVEMENT_MECHANICS:
                _isMovementInputsBlocked = false;
                break;

            case InputBlockTypes.SHOOTING_MECHANICS:
                _isShootingInputsBlocked = false;
                break;
        }
    }

    private void OnStartAimingPushShoot()
    {
        _isAimingPushShoot = true;
        _isPushShootReleased = false;
    }

    private void OnReleasedPushShoot()
    {
        _isAimingPushShoot = false;
        _isPushShootReleased = true;
    }

    public INPUT_DEVICE_TYPE GetCurrentInputDevice() => _currentInputDevice;
}
