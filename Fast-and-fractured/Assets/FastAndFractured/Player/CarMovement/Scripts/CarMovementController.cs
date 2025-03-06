using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CarMovementController : MonoBehaviour
{
    public WheelController[] wheels;
    public TextMeshProUGUI speedOverlay;
    public bool applyRollPrevention = true;
    [Tooltip("Value on km/h, in code has to be / 3.6 to be converted to m/s")]
    public float maxRbVelocity;

    private PlayerInputController _playerInputController;
    private RollPrevention _rollPrevention;
    private Rigidbody _carRb;
    private bool _usingController { get; set; } = false;

    [Header("Motor Settings")]
    public STEERING_MODE SteeringMode = STEERING_MODE.FrontWheel;
    [SerializeField] private float motorTorque;// how fast the car accelerates, bigger values faster acceleration
    [SerializeField] private float maxSteerAngle; //max angle to witch the car wheel can rotate. bigger values faster and more agressive turns, should be between 30 & 45
    [SerializeField] private float steeringSmoothness; //smoothness so that the wheel doesnt instantly go form 1 angle to the other

    [Header("Brake Settings")]
    public BRAKE_MODE brakeMode = BRAKE_MODE.AllWheels;
    public bool usesCustomBraking = false;
    [SerializeField] private float brakeTorque;// regular braking force, bigger values faster stoppage
    [Tooltip("threshold to detect whether the user wants to drift or not, works only on controller")]
    [SerializeField] private float driftThreshold;
    [SerializeField] private float driftForce; //custom drift force, values should be low
    private bool _isBraking { get; set; } = false;
    private bool _isDrifting = false;
    private float _driftDirection = 1f;

    [Header("Dashing Settings")]
    public bool usingPhysicsDash;
    [SerializeField] private float dashSpeed; // dash speed for dash without physics
    [SerializeField] private float dashForce; //force for the dash with phyisics
    [SerializeField] private float maxRbVelocityWhileDashing; //limit the speed so the car doesnt accelerate infinetly
    private bool _isDashing { get; set; } = false;

    private float targetSteerAngle;
    private float currentSteerAngle;
    private Vector2 steeringInput;
    private float currentRbMaxVelocity;


    private void Start()
    {
        _playerInputController = GetComponent<PlayerInputController>();
        _rollPrevention = GetComponent<RollPrevention>();
        _carRb = GetComponent<Rigidbody>();
        currentRbMaxVelocity = maxRbVelocity;
    }
    private void OnEnable()
    {
        PlayerInputController.OnInputDeviceChanged += HandleInputDeviceChanged;
    }

    private void OnDisable()
    {
        PlayerInputController.OnInputDeviceChanged -= HandleInputDeviceChanged;
    }

    private void FixedUpdate()
    {
        if (applyRollPrevention)
        {
            float steeringInputMagnitude = steeringInput.y;
            _rollPrevention.ApplyRollPrevention(_carRb, steeringInputMagnitude);
        }
       
        UpdateWheelVisuals();
        LimitRigidBodySpeed();
        LimitRigidBodyRotation();

    }

    private void Update()
    {
        if(!_usingController)
        {
            HandleInput();
        } else
        {
            HandleInputController();
        }
        ApplySteering();
        UpdateSpeedOverlay();

        Debug.DrawRay(transform.position, _carRb.velocity, Color.red);
        Debug.DrawRay(transform.position, transform.forward * currentSteerAngle, Color.blue);
    }

    #region Input Handling

    private void HandleInputDeviceChanged(INPUT_DEVICE_TYPE deviceType)
    {
        if(deviceType == INPUT_DEVICE_TYPE.KeyboardMouse)
        {
            _usingController = false;
        } else
        {
            _usingController = true;
        }
    }

    private void HandleInput()
    {
        steeringInput = _playerInputController.moveInput;
        
        if(!_isBraking)
        {
            float acceleration = steeringInput.y * motorTorque;
            foreach (var wheel in wheels)
            {
                wheel.ApplyMotorTorque(acceleration);
            }
        }

        if(steeringInput.y < 0.05f)//should create a small threshold to consider if the button is being clicked or not
        {
            foreach (var wheel in wheels)
            {
                wheel.ApplyMotorTorque(0f);
            }
        }

        if(_playerInputController.isBraking)
        {
            if(Mathf.Abs(steeringInput.x) > driftThreshold) //only works on controller, threshold that will determine how much you have to move the joystick to enter threshold instead of regular braking
            {
                if(!_isDrifting)
                {
                    StartDrift();
                }
                ApplyDrift();
            } else
            {
                if(_isDrifting)
                {
                    EndDrift();
                }
                ApplyBrake();
            }
            _isBraking = true;
        } else
        {
            if(_isDrifting)
                EndDrift();
            _isBraking= false;
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

    private void HandleInputController()
    {

    }

    #endregion

    #region Braking Fcuntions
    private void ApplyBrake() //regular brake (user is not drifting)
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

    private void StartDrift()
    {
        _isDrifting = true;
        _driftDirection = Mathf.Sign(steeringInput.x); //only determine direcition + or -
        //currentRbMaxVelocity = maxRbVelocity * 0.5f; //recude max speed to ensure that the drift dont feel super fast (decide whther we want to do it or not)
        _carRb.drag = 1f;
    }

    private void EndDrift() 
    {
        _isDrifting = false;
        //currentRbMaxVelocity = maxRbVelocity;
        _carRb.drag = 0f;
        currentSteerAngle = 0f;
    }

    private void ApplyDrift()
    {
        Vector3 driftFinalForce = transform.right * _driftDirection * driftForce;
        _carRb.AddForce(driftFinalForce, ForceMode.Acceleration);

        //rotate the car while drifting
        float driftTorque = _driftDirection * driftForce * 1.2f; 
        _carRb.AddTorque(transform.up * driftTorque, ForceMode.Acceleration);
    }

    #endregion

    #region Steering

    private void ApplySteering()
    {

        if(_isDrifting)
        {
            currentSteerAngle = maxSteerAngle * _driftDirection; // lock steering angle to the drift direction
        } else
        {
            currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetSteerAngle, Time.deltaTime * steeringSmoothness); //sñight delelay so that wheel turn are not instanty
        }

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

    #endregion

    #region Dash
    private void HandleDahsWithoutPhysics()
    {
        if (!_isDashing)
        {
            _playerInputController.DisableInput();
            _isDashing = true;
            Vector3 dashDirection = transform.forward;
            //_carRb.
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
            currentRbMaxVelocity = maxRbVelocityWhileDashing;
            _carRb.constraints = RigidbodyConstraints.FreezeRotation;
            TimerManager.Instance.StartTimer(1.5f, () =>
            {
                _isDashing = false;
                _carRb.constraints = RigidbodyConstraints.None;   
                currentRbMaxVelocity = maxRbVelocity;
            }, (progress) => {
                _carRb.AddForce(dashDirection * dashForce, ForceMode.Impulse);
            }, "dash", false, false);
        }
    }

    #endregion

    #region rbLimiters
    private void LimitRigidBodySpeed()
    {
        Vector3 clampedVelocity = _carRb.velocity;//get current speed

        if (clampedVelocity.magnitude > (currentRbMaxVelocity / 3.6f))
        {
            clampedVelocity = clampedVelocity.normalized * (currentRbMaxVelocity / 3.6f);//retain direction (normalize) & scale it down to then apply it to rbvelocirty
            _carRb.velocity = clampedVelocity;
        }
    }

    private void LimitRigidBodyRotation()
    {
        if (_carRb.angularVelocity.magnitude > 2f) //sdjust the threshold as needed
        {
            _carRb.angularVelocity = _carRb.angularVelocity.normalized * 2f;
        }
    }

    #endregion

    private void UpdateWheelVisuals()
    {
        foreach(var wheel in wheels)
        {
            wheel.UpdateWheelVisuals();
        }
    }

    private void UpdateSpeedOverlay()
    { 
        float speedZ = Mathf.Abs(_carRb.velocity.z);
        float speedKmh = speedZ * 3.6f;
        speedOverlay.text = "Speed: " + speedKmh.ToString("F1") + " km/h";
    }
}
