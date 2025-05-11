using DG.Tweening;
using UnityEngine;
using Utilities;
using Utilities.Managers.PauseSystem;

namespace FastAndFractured
{
    public class McChickenMovement : MonoBehaviour, IPausable
    {
        public Vector3 MoveDirection => _currentMoveDirection;
        public bool IsClimbing => _isClimbing;

        public bool IsInCeling => _isInCeiling;

        bool _isPaused = false;

        [Header("Movement Settings")]
        [SerializeField] private float initialMoveSpeed = 5f;
        [SerializeField] private float airSpeed = 0.5f;
        private float currentSpeed;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float maxSlopeAngle = 45f;

        [Header("Climbing")]
        [SerializeField] private float jumpDuration = 3f;
        [SerializeField] private float jumpHeight = 3f;

        [Header("Gravity")]
        [SerializeField] private float customGravity;

        private Vector3 _currentMoveDirection;
        private bool _isClimbing;
        private bool _isInCeiling = false;
        private bool _canSendIsGrounded = true;
        private bool _canSendIsNotGrounded = true;
        private Rigidbody _rb;
        private McChickenPhysicsHandler _physicsHandler;
        private McChickenVisuals _visualsHandler;
        private const int NUMBER_OF_JUMPS = 1;

        void OnEnable()
        {
            PauseManager.Instance?.RegisterPausable(this);
        }

        void OnDisable()
        {
            PauseManager.Instance?.UnregisterPausable(this);
        }
        public void Initialize(Rigidbody rb, McChickenPhysicsHandler physics, McChickenVisuals visuals)
        {
            _rb = rb;
            _physicsHandler = physics;
            _visualsHandler = visuals;
        }

        public void StartMoving(Vector3 direction)
        {
            _currentMoveDirection = direction;
            _rb.constraints = RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
            currentSpeed = initialMoveSpeed;
        }

        private void FixedUpdate()
        {
            if (_isPaused)
                return;

            _physicsHandler.UpdateGroundState();

            if (_isClimbing)
            {
                // TODO handle effect when climbing
            }
            else
            {

                KinematicMovement();
                if(!_physicsHandler.IsGrounded)
                {
                    currentSpeed = airSpeed;
                    ApplyCustomGravity();
                    _rb.angularVelocity = Vector3.zero;
                } else
                {
                    currentSpeed = initialMoveSpeed;
                }
            }
        }

        private void ApplyCustomGravity()
        {
            if(!_physicsHandler.IsGrounded)
            {
                _rb.AddForce(Vector3.down * customGravity, ForceMode.Impulse);
            }
        }

        private void KinematicMovement()
        {
            // handle slope rotation
            if (_physicsHandler.IsGrounded)
            {
                if(_canSendIsGrounded)
                {
                    _visualsHandler.OnChickenOnFloor();
                    _canSendIsNotGrounded = true;
                    _canSendIsGrounded = false;
                }
                float slopeAngle = Vector3.Angle(_physicsHandler.GroundNormal, Vector3.up);
                float slopeSign = Mathf.Sign(Vector3.Dot(-transform.right, _physicsHandler.GroundNormal));
                float targetXRotation = Mathf.Clamp(slopeAngle * slopeSign, -maxSlopeAngle, maxSlopeAngle);

                Quaternion targetRot = Quaternion.Euler(
                    -targetXRotation,
                    transform.eulerAngles.y,
                    transform.eulerAngles.z
                );

                _rb.MoveRotation(Quaternion.Slerp(
                    _rb.rotation,
                    targetRot,
                    rotationSpeed * Time.fixedDeltaTime
                ));
            } else
            {
                if(_canSendIsNotGrounded)
                {
                    _visualsHandler.OnChickenOffFloor();
                    _canSendIsGrounded = true;
                    _canSendIsNotGrounded = false;
                }
                _rb.angularVelocity = Vector3.zero;
            }

            transform.position += _currentMoveDirection * currentSpeed * Time.fixedDeltaTime;

        }

        public void PrepareClimbing(Vector3 climbPoint)
        {
            _visualsHandler.OnChickenOffFloor();
            _isClimbing = true;
            _rb.isKinematic = true;
            _rb.useGravity = true;
            transform.DOJump(climbPoint, jumpHeight, NUMBER_OF_JUMPS, jumpDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    StopClimbing();
                    _isInCeiling = true;
                });
        }

        public void StopClimbing()
        {
            _visualsHandler.OnChickenOnFloor();
            _isClimbing = false;
            _rb.useGravity = true;
            _rb.isKinematic = false;
            _physicsHandler.UpdateGroundStateBool(false);
        }

        public void StopIsOnCeiling()
        {
            _isInCeiling = false;
        }

        public void OnPause()
        {
            _isPaused = true;
        }

        public void OnResume()
        {
            _isPaused = false;
        }
    }
}


