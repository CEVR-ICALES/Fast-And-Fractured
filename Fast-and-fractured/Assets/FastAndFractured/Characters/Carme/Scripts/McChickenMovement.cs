using DG.Tweening;
using UnityEngine;
using Utilities;

namespace FastAndFractured
{
    public class McChickenMovement : MonoBehaviour
    {
        public Vector3 MoveDirection => _currentMoveDirection;
        public bool IsClimbing => _isClimbing;

        public bool IsInCeling => _isInCeiling;

        [Header("Movement Settings")]
        [SerializeField] private float maxSpeed = 30f;
        [SerializeField] private float moveForce = 5f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float maxSlopeAngle = 45f;



        [Header("Climbing")]
        [SerializeField] private float climbLerpSpeed = 2f;
        [SerializeField] private float wallClimbSpeed = 3f;
        [SerializeField] private float jumpDuration = 3f;
        [SerializeField] private float jumpHeight = 3f;

        [Header("Gravity")]
        [SerializeField] private float customGravity;



        private Vector3 _currentMoveDirection;
        private Vector3 _initialDirection;
        private bool _isClimbing;
        private bool _applyCustomGravity = false;
        private bool _isInCeiling = false;
        private Rigidbody _rb;
        private McChickenPhysicsHandler _physicsHandler;

        public void Initialize(Rigidbody rb, McChickenPhysicsHandler physics)
        {
            _rb = rb;
            _physicsHandler = physics;
        }

        public void StartMoving(Vector3 direction)
        {
            _currentMoveDirection = direction;
            _initialDirection = direction;
            _rb.constraints = RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        }

        private void FixedUpdate()
        {
            _physicsHandler.UpdateGroundState();

            if (_isClimbing)
            {

            }
            else
            {
                //MoveForward();
                KinematicMovement();
                ApplyCustomGravity();
                _physicsHandler.LimitRbSpeed(maxSpeed);
                _physicsHandler.ApplyRotation(rotationSpeed);
            }
        }

        private void ApplyCustomGravity()
        {
            if(!_physicsHandler.IsGrounded)
            {
                _rb.AddForce(Vector3.down * customGravity, ForceMode.Impulse);
            } else
            {
                //_rb.velocity.y = 0f
            }
        }


        private void MoveForward()
        {
           Vector3 finalMoveForce = _currentMoveDirection * moveForce;
            _rb.AddForce(finalMoveForce, ForceMode.Acceleration);
        }

        private void KinematicMovement()
        {

            Vector3 targetPosition = _rb.position + _initialDirection * moveForce * Time.fixedDeltaTime;

            // Handle slope rotation
            if (_physicsHandler.IsGrounded)
            {
                float slopeAngle = Vector3.Angle(_physicsHandler.GroundNormal, Vector3.up);
                float slopeSign = Mathf.Sign(Vector3.Dot(transform.right, _physicsHandler.GroundNormal));
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
            }

            // Move position
            _rb.MovePosition(targetPosition);

        }

        public void PrepareClimbing(Vector3 climbPoint)
        {
            _isClimbing = true;
            _rb.isKinematic = true;
            _rb.useGravity = true;
            transform.DOJump(climbPoint, jumpHeight, 1, jumpDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    StopClimbing();
                    _isInCeiling = true;
                });
        }

        public void StopClimbing()
        {
            _isClimbing = false;
            _rb.useGravity = true;
            _rb.isKinematic = false;
        }
    }
}


