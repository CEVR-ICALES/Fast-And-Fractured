using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Utilities;
using Enums;

namespace FastAndFractured
{
    public class CarMovementController : MonoBehaviour
    {
        public WheelController[] wheels;
        public TextMeshProUGUI speedOverlay;
        public bool applyRollPrevention = true;

        private PhysicsBehaviour _physicsBehaviour;

        [Header("Motor Settings")]
        public SteeringMode SteeringMode = SteeringMode.FRONT_WHEEL;


        [Header("Brake Settings")]
        public BrakeMode brakeMode = BrakeMode.ALL_WHEELS;
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

        public bool CanDash { get => _canDash; }
        private bool _canDash = true;

        [Header("Slope Detecting")]
        [SerializeField] private float slopeAngleThreshold;
        [Tooltip("Minimum forward ratio for uphill detection")]
        [Range(0.1f, 1f)][SerializeField] private float uphillForwardThreshold = -0.3f;
        [Tooltip("Maximum forward ratio for downhill detection")]
        [Range(-1f, -0.1f)][SerializeField] private float downhillForwardThreshold = 0.3f;
        [SerializeField] private float slopeSpeedThreshold;

        private float _currentSlopeAngle;
        private bool _isGoingUphill;
        private bool _isGoingDownhill;
        private float _targetSteerAngle;
        private float _currentSteerAngle;
        private float _currentRbMaxVelocity;
        private bool _isUsingController = false;
        [SerializeField] private bool isAi = false;
        const float SPEED_TO_METERS_PER_SECOND = 3.6f;

        [Header("References")]
        [SerializeField] private StatsController statsController;


        private void Start()
        {
            _physicsBehaviour = GetComponent<PhysicsBehaviour>();
            SetMaxRbSpeedDelayed();
        }



        private void FixedUpdate()
        {
            CheckSlope();
            UpdateMaxRbSpeedOnSlopes();
            UpdateWheelVisuals();
            ApplySteering();
            if(_isDrifting)
            {
                ApplyDrift();
            }
            _physicsBehaviour.LimitRigidBodySpeed(_currentRbMaxVelocity);
            _physicsBehaviour.LimitRigidBodyRotation(2f);

        }

        private void Update()
        {
            UpdateSpeedOverlay();
        }

        private void SetMaxRbSpeedDelayed()
        {
            _currentRbMaxVelocity = statsController.MaxSpeed;
        }


        public void HandleInputChange(bool usingController)
        {
            if (isAi)
            {
                _isUsingController = false;
            }
            else
            {
                _isUsingController = usingController;
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
        }

        public void HandleAccelerateInput(float rawAccelerationInput)
        {
            if (_isUsingController && !_isBraking && rawAccelerationInput > 0)
            {
                float acceleration = rawAccelerationInput * statsController.Acceleration;
                ApplyMotorTorque(acceleration);
            }
        }

        public void HandleDeaccelerateInput(float rawAccelerationInput)
        {
            if (_isUsingController && !_isBraking && rawAccelerationInput > 0)
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
                case BrakeMode.ALL_WHEELS:
                    ApplyBrakeTorque(statsController.BrakeTorque);
                    break;

                case BrakeMode.FRONT_WHEELS_STRONGER:
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
                case SteeringMode.FRONT_WHEEL:
                    wheels[0].ApplySteering(_currentSteerAngle);
                    wheels[1].ApplySteering(_currentSteerAngle);
                    break;

                case SteeringMode.REAR_WHEEL:
                    float rearSteerAngle = _currentSteerAngle;
                    if (_physicsBehaviour.Rb.velocity.magnitude < 10f)
                    {
                        rearSteerAngle = -_currentSteerAngle; //posite direciton wheen going at low speeds
                    }
                    wheels[2].ApplySteering(rearSteerAngle);
                    wheels[3].ApplySteering(rearSteerAngle);
                    break;

                case SteeringMode.ALL_WHEEL:
                    foreach (var wheel in wheels)
                    {
                        wheel.ApplySteering(_currentSteerAngle);
                    }
                    break;
            }
        }

        #endregion

        #region Dash
        ITimer _dashTimer;
        public void HandleDashWithPhysics()
        {
            if (!_isDashing && _canDash)
            {
                _isDashing = true;
                _physicsBehaviour.BlockRigidBodyRotations();
                Vector3 dashDirection = transform.forward.normalized;
                _currentRbMaxVelocity = statsController.MaxSpeedDashing;
                _physicsBehaviour.IsCurrentlyDashing = true;
                _canDash = false;
                _dashTimer=  TimerSystem.Instance.CreateTimer(statsController.DashTime, onTimerDecreaseComplete: () =>
                {
                    FinishDash();
                }, onTimerDecreaseUpdate: (progress) =>
                {
                    _physicsBehaviour.AddForce(dashDirection * dashForce, ForceMode.Impulse);
                });
            }
        }
        ITimer _dashCooldown;
        private void FinishDash()
        {
            _isDashing = false;
            _physicsBehaviour.UnblockRigidBodyRotations();
            _currentRbMaxVelocity = statsController.MaxSpeed;
            _physicsBehaviour.IsCurrentlyDashing = false;
            _dashCooldown = TimerSystem.Instance.CreateTimer(statsController.DashCooldown, onTimerDecreaseComplete: () =>
             {
                 _canDash = true;
             }, onTimerDecreaseUpdate: (progress) =>
             {

             });
        }
        public void CancelDash()
        {
            TimerSystem.Instance.StopTimer(_dashTimer.GetData().ID);
            FinishDash();
        }

        #endregion

        #region Slopes

        private void UpdateMaxRbSpeedOnSlopes()
        {
            if(!IsDashing && !_isBraking)
            {
                if(_isGoingUphill)
                {
                    _currentRbMaxVelocity = statsController.MaxSpeedAscend;
                }

                if(_isGoingDownhill)
                {
                    _currentRbMaxVelocity = statsController.MaxSpeedDescend;
                }

                if(!_isGoingDownhill && !_isGoingUphill)
                {
                    _currentRbMaxVelocity = statsController.MaxSpeed;
                }
            }
        }
        private void CheckSlope()
        {
            _currentSlopeAngle = 0;
            int groundedWheels = 0;
            Vector3 combinedNormal = Vector3.zero;

            foreach (WheelController wheel in wheels)
            {
                WheelGroundInfo groundInfo = wheel.GetGroundInfo();
                if (groundInfo.isGrounded)
                {
                    _currentSlopeAngle = Mathf.Max(_currentSlopeAngle, groundInfo.slopeAngle);
                    combinedNormal += groundInfo.groundNormal;
                    groundedWheels++;
                }
            }

            // reset states if not on significant slope or not enough wheels on floor
            if (groundedWheels < 2 || _currentSlopeAngle <= slopeAngleThreshold)
            {
                _isGoingUphill = false;
                _isGoingDownhill = false;
                return;
            }
            
            // calculate average ground normal (up direction of the surface)
            Vector3 averageNormal = combinedNormal / groundedWheels;
            Vector3 carForward = transform.forward;

            // flatten the cars forward vector to ignore vertical component
            Vector3 carForwardFlat = Vector3.ProjectOnPlane(carForward, Vector3.up).normalized;

            // calculate how much the slope is aligned with carss forward direction
            float slopeAlignment = Vector3.Dot(averageNormal, carForwardFlat);

            // prevent change of speed liimtations when not moving enough
            bool isMoving = _physicsBehaviour.Rb.velocity.magnitude > slopeSpeedThreshold;

            _isGoingUphill = isMoving && slopeAlignment < uphillForwardThreshold; // slope opposes movement
            _isGoingDownhill = isMoving && slopeAlignment > downhillForwardThreshold; // slope aligns with movement


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
            if (speedOverlay != null)
                speedOverlay.text = "Speed: " + speedKmh.ToString("F1") + " km/h";
        }
    }
}
