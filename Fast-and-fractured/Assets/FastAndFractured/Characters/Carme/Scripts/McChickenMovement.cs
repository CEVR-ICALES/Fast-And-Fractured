using DG.Tweening;
using UnityEngine;
using Utilities;

namespace FastAndFractured
{
    public class McChickenMovement : MonoBehaviour
    {
        public Vector3 MoveDirection => _moveDirection;
        public bool IsClimbing => _isClimbing;

        public bool IsInCeling => _isInCeiling;

        [Header("Movement Settings")]
        [SerializeField] private float maxSpeed = 30f;
        [SerializeField] private float moveForce = 5f;
        [SerializeField] private float rotationSpeed = 10f;

        [Header("Climbing")]
        [SerializeField] private float climbLerpSpeed = 2f;
        [SerializeField] private float wallClimbSpeed = 3f;
        [SerializeField] private float jumpDuration = 3f;
        [SerializeField] private float jumpHeight = 3f;


        private Vector3 _moveDirection;
        private bool _isClimbing;
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
            _moveDirection = direction;
        }

        private void FixedUpdate()
        {
            _physicsHandler.UpdateGroundState();

            if (_isClimbing)
            {
                Debug.Log("Climbing rn");
            }
            else
            {
                Debug.Log("Not Climbing Rn");
                MoveForward();
                _physicsHandler.LimitRbSpeed(maxSpeed);
                _physicsHandler.ApplyRotation(rotationSpeed);
            }
        }

        private void MoveForward()
        {
           Vector3 finalMoveForce = _moveDirection * moveForce;
            _rb.AddForce(finalMoveForce, ForceMode.Acceleration);
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


