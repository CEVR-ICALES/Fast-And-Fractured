using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Utilities;
using Enums;
using FastAndFractured;

namespace FastAndFractured
{
    public class CarMovementController : AbstractAutoInitializableMonoBehaviour, ITimeSpeedModifiable
    {
        public UnityEvent<float, float> onDashCooldownUpdate;

        public WheelController[] wheels;
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
        private bool _driftIsBackwards = false;

        [SerializeField] private AnimationCurve brakeSpeedCurve;
        [SerializeField] private float brakeSlowDownTime;
        private ITimer _brakeSlowDownTimer;

        [Header("Dashing Settings")]
        public bool usingPhysicsDash;
        [SerializeField] private float dashForce; //force for the dash with phyisics
        public bool IsDashing => _isDashing;
        private bool _isDashing = false;
        private float _previousSteeringYValue = 0;

        public bool CanDash { get => _canDash; }
        private bool _canDash = true;
        [SerializeField] private int airUseDashLimit = 1;
        private int _currentNumberOfAirDashes = 0;

        [Header("Slope Detecting")]
        [SerializeField] private float slopeAngleThreshold;
        [Tooltip("Minimum forward ratio for uphill detection")]
        [Range(0.1f, 1f)][SerializeField] private float uphillForwardThreshold = -0.3f;
        [Tooltip("Maximum forward ratio for downhill detection")]
        [Range(-1f, -0.1f)][SerializeField] private float downhillForwardThreshold = 0.3f;
        [SerializeField] private float slopeSpeedThreshold;
        [SerializeField] private float maxGroundWheelsAngleThreshold = 65;
        [SerializeField] private float maxGroundCarAngleThreshold = 12f;

        public bool IsFlipped { get { return _isFlipped; } set => _isFlipped = value; }

        public IInputProvider InputProvider { get => _inputProvider; set { _inputProvider = value;
                _inputProvider?.Initialize();
                currentProviderName = _inputProvider?.GetType().Name;
            }
        }

        private bool _isFlipped = false;

        [SerializeField]
        private float detectFlipTime = 2f;
        private ITimer _flipTimer;
        private LayerMask _combinedMask;
        [SerializeField]
        private LayerMask groundLayer;
        [SerializeField]
        private LayerMask staticLayer;

        private const float WHEELS_IN_SLOPE = 2;

        private bool _isGoingUphill;
        private bool _isGoingDownhill;
        private float _targetSteerAngle;
        private float _currentSteerAngle;
        private float _currentRbMaxVelocity;
 
        private bool _isMovingForward = false;
        private bool _isMovingBackwards = false;
        const float MOVING_DIRECTION_THRESHOLD = 0.4f;

         
 
        const float SPEED_TO_METERS_PER_SECOND = 3.6f;

        [Header("References")]
        [SerializeField] private StatsController statsController;
        [SerializeField] private VehicleVfxController vehicleVfxController;
        [SerializeField] private float slowingDownAngularMomentumTime;
        private bool _canSlowDownMomentum = false;
        private ITimer _slowDownAngularMomentumTimer;
        private IInputProvider _inputProvider;
        public string currentProviderName;
        protected override void Construct()
        {
         }
        protected override void Initialize()
        {
            base.Initialize();
            statsController.CustomStart();
            if (_physicsBehaviour == null)
            {
                _physicsBehaviour = GetComponent<PhysicsBehaviour>();
            }
            SetMaxRbSpeedDelayed();
            _combinedMask = groundLayer | staticLayer;
            if(_inputProvider==null)
            _inputProvider = GetComponentInParent<IInputProvider>();
            currentProviderName = _inputProvider?.GetType().Name;

        }

        private void FixedUpdate()
        {
            CheckSlope();
            UpdateMaxRbSpeedOnSlopes();
            UpdateWheelVisuals();
            ApplySteering();
            if (_isDrifting)
            {
                ApplyDrift();
            }
            _physicsBehaviour.LimitRigidBodySpeed(_currentRbMaxVelocity);
            _physicsBehaviour.LimitRigidBodyRotation(2f);
            SmoothAccelerationAndDeacceleration();
        }

        private void Update()
        {

        }

        private void SetMaxRbSpeedDelayed()
        {
            _currentRbMaxVelocity = statsController.MaxSpeed;
        }

        private void SmoothAccelerationAndDeacceleration()
        {
            UpdateCarCurrentDirection();

            if(_previousSteeringYValue > 0 && _isMovingBackwards) // moving backwards wants to go forward
            {
                Debug.Log("Wnats to change direction to forward");
                ApplyDirectionChange();
            }

            if(_previousSteeringYValue < 0 && _isMovingForward) // moving forward wants to go bakcwards
            {
                Debug.Log("Wnats to change direction to backward");
                ApplyDirectionChange();

            }

        }

        private void UpdateCarCurrentDirection()
        {
            float forwardVelocity = Vector3.Dot(_physicsBehaviour.Rb.linearVelocity, transform.forward);
            _isMovingForward = forwardVelocity > MOVING_DIRECTION_THRESHOLD;
            _isMovingBackwards = forwardVelocity < -MOVING_DIRECTION_THRESHOLD;
        }

        #region Refactorized Code

         

        public void ApplyMotorTorque(float acceleration)
        {
            if (_brakeSlowDownTimer != null) return;
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
        public void ProcessMovementInput()
        {
            if (_inputProvider == null) return;

            Vector2 moveInput = _inputProvider.MoveInput;
            bool isBraking = _inputProvider.IsBraking;

            if (!isBraking)
            {
                _previousSteeringYValue = moveInput.y;
                float acceleration = moveInput.y * statsController.Acceleration;
                ApplyMotorTorque(acceleration);
            }
            _targetSteerAngle = statsController.Handling * moveInput.x;

            HandleBrakingInput(isBraking, moveInput);

            if (_inputProvider.IsDashing)
            {
                HandleDashWithPhysics();
            }
        }
        public void ProcessAirRotationInput()
        {
            if (_inputProvider == null) return;
            HandleInputOnAir(_inputProvider.MoveInput);
        }
        #endregion

        #region Braking Functions
        private void ApplyBrake() //regular brake (user is not drifting)
        {
            ApplyMotorTorque(0);
            //to do add logic for all brake Types
            switch (brakeMode)
            {
                case BrakeMode.ALL_WHEELS:
                    ApplyBrakeTorque(statsController.BrakeTorque);
                    ApplyModBrake();
                    break;

                case BrakeMode.FRONT_WHEELS_STRONGER:
                    wheels[0].ApplyBrakeTorque(statsController.BrakeTorque * statsController.FrontWheelsStrenghtFactor);
                    wheels[1].ApplyBrakeTorque(statsController.BrakeTorque * statsController.FrontWheelsStrenghtFactor);
                    wheels[2].ApplyBrakeTorque(statsController.BrakeTorque * statsController.RearWheelsStrenghtFactor);
                    wheels[3].ApplyBrakeTorque(statsController.BrakeTorque * statsController.RearWheelsStrenghtFactor);
                    //ApplyModBrake(brakeSlowDownTime);
                    break;
            }
        }

        private void ApplyModBrake()
        {
            if(_brakeSlowDownTimer != null)
                return;

            ApplyMotorTorque(0f);
            ApplyBrakeTorque(statsController.BrakeTorque);
            if (_brakeSlowDownTimer == null)
            {
                Vector3 initialSpeed = _physicsBehaviour.Rb.linearVelocity;
                _brakeSlowDownTimer = TimerSystem.Instance.CreateTimer(brakeSlowDownTime, TimerDirection.INCREASE, onTimerIncreaseComplete: () =>
                {
                    _brakeSlowDownTimer = null;
                }, onTimerIncreaseUpdate: (progress) =>
                {
                    if (IsGrounded())
                    {
                        float toApply = brakeSpeedCurve.Evaluate(progress);
                        _physicsBehaviour.Rb.linearVelocity = initialSpeed * toApply;
                    }
                });
            }
        }

        private void ApplyDirectionChange()
        {
            ApplyMotorTorque(0f);
            ApplyBrakeTorque(statsController.BrakeTorque);
        }

        private void StartDrift(float steeringInput)
        {
            _driftDirection = Mathf.Sign(steeringInput); //only determine direcition + or -
            _physicsBehaviour.Rb.linearDamping = 1f;
            _initialSpeedWhenDrifting = _physicsBehaviour.Rb.linearVelocity.magnitude;

            _driftIsBackwards = Vector3.Dot(_physicsBehaviour.Rb.linearVelocity, transform.forward) < 0f;

            _isDrifting = true;
        }

        private void EndDrift()
        {
            _isDrifting = false;
            _physicsBehaviour.Rb.linearDamping = 0.08f;
        }

        private void ApplyDrift()
        {

            if (!IsGrounded())
            {
                EndDrift();
                return;
            }

            if(_brakeSlowDownTimer != null)
            {
                _brakeSlowDownTimer.StopTimer();
            }
            float speedFactor = Mathf.Clamp01(_initialSpeedWhenDrifting / (statsController.DriftingFactorToSpeed / SPEED_TO_METERS_PER_SECOND));

            Vector3 targetDriftDirection = transform.right * _driftDirection;

            //smoothly interpolate between the car's forward direction and the target drift direction
            float driftProgress = Mathf.Clamp01(Time.deltaTime * statsController.DriftingSmoothFactor); //asdjust smoothing factor as needed
            Vector3 currentDriftDirection = Vector3.Slerp(transform.forward, targetDriftDirection, driftProgress);

            if (_driftIsBackwards) { currentDriftDirection = -currentDriftDirection; }

            //aapply the drift force in the interpolated direction
            Vector3 driftFinalForce = currentDriftDirection * statsController.DriftForce * speedFactor;
            _physicsBehaviour.AddForce(driftFinalForce, ForceMode.Acceleration);

            //rotate the car while drifting
            float driftTorque = _driftDirection * statsController.DriftForce * 0.8f * speedFactor;
            if (_driftIsBackwards) { driftTorque = -driftTorque; }

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
                    if (_physicsBehaviour.Rb.linearVelocity.magnitude < 10f)
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
        public void HandleDashWithPhysics(bool skipEffectsAndSounds=false)
        {
            if (!_isDashing && _canDash)
            {
                if (!IsGrounded() && _currentNumberOfAirDashes >= airUseDashLimit)
                    return;

                _currentNumberOfAirDashes++;
                _isDashing = true;
                vehicleVfxController.StartDashVfx();
                _physicsBehaviour.BlockRigidBodyRotations();
                Vector3 dashDirection = transform.forward.normalized;
                _currentRbMaxVelocity = statsController.MaxSpeedDashing;
                _physicsBehaviour.IsCurrentlyDashing = true;
                _canDash = false;

                // Apply a small initial velocity to overcome static friction
                if (_physicsBehaviour.Rb.linearVelocity.magnitude < 1f)
                {
                    _physicsBehaviour.Rb.linearVelocity += dashDirection * 0.5f;
                }

                _dashTimer = TimerSystem.Instance.CreateTimer(statsController.DashTime, onTimerDecreaseComplete: () =>
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
            vehicleVfxController.StopDashVfx();
            _dashCooldown = TimerSystem.Instance.CreateTimer(statsController.DashCooldown, onTimerDecreaseComplete: () =>
            {
                _canDash = true;
            }, onTimerDecreaseUpdate: (progress) =>
            {
                onDashCooldownUpdate?.Invoke(progress, statsController.DashCooldown);
            });
            ModifySpeedOfExistingTimer(statsController.CooldownSpeed);
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
            if (!IsDashing && !_isBraking)
            {
                if (_isGoingUphill)
                {
                    _currentRbMaxVelocity = statsController.MaxSpeedAscend;
                }

                if (_isGoingDownhill)
                {
                    _currentRbMaxVelocity = statsController.MaxSpeedDescend;
                }

                if (!_isGoingDownhill && !_isGoingUphill)
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
            bool isMoving = _physicsBehaviour.Rb.linearVelocity.magnitude > slopeSpeedThreshold;

            _isGoingUphill = isMoving && slopeAlignment < uphillForwardThreshold; // slope opposes movement
            _isGoingDownhill = isMoving && slopeAlignment > downhillForwardThreshold; // slope aligns with movement
        }

        #endregion

        #region Ground_Check
        public bool IsGrounded()
        {
            foreach (var wheel in wheels)
            {
                if (wheel.IsGrounded())
                {
                    _currentNumberOfAirDashes = 0;
                    return true;
                }
            }
            if (_physicsBehaviour == null)
            {
                _physicsBehaviour = GetComponent<PhysicsBehaviour>();
            }
            return _physicsBehaviour.IsTouchingGround;
        }

        public bool IsInFlipCase()
        {
            if (_physicsBehaviour == null)
            {
                _physicsBehaviour = GetComponent<PhysicsBehaviour>();
            }
            return IsInWall() || _physicsBehaviour.IsTouchingGround;
        }

        public bool IsInWall()
        {
            float currentWheelsAngle = ReturnCurrentWheelsAngle(out int groundWheels);
            float absoluteXRotationOfCar = Mathf.Abs(transform.localRotation.x) * Mathf.Rad2Deg;
            return currentWheelsAngle >= maxGroundWheelsAngleThreshold || absoluteXRotationOfCar >= maxGroundCarAngleThreshold;
        }

        public void StartIsFlippedTimer(float decreseTimeFactor)
        {
            if (_flipTimer == null)
            {
                _flipTimer = TimerSystem.Instance.CreateTimer(detectFlipTime * decreseTimeFactor, onTimerDecreaseComplete: () =>
                {
                    _isFlipped = true;
                    _flipTimer = null;
                });
            }
        }

        public void StopFlippedTimer()
        {
            if (_flipTimer != null)
            {
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
                if (_canSlowDownMomentum)
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
                }
                else
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
            }
            else if (steeringInput.x < 0)
            {
                _physicsBehaviour.AddTorque(transform.forward * statsController.AirRotationForce, ForceMode.Acceleration);
            }

            if (steeringInput.y > 0)
            {
                _physicsBehaviour.AddTorque(transform.right * statsController.AirRotationForce, ForceMode.Acceleration);

            }
            else if (steeringInput.y < 0)
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

        public void OnDie()
        {
            _physicsBehaviour.Rb.isKinematic = true;
        }

        private void UpdateWheelVisuals()
        {
            foreach (var wheel in wheels)
            {
                wheel.UpdateWheelVisuals();
            }
        }

        public void ModifySpeedOfExistingTimer(float newTimerSpeed)
        {
            if (_dashCooldown != null && TimerSystem.Instance.HasTimer(_dashCooldown))
            {
                TimerSystem.Instance.ModifyTimer(_dashCooldown, speedMultiplier: newTimerSpeed);
            }
        }
    }
}
public interface IInputProvider
{
    void Initialize();
    Vector2 MoveInput { get;   }  
    bool IsBraking { get; }
    bool IsDashing { get; }
}