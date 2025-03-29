using UnityEngine;
using Utilities;
using Enums;

namespace FastAndFractured
{
    [RequireComponent(typeof(Collider))]
    public class McChickenPhysicsHandler : MonoBehaviour
    {
        [Header("Collision Settings")]
        [SerializeField] private LayerMask groundLayerMask;
        [SerializeField] private LayerMask wallLayerMask;
        [SerializeField] private float bounceForce = 8f;
        [SerializeField] private float bounceCooldown = 0.5f;
        [SerializeField] private float groundCheckOffset = 0.1f;
        [SerializeField] private float bounceDuration;
        private const float GROUNDED_GRACE_TIME = 0.2f;

        private Rigidbody _rb;
        private McChickenMovement _movementHandler;
        private Vector3 _groundNormal;
        private Vector3 _wallNormal;
        private float _lastBounceTime;
        private float _lastGroundedTime;
        private bool _isGrounded;

        private ITimer _bounceTimer;
        public void Initialize(Rigidbody rb, McChickenMovement movement)
        {
            _rb = rb;
            _movementHandler = movement;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsInLayerMask(collision.gameObject.layer, wallLayerMask))
            {
                if(!_movementHandler.IsInCeling)
                {
                    HandleWallCollision(collision);
                } else
                {

                }
            }
            CacheGroundContacts(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            CacheGroundContacts(collision);
        }

        private void HandleWallCollision(Collision collision)
        {
            if (Time.time < _lastBounceTime + bounceCooldown) return;

            _lastBounceTime = Time.time;
            _wallNormal = collision.contacts[0].normal;
            Vector3 bounceDir = Vector3.Reflect(_movementHandler.MoveDirection, _wallNormal);
            Climbeable climbeable;
            _rb.AddForce(bounceDir * bounceForce, ForceMode.Impulse);

            if(collision.gameObject.TryGetComponent(out climbeable))
            {
                _bounceTimer = TimerSystem.Instance.CreateTimer(bounceDuration, TimerDirection.DECREASE, onTimerDecreaseComplete: () =>
                {
                    NotifyStartClimbing(climbeable.GetLandingPoint(transform.position));
                });
            }
        }

        private void NotifyStartClimbing(Vector3 climbPoint)
        {
            _movementHandler.PrepareClimbing(climbPoint);
        }

        private bool IsInLayerMask(int layer, LayerMask layerMask)
        {
            return (layerMask.value & (1 << layer)) != 0;
        }

        private void CacheGroundContacts(Collision collision)
        {
            if (_movementHandler.IsClimbing) return;

            if (!IsInLayerMask(collision.gameObject.layer, groundLayerMask)) return;
            _groundNormal = collision.contacts[0].normal;
            _lastGroundedTime = Time.time;
        }

        public void UpdateGroundState()
        {
            _isGrounded = Time.time < _lastGroundedTime + GROUNDED_GRACE_TIME;
        }

        public void ApplyRotation(float rotationSpeed)
        {
            Quaternion targetRot = Quaternion.LookRotation(_movementHandler.MoveDirection, _isGrounded ? _groundNormal : Vector3.up);
            _rb.rotation = Quaternion.Slerp(_rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
        }

        public void LimitRbSpeed(float maxSpeed)
        {
            if(_rb.velocity.magnitude > maxSpeed / 3.6f)
            {
                _rb.velocity = _rb.velocity.normalized * maxSpeed;
            }
        }

    }
}

