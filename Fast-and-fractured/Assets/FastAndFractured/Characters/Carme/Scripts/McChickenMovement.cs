using DG.Tweening;
using UnityEngine;
using Utilities;

namespace FastAndFractured
{
    public class McChickenMovement : MonoBehaviour
    {
        public Vector3 MoveDirection => _moveDirection;
        public bool IsClimbing => _isClimbing;

        [Header("Movement Settings")]
        [SerializeField] private float maxSpeed = 30f;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float speedForce = 10f;

        [Header("Climbing")]
        [SerializeField] private float groundCheckOffset = 0.1f;
        [SerializeField] private float wallClimbSpeed = 3f;
        [SerializeField] private float climbStartDelay = 0.3f;
        [SerializeField] private float footPlacementSpeed = 5f;

        // movement tracking
        private Vector3 _moveDirection;
        private Vector3 _groundNormal;
        private Vector3 _currentWallNormal;
        private Vector3 _wallContactPoint;
        private Quaternion _preClimbRotation;

        // state variables
        private bool _hasLanded = false;
        private bool _isGrounded = false;
        private bool _isClimbing = false;
        private bool _isPreparingToClimb = false;
        private bool _isOnCeiling = false;
        private bool _canStartMovement = false;

        private Rigidbody _rb;
        private McChicken _core;
        private McChickenPhysicsHandler _physicsHandler;
        private ITimer _climbStartTimer;

        public void Initialize(Rigidbody rb, McChicken core, McChickenPhysicsHandler _physics, Vector3 moveDirection)
        {
            _rb = rb;
            _core = core;
            _physicsHandler = _physics;
            _moveDirection = moveDirection;
        }

        public void CanStartMovement()
        {
            _canStartMovement = true;
        }

        private void FixedUpdate()
        {
            if (!_canStartMovement) return;

            _physicsHandler.UpdateGroundState();

            if (_isPreparingToClimb)
            {
                HandleClimbPreparation();
            }
            else
            {
                HandleMovement();
                _physicsHandler.ApplyRotation(rotationSpeed);
                _physicsHandler.LimitRbSpeed(maxSpeed);
            }
        }



        private void HandleMovement()
        {
            if (_isClimbing)
            {
                Vector3 climbDir = Vector3.ProjectOnPlane(Vector3.up, _currentWallNormal).normalized;
                _rb.velocity = climbDir * wallClimbSpeed + _moveDirection * 0.5f;
            }
            else if (!_isGrounded)
            {
                _rb.AddForce(Physics.gravity * 2f + _moveDirection * 2f, ForceMode.Acceleration);
            }
            else
            {
                _rb.velocity = _moveDirection * moveSpeed;
            }
        }


        public void PrepareClimbing() // so that it doesnt directly jump to the weall but theres a small delay for better visuals
        {
            _isPreparingToClimb = true;
            _preClimbRotation = transform.rotation;

            _climbStartTimer = TimerSystem.Instance.CreateTimer(
                climbStartDelay,
                onTimerDecreaseComplete: StartClimbing
            );
        }

        public void StartClimbing() // needs to be upgraded to not just tp it to the position nbut lerp it and properly rotate the obj couse now it isnt doing it
        {
            _isPreparingToClimb = false;
            _isClimbing = true;
            _rb.useGravity = false;

            Vector3 footPosition = _wallContactPoint + _currentWallNormal * groundCheckOffset;
            transform.position = footPosition;
            transform.rotation = Quaternion.LookRotation(-_currentWallNormal, Vector3.up);
        }

        private void HandleClimbPreparation() // not workin properly
        {
            Vector3 targetFootPosition = _wallContactPoint + _currentWallNormal * groundCheckOffset;
            transform.position = Vector3.Lerp(
                transform.position,
                targetFootPosition,
                footPlacementSpeed * Time.fixedDeltaTime
            );

            Quaternion targetRot = Quaternion.LookRotation(-_currentWallNormal, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                rotationSpeed * Time.fixedDeltaTime
            );
        }

        public void StopClimbing()
        {
            _isClimbing = false;
            _rb.useGravity = true;
            transform.DORotateQuaternion(_preClimbRotation, 0.3f).SetEase(Ease.OutBack);
        }
    }
}


