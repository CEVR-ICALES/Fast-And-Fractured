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

    [Header("Motor Settings")]
    public STEERING_MODE SteeringMode = STEERING_MODE.FrontWheel;
    [SerializeField] private float motorTorque;
    [SerializeField] private float maxSteerAngle;
    [SerializeField] private float steeringSmoothness;

    [Header("Brake Settings")]//to do try to block input when braking so that the car doesnt keep accelerating when brakin and turning
    public BRAKE_MODE brakeMode = BRAKE_MODE.AllWheels;
    [SerializeField] private float brakeTorque;
    private bool _isBraking { get; set; } = false;

    [Header("Dashing Settings")]
    public bool usingPhysicsDash;
    [SerializeField] private float dashSpeed;
    [SerializeField] private float dashForce;
    [SerializeField] private float maxRbVelocityWhileDashing;
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

    }

    private void OnDisable()
    {
        
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

    }

    private void Update()
    {
        HandleInput();
        ApplySteering();
        UpdateSpeedOverlay();
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
            ApplyBrake();
            _isBraking = true;
        } else
        {
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

    private void LimitRigidBodySpeed()
    {
        Vector3 clampedVelocity = _carRb.velocity;//get current speed

        if (clampedVelocity.magnitude > (currentRbMaxVelocity / 3.6f))
        {
            clampedVelocity = clampedVelocity.normalized * (currentRbMaxVelocity / 3.6f);//retain direction (normalize) & scale it down to then apply it to rbvelocirty
            _carRb.velocity = clampedVelocity;
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
