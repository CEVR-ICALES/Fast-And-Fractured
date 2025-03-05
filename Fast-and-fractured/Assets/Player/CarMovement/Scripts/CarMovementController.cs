using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarMovementController : MonoBehaviour
{
    public WheelController[] wheels;

    private PlayerInputController _playerInputController;
    private RollPrevention _rollPrevention;
    private Rigidbody _carRb;

    [Header("Motor Settings")]
    public STEERING_MODE SteeringMode = STEERING_MODE.FrontWheel;
    [SerializeField] private float motorTorque;
    [SerializeField] private float maxSteerAngle;
    [SerializeField] private float steeringSmoothness;


    [Header("Brake Settings")]
    public BRAKE_MODE brakeMode = BRAKE_MODE.AllWheels;
    [SerializeField] private float brakeTorque;

    [Header("Dashing Settings")]
    public bool usingPhysicsDash;
    [SerializeField] private float dashSpeed;
    [SerializeField] private float dashForce;
    [SerializeField] private float maxRbVelocityWhileDashing;
    private bool _isDashing { get; set; } = false;

    private float targetSteerAngle;
    private float currentSteerAngle;
    private Vector2 steeringInput;


    private void Start()
    {
        _playerInputController = GetComponent<PlayerInputController>();
        _rollPrevention = GetComponent<RollPrevention>();
        _carRb = GetComponent<Rigidbody>();
    }
    private void OnEnable()
    {

    }

    private void OnDisable()
    {
        
    }

    private void FixedUpdate()
    {
        float steeringInputMagnitude = steeringInput.magnitude;
        _rollPrevention.ApplyRollPrevention(_carRb, steeringInputMagnitude);

        UpdateWheelVisuals();

    }

    private void Update()
    {
        HandleInput();
        ApplySteering();
    }

    private void HandleInput()
    {
        steeringInput = _playerInputController.moveInput;

        float acceleration = steeringInput.y * motorTorque;
        foreach(var wheel in wheels)
        {
            wheel.ApplyMotorTorque(acceleration);
        }

        if(_playerInputController.isBraking)
        {
            ApplyBrake();
        } else
        {
            foreach(var wheel in wheels)
            {
                wheel.ApplyBrakeTorque(0f); //update brake to 0, if not it will keep applying last brake value
            }
        }

        if(_playerInputController.isDashing)
        {
            if(usingPhysicsDash)
            {
                HandleDashWithPhysics();
            } else
            {
                HandleDahsWithoutPhysics();
            }
        }

        targetSteerAngle = maxSteerAngle * steeringInput.x;
    }

    private void ApplyBrake()
    {
        //to do add logic for all brake Types
        switch (brakeMode)
        {
            case BRAKE_MODE.AllWheels:
                foreach (var wheel in wheels)
                {
                    wheel.ApplyBrakeTorque(brakeTorque);
                }
                break;

            case BRAKE_MODE.FrontWheelsStronger:
                wheels[0].ApplyBrakeTorque(brakeTorque * 0.8f);
                wheels[1].ApplyBrakeTorque(brakeTorque * 0.8f);
                wheels[2].ApplyBrakeTorque(brakeTorque * 0.2f);
                wheels[3].ApplyBrakeTorque(brakeTorque * 0.2f);
                break;
        }
    }


    private void ApplySteering()
    {
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetSteerAngle, Time.deltaTime * steeringSmoothness); //sñight delelay so that wheel turn are not instanty

        switch(SteeringMode)
        {
            case STEERING_MODE.FrontWheel:
                wheels[0].ApplySteering(currentSteerAngle);
                wheels[1].ApplySteering(currentSteerAngle);
                break;

            case STEERING_MODE.RearWheel:
                float rearSteerAngle = currentSteerAngle;
                if(_carRb.velocity.magnitude < 10f)
                {
                    rearSteerAngle = -currentSteerAngle; //posite direciton wheen going at low speeds
                }
                wheels[2].ApplySteering(rearSteerAngle);
                wheels[3].ApplySteering(rearSteerAngle);
                break;

            case STEERING_MODE.AllWheel:
                foreach(var wheel in wheels)
                {
                    wheel.ApplySteering(currentSteerAngle);
                }
                break;
        }
    }

    private void HandleDahsWithoutPhysics()
    {
        if (!_isDashing)
        {
            _playerInputController.DisableInput();
            _isDashing = true;
            Vector3 dashDirection = transform.forward;
            _carRb.isKinematic = true;
            TimerManager.Instance.StartTimer(0.7f, () =>
            {
                _playerInputController.EnableInput();
                _isDashing = false;
                _carRb.isKinematic = false;
            }, (progress) =>
            {
                transform.position += dashDirection * dashSpeed * Time.deltaTime;
            }, "dash", false, false);
        }
    }

    private void HandleDashWithPhysics()
    {
        if (!_isDashing)
        {
            _isDashing = true;
            Vector3 dashDirection = transform.forward.normalized;
            TimerManager.Instance.StartTimer(1.5f, () =>
            {
                _isDashing = false;
            }, (progress) => {
                _carRb.AddForce(dashDirection * dashForce, ForceMode.Impulse);
                Vector3 clampedVelocity = _carRb.velocity;//get current speed

                if (clampedVelocity.magnitude > maxRbVelocityWhileDashing)
                {
                    clampedVelocity = clampedVelocity.normalized * maxRbVelocityWhileDashing;//retain direction (normalize) & scale it down to then apply it to rbvelocirty
                    _carRb.velocity = clampedVelocity;
                }

            }, "dash", false, false);
        }
    }

    private void UpdateWheelVisuals()
    {
        foreach(var wheel in wheels)
        {
            wheel.UpdateWheelVisuals();
        }
    }
}
