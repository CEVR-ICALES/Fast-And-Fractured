using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Utilities;
using Enums;

namespace FastAndFractured
{
    public class CarMovementController : MonoBehaviour
    {
        public UnityEvent<float, float> onDashCooldownUpdate;

        public WheelController[] wheels;
        public TextMeshProUGUI speedOverlay;
        public bool applyRollPrevention = true;

        private PhysicsBehaviour _physicsBehaviour;

        [Header("Motor Settings")]
        public SteeringMode SteeringMode = SteeringMode.FRONT_WHEEL;


        [Header("Brake Settings")]
        public BrakeMode brakeMode = BrakeMode.ALL_WHEELS;
        public bool usesCustomBraking = false;
        public bool IsBraking => _isBraking;
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
        [SerializeField] private float maxGroundAngleThreshold = 65;

        public bool IsFlipped { get { return _isFlipped; } set => _isFlipped = value; }
        private bool _isFlipped = false;

        [SerializeField]
        private float detectFlipTime = 3.5f;
        private ITimer _flipTimer;

        private const float WHEELS_IN_SLOPE = 2; 

        private bool _isGoingUphill;
        private bool _isGoingDownhill;
        private float _targetSteerAngle;
        private float _currentSteerAngle;
        private float _currentRbMaxVelocity;
        private bool _isUsingController = false;

        public bool IsAi => isAi;
        [SerializeField] private bool isAi = false;

        const float SPEED_TO_METERS_PER_SECOND = 3.6f;

        [Header("References")]
        [SerializeField] private StatsController statsController;
        [SerializeField] private float slowingDownAngularMomentumTime;
        private bool _canSlowDownMomentum = false;
        private ITimer _slowDownAngularMomentumTimer;


        private void Start()
        {
            statsController.CustomStart();
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


        // public void HandleInputChange(bool usingController)
        // {
        //     if (isAi)
        //     {
        //         _isUsingController = false;
        //     }
        //     else
        //     {
        //         _isUsingController = usingController;
        //     }
        // }


        #region Refactorized Code

        public void HandleSteeringInput(Vector2 steeringInput)
        {
            if (!PlayerInputController.Instance.IsUsingController && !_isBraking)
            {
                float acceleration = steeringInput.y * statsController.Acceleration;
                ApplyMotorTorque(acceleration);
                // Possible set motor torque to 0 if no input (w,s)
            }
            _targetSteerAngle = statsController.Handling * steeringInput.x;
        }

        public void HandleAccelerateInput(float rawAccelerationInput)
        {
            if (PlayerInputController.Instance.IsUsingController && !_isBraking && rawAccelerationInput > 0)
            {
                float acceleration = rawAccelerationInput * statsController.Acceleration;
                ApplyMotorTorque(acceleration);
            }
        }

        public void HandleDeaccelerateInput(float rawAccelerationInput)
        {
            if (PlayerInputController.Instance.IsUsingController && !_isBraking && rawAccelerationInput > 0)
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
                    onDashCooldownUpdate?.Invoke(statsController.DashTime - progress, statsController.DashTime);
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
                 onDashCooldownUpdate?.Invoke(progress, statsController.DashCooldown);
             });
        }
        public void CancelDash()
        {
            if (TimerSystem.Instance.HasTimer(_dashTimer))
            {
                TimerSystem.Instance.StopTimer(_dashTimer.GetData().ID);
            }
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
            float currentSlopeAngle = ReturnCurrentWheelsAngle(out int groundWheels, out Vector3 combinedNormal);

            // reset states if not on significant slope or not enough wheels on floor
            if (groundWheels < WHEELS_IN_SLOPE || currentSlopeAngle <= slopeAngleThreshold)
            {
                _isGoingUphill = false;
                _isGoingDownhill = false;
                return;
            }
            
            // calculate average ground normal (up direction of the surface)
            Vector3 averageNormal = combinedNormal / groundWheels;
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

        #region Ground_Check
        public bool IsGrounded()
        {
            foreach (var wheel in wheels)
            {
                if(wheel.IsGrounded())
                    return true;
            }
            return _physicsBehaviour.IsTouchingGround;
        }

        public bool IsInWall()
        {
            float currentWheelsAngle = ReturnCurrentWheelsAngle(out int groundWheels);
            
            if (groundWheels < WHEELS_IN_SLOPE || currentWheelsAngle < maxGroundAngleThreshold)
            {
                return false;
            }
            Debug.Log("IsWall");
            return currentWheelsAngle >= maxGroundAngleThreshold;
        }

        public void StartIsFlippedTimer()
        {
            if (_flipTimer == null)
            {
                Debug.Log("StartTimer");
                _flipTimer = TimerSystem.Instance.CreateTimer(detectFlipTime, TimerDirection.INCREASE, () => { _isFlipped = true; });
            }
        }

        public void StopFlippedTimer()
        {
            if (_flipTimer != null)
            {
                Debug.Log("StopTimer");
                _flipTimer.StopTimer();
                _flipTimer = null;
            }
        }

        private float ReturnCurrentWheelsAngle(out int groundWheels)
        {
            float wheelsAngle = 0;
            groundWheels = 0;

            foreach (var wheel in wheels)
            {
                WheelGroundInfo groundInfo = wheel.GetGroundInfo();
                if (groundInfo.isGrounded)
                {
                    wheelsAngle = Mathf.Max(wheelsAngle, groundInfo.slopeAngle);
                    groundWheels++;
                }
            }
            return wheelsAngle;
        }

        private float ReturnCurrentWheelsAngle(out int groundWheels, out Vector3 combinedNormal)
        {
            float wheelsAngle = 0;
            groundWheels = 0;
            combinedNormal = Vector3.zero;

            foreach (var wheel in wheels)
            {
                WheelGroundInfo groundInfo = wheel.GetGroundInfo();
                if (groundInfo.isGrounded)
                {
                    wheelsAngle = Mathf.Max(wheelsAngle, groundInfo.slopeAngle);
                    combinedNormal += groundInfo.groundNormal;
                    groundWheels++;
                }
            }
            return wheelsAngle;
        }
        #endregion

        #region AirRotation
        public void HandleInputOnAir(Vector2 steeringInput)
        {

            if (steeringInput.x == 0 && steeringInput.y == 0)// slow down momentum when rotating in frhe air
            {
                if(_canSlowDownMomentum)
                {
                    _physicsBehaviour.SlowDownAngularMomentum();
                }
            }
            else
            {
                _canSlowDownMomentum = true;
                if (_slowDownAngularMomentumTimer == null)
                {
                    _slowDownAngularMomentumTimer = TimerSystem.Instance.CreateTimer(slowingDownAngularMomentumTime, onTimerDecreaseComplete: () =>
                    {
                        _canSlowDownMomentum = false;
                        _slowDownAngularMomentumTimer = null;
                    });
                } else
                {
                    if (TimerSystem.Instance.HasTimer(_slowDownAngularMomentumTimer))
                    {
                        TimerSystem.Instance.ModifyTimer(_slowDownAngularMomentumTimer, newCurrentTime: slowingDownAngularMomentumTime);
                    }
                }
                 
                
            }

            if (steeringInput.x > 0)
            {
                _physicsBehaviour.AddTorque(-transform.forward * statsController.AirRotationForce, ForceMode.Acceleration);
            } else if(steeringInput.x < 0)
            {
                _physicsBehaviour.AddTorque(transform.forward * statsController.AirRotationForce, ForceMode.Acceleration);
            }

            if(steeringInput.y > 0)
            {
                _physicsBehaviour.AddTorque(transform.right * statsController.AirRotationForce, ForceMode.Acceleration);
                
            } else if(steeringInput.y < 0)
            {
                _physicsBehaviour.AddTorque(-transform.right * statsController.AirRotationForce, ForceMode.Acceleration);
            }

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
