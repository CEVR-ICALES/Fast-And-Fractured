using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInputController : MonoBehaviour
{
    public static PlayerInputController Instance { get; private set; }
    
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

    private void OnDisable()
    {
        inputActions.Disable();
    }
}
