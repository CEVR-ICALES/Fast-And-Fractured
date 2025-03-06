using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.XInput;

public class PlayerInputController : MonoBehaviour
{
    public static PlayerInputController Instance { get; private set; }

    public delegate void InputDeviceChanged(INPUT_DEVICE_TYPE deviceType);
    public static event InputDeviceChanged OnInputDeviceChanged;

    PlayerInputAction inputActions;

    //movement & Camera Inputs
    public Vector2 moveInput { get; private set; }
    public Vector2 cameraInput { get; private set; }

    //action Flags
    public bool isAccelerating { get; private set; }
    public bool isBraking { get; private set; }
    public bool isReversing { get; private set; }
    public bool isShooting { get; private set; }
    public bool isAimingPushShoot { get; private set; }
    public bool isPushShootReleased { get; private set; }
    public bool isUsingAbility { get; private set; }
    public bool isThrowingMine { get; private set; }
    public bool isPausing { get; private set; }
    public bool isResettingCamera { get; private set; }

    public bool isDashing { get; private set; }

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

        inputActions.PlayerInputActions.Movement.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.PlayerInputActions.Movement.canceled += ctx => moveInput = Vector2.zero;

        inputActions.PlayerInputActions.CameraMove.performed += ctx => cameraInput = ctx.ReadValue<Vector2>();
        inputActions.PlayerInputActions.CameraMove.canceled += ctx => cameraInput = Vector2.zero;

        inputActions.PlayerInputActions.Accelerate.performed += ctx => isAccelerating = true;
        inputActions.PlayerInputActions.Accelerate.canceled += ctx => isAccelerating = false;

        inputActions.PlayerInputActions.Reverse.performed += ctx => isReversing = true;
        inputActions.PlayerInputActions.Reverse.canceled += ctx => isReversing = false;

        inputActions.PlayerInputActions.Brake.performed += ctx => isBraking = true;
        inputActions.PlayerInputActions.Brake.canceled += ctx => isBraking = false;

        inputActions.PlayerInputActions.RegularShoot.performed += ctx => isShooting = true;
        inputActions.PlayerInputActions.RegularShoot.canceled += ctx => isShooting = false;

        inputActions.PlayerInputActions.PushShoot.performed += ctx => OnStartAimingPushShoot();
        inputActions.PlayerInputActions.PushShoot.canceled += ctx => OnReleasedPushShoot();

        inputActions.PlayerInputActions.SpecialAbility.performed += ctx => isUsingAbility = true;
        inputActions.PlayerInputActions.SpecialAbility.canceled += ctx => isUsingAbility = false;

        inputActions.PlayerInputActions.ThrowMine.performed += ctx => isThrowingMine = true;
        inputActions.PlayerInputActions.ThrowMine.canceled += ctx => isThrowingMine = false;

        inputActions.PlayerInputActions.Pause.performed += ctx => isPausing = true;
        inputActions.PlayerInputActions.Pause.canceled += ctx => isPausing = false;

        inputActions.PlayerInputActions.ResetCamera.performed += ctx => isResettingCamera = true;
        inputActions.PlayerInputActions.ResetCamera.canceled += ctx => isResettingCamera = false;

        inputActions.PlayerInputActions.Dash.performed += ctx => isDashing = true;
        inputActions.PlayerInputActions.Dash.canceled += ctx => isDashing = false;
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

    private void OnStartAimingPushShoot()
    {
        isAimingPushShoot = true;
        isPushShootReleased = false;
    }

    private void OnReleasedPushShoot()
    {
        isAimingPushShoot = false;
        isPushShootReleased = true;
    }

    
}
