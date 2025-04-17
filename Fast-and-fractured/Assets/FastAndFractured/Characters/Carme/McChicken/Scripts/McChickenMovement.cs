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
        [SerializeField] private float moveForce = 5f;
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
        private Rigidbody _rb;
        private McChickenPhysicsHandler _physicsHandler;
        private const int NUMBER_OF_JUMPS = 1;

        public void Initialize(Rigidbody rb, McChickenPhysicsHandler physics)
        {
            _rb = rb;
            _physicsHandler = physics;
        }

        public void StartMoving(Vector3 direction)
        {
            _currentMoveDirection = direction;
            _rb.constraints = RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        }

        private void FixedUpdate()
        {
            _physicsHandler.UpdateGroundState();

            if (_isClimbing)
            {
                // TODO handle effect when climbing
            }
            else
            {
                KinematicMovement();
                ApplyCustomGravity();
                _physicsHandler.ApplyRotation(rotationSpeed);
            }
        }

        private void ApplyCustomGravity()
        {
            if(!_physicsHandler.IsGrounded)
            {
                Debug.Log("gravity");
                _rb.AddForce(Vector3.down * customGravity, ForceMode.Impulse);
            }
        }

        private void KinematicMovement()
        {
            // handle slope rotation
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

            transform.position += _currentMoveDirection * moveForce * Time.fixedDeltaTime;

        }

        public void PrepareClimbing(Vector3 climbPoint)
        {
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
            _isClimbing = false;
            _rb.useGravity = true;
            _rb.isKinematic = false;
        }
    }
}


