using TMPro;
using UnityEngine;

namespace Game {
    public class CarMovementController : MonoBehaviour
    {
        public WheelController[] wheels;
        public TextMeshProUGUI speedOverlay;
        public bool applyRollPrevention = true;

        private PhysicsBehaviour _physicsBehaviour;

        [Header("Motor Settings")]
        public STEERING_MODE SteeringMode = STEERING_MODE.FrontWheel;


        [Header("Brake Settings")]
        public BRAKE_MODE brakeMode = BRAKE_MODE.AllWheels;
        public bool usesCustomBraking = false;
        private bool _isBraking = false;
        private bool _isDrifting = false;
        private float _driftDirection = 1f;
        private float _initialSpeedWhenDrifting;

        [Header("Dashing Settings")]
        public bool usingPhysicsDash;
        [SerializeField] private float dashForce; //force for the dash with phyisics
        public bool IsDashing => _isDashing;
        private bool _isDashing = false;

        private float _targetSteerAngle;
        private float _currentSteerAngle;
        private float _currentRbMaxVelocity;
        public bool _isUsingController = false;
        const float SPEED_TO_METERS_PER_SECOND = 3.6f;

        [Header("References")]
        [SerializeField] private StatsController statsController;


        private void Start()
        {
            _physicsBehaviour = GetComponent<PhysicsBehaviour>();
            _currentRbMaxVelocity = statsController.MaxSpeed;
        }
        private void OnEnable()
        {
            PlayerInputController.OnInputDeviceChanged += HandleInputChange;
        }

        private void OnDisable()
        {
            PlayerInputController.OnInputDeviceChanged -= HandleInputChange;
        }

        private void OnDestroy()
        {
            TimerManager.Instance.StopTimer("dash");
        }

        private void FixedUpdate()
        {

            UpdateWheelVisuals();
            _physicsBehaviour.LimitRigidBodySpeed(_currentRbMaxVelocity);
            _physicsBehaviour.LimitRigidBodyRotation(2f);

        }

        private void Update()
        {
            UpdateSpeedOverlay();

            Debug.DrawRay(transform.position, _physicsBehaviour.Rb.velocity, Color.red);
            Debug.DrawRay(transform.position, transform.forward * _currentSteerAngle, Color.blue);
        }

        public void HandleInputChange(INPUT_DEVICE_TYPE inputType)
        {
            // Debug.Log(inputType);
            if (inputType == INPUT_DEVICE_TYPE.KeyboardMouse)
            {
                _isUsingController = false;
            } else if (inputType == INPUT_DEVICE_TYPE.XboxController || inputType == INPUT_DEVICE_TYPE.PSController)
            {
                _isUsingController = true;
            }
        }


        #region Refactorized Code

        public void HandleSteeringInput(Vector2 steeringInput)
        {
            if (!_isUsingController && !_isBraking)
            {
                float acceleration = steeringInput.y * statsController.Acceleration;
                ApplyMotorTorque(acceleration);
                // Possible set motor torque to 0 if no input (w,s)
            }
            _targetSteerAngle = statsController.Handling * steeringInput.x;
            ApplySteering();
        }

        public void HandleAccelerateInput(float rawAccelerationInput)
        {
            if (_isUsingController && !_isBraking)
            {
                float acceleration = rawAccelerationInput * statsController.Acceleration;
                ApplyMotorTorque(acceleration);
            }
        }

        public void HandleDeaccelerateInput(float rawAccelerationInput)
        {
            if (_isUsingController && !_isBraking)
            {
                float acceleration = rawAccelerationInput * statsController.Acceleration;
                ApplyMotorTorque(-acceleration);
            }
        }

        private void ApplyMotorTorque(float acceleration)
        {
            foreach (WheelController wheel in wheels)
            {
                wheel.ApplyMotorTorque(acceleration);
            }
        }

        private void ApplyBrakeTorque(float brakeTorque)
        {
            foreach (WheelController wheel in wheels)
            {
                wheel.ApplyBrakeTorque(brakeTorque);
            }
        }

        public void HandleBrakingInput(bool isBraking, Vector2 steeringInput)
        {
            _isBraking = isBraking;
            if (_isBraking)
            {
                if (Mathf.Abs(steeringInput.x) > statsController.DriftThreshold)
                {
                    if (!_isDrifting)
                    {
                        StartDrift(steeringInput.x);
                    }
                    ApplyDrift();
                }
                else
                {
                    if (_isDrifting)
                    {
                        EndDrift();
                    }
                    ApplyBrake();
                }
                _isBraking = true;
            }
            else
            {
                if (_isDrifting)
                    EndDrift();
                _isBraking = false;
                ApplyBrakeTorque(0f);
            }
        }

        #endregion

        #region Braking Functions
        private void ApplyBrake() //regular brake (user is not drifting)
        {
            //to do add logic for all brake Types
            switch (brakeMode)
            {
                case BRAKE_MODE.AllWheels:
                    ApplyBrakeTorque(statsController.BrakeTorque);
                    break;

                case BRAKE_MODE.FrontWheelsStronger:
                    wheels[0].ApplyBrakeTorque(statsController.BrakeTorque * statsController.FrontWheelsStrenghtFactor);
                    wheels[1].ApplyBrakeTorque(statsController.BrakeTorque * statsController.FrontWheelsStrenghtFactor);
                    wheels[2].ApplyBrakeTorque(statsController.BrakeTorque * statsController.RearWheelsStrenghtFactor);
                    wheels[3].ApplyBrakeTorque(statsController.BrakeTorque * statsController.RearWheelsStrenghtFactor);
                    break;
            }
        }

        private void StartDrift(float steeringInput)
        {
            _isDrifting = true;
            _driftDirection = Mathf.Sign(steeringInput); //only determine direcition + or -
            _physicsBehaviour.Rb.drag = 1f;
            _initialSpeedWhenDrifting = _physicsBehaviour.Rb.velocity.magnitude;
        }

        private void EndDrift()
        {
            _isDrifting = false;
            _physicsBehaviour.Rb.drag = 0.08f;
        }

        private void ApplyDrift() //to do consider current speed to determine how the drift is going to work
        {
            float speedFactor = Mathf.Clamp01(_initialSpeedWhenDrifting / (statsController.DriftingFactorToSpeed / SPEED_TO_METERS_PER_SECOND));

            Vector3 targetDriftDirection = transform.right * _driftDirection;

            //smoothly interpolate between the car's forward direction and the target drift direction
            float driftProgress = Mathf.Clamp01(Time.deltaTime * statsController.DriftingSmoothFactor); //asdjust smoothing factor as needed
            Vector3 currentDriftDirection = Vector3.Slerp(transform.forward, targetDriftDirection, driftProgress);

            //aapply the drift force in the interpolated direction
            Vector3 driftFinalForce = currentDriftDirection * statsController.DriftForce * speedFactor;
            _physicsBehaviour.AddForce(driftFinalForce, ForceMode.Acceleration);

            //rotate the car while drifting
            float driftTorque = _driftDirection * statsController.DriftForce * 0.8f * speedFactor;
            _physicsBehaviour.AddTorque(transform.up * driftTorque, ForceMode.Acceleration);
        }

        #endregion

        #region Steering

        private void ApplySteering()
        {

            if (_isDrifting)
            {
                _currentSteerAngle = statsController.Handling * _driftDirection; // lock steering angle to the drift direction
            }
            else
            {
                _currentSteerAngle = Mathf.Lerp(_currentSteerAngle, _targetSteerAngle, Time.deltaTime * statsController.HandlingSmothnees); //slight delelay so that wheel turn are not instanty
            }

            switch (SteeringMode)
            {
                case STEERING_MODE.FrontWheel:
                    wheels[0].ApplySteering(_currentSteerAngle);
                    wheels[1].ApplySteering(_currentSteerAngle);
                    break;

                case STEERING_MODE.RearWheel:
                    float rearSteerAngle = _currentSteerAngle;
                    if (_physicsBehaviour.Rb.velocity.magnitude < 10f)
                    {
                        rearSteerAngle = -_currentSteerAngle; //posite direciton wheen going at low speeds
                    }
                    wheels[2].ApplySteering(rearSteerAngle);
                    wheels[3].ApplySteering(rearSteerAngle);
                    break;

                case STEERING_MODE.AllWheel:
                    foreach (var wheel in wheels)
                    {
                        wheel.ApplySteering(_currentSteerAngle);
                    }
                    break;
            }
        }

        #endregion

        #region Dash

        public void HandleDashWithPhysics()
        {
            if (!_isDashing)
            {
                _isDashing = true;
                _physicsBehaviour.BlockRigidBodyRotations();
                Vector3 dashDirection = transform.forward.normalized;
                _currentRbMaxVelocity = statsController.MaxSpeedDashing;
                _physicsBehaviour.isCurrentlyDashing = true;
                TimerManager.Instance.StartTimer(statsController.DashTime, () =>
                {
                    FinishDash();
                }, (progress) =>
                {
                    _physicsBehaviour.AddForce(dashDirection * dashForce, ForceMode.Impulse);
                }, "dash", false, true);
            }
        }

        private void FinishDash()
        {
            _isDashing = false;
            _physicsBehaviour.UnblockRigidBodyRotations();
            _currentRbMaxVelocity = statsController.MaxSpeed;
            _physicsBehaviour.isCurrentlyDashing = false;
        }
        public void CancelDash()
        {
            TimerManager.Instance.StopTimer("dash"); //shouldnt be hard coded, but since i dont know how the final structure is going to be i just put it like this
            FinishDash();
        }

        #endregion

        public void StopAllCarMovement()
        {
            foreach (var wheel in wheels)
            {
                wheel.ApplyBrakeTorque(0f);
                wheel.ApplyMotorTorque(0f);
            }
        }

        private void UpdateWheelVisuals()
        {
            foreach (var wheel in wheels)
            {
                wheel.UpdateWheelVisuals();
            }
        }

        private void UpdateSpeedOverlay()
        {
            float speedZ = Mathf.Abs(_physicsBehaviour.Rb.velocity.magnitude);
            float speedKmh = speedZ * SPEED_TO_METERS_PER_SECOND;
            speedOverlay.text = "Speed: " + speedKmh.ToString("F1") + " km/h";
        }
    }
}
