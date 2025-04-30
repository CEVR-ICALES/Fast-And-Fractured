using UnityEngine;
using Utilities;
using Enums;

namespace FastAndFractured
{
    [RequireComponent(typeof(Collider))]
    public class McChickenPhysicsHandler : MonoBehaviour
    {
        public bool IsGrounded => _isGrounded;
        public Vector3 GroundNormal => _groundNormal;

        [Header("Collision Settings")]
        [SerializeField] private LayerMask groundLayerMask;
        [SerializeField] private LayerMask wallLayerMask;
        [SerializeField] private LayerMask charLayerMask;
        [SerializeField] private float bounceForce = 8f;
        [SerializeField] private float bounceCooldown = 0.5f;
        [SerializeField] private float bounceDuration;
        [SerializeField] private float climbingDotThreshold = 0.7f;
        [SerializeField] private float chickenForce;
        [SerializeField] private float charCollisionDuration;
        private const float GROUNDED_GRACE_TIME = 0.2f;

        private Rigidbody _rb;
        private McChickenMovement _movementHandler;
        private Vector3 _groundNormal;
        private Vector3 _wallNormal;
        private float _lastBounceTime;
        private float _lastGroundedTime;
        private bool _isGrounded;


        private ITimer _bounceTimer;
        private ITimer _charCollisionTimer;
        private const float ENDURANCE_DAMAGE_ON_COLLISION = 1f;
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
                    // TODO handle collision when climbing
                }
            }

            if(IsInLayerMask(collision.gameObject.layer, charLayerMask))
            {
                HandleCharCollision(collision);
            }
            CacheGroundContacts(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            CacheGroundContacts(collision);
        }

        private void HandleWallCollision(Collision collision)
        {
            if (Time.time < _lastBounceTime + bounceCooldown || Vector3.Dot(_movementHandler.MoveDirection, -collision.contacts[0].normal) < climbingDotThreshold) return;

            _lastBounceTime = Time.time;
            _wallNormal = collision.contacts[0].normal;
            Vector3 bounceDir = Vector3.Reflect(_movementHandler.MoveDirection, _wallNormal);
            ChickenClimbeable climbeable;
            _rb.AddForce(bounceDir * bounceForce, ForceMode.Impulse);

            if(collision.gameObject.TryGetComponent(out climbeable))
            {
                _bounceTimer = TimerSystem.Instance.CreateTimer(bounceDuration, TimerDirection.DECREASE, onTimerDecreaseComplete: () =>
                {
                    NotifyStartClimbing(climbeable.GetLandingPoint(transform.position));
                });
            }
        }

        private void HandleCharCollision(Collision collision)
        {
            PhysicsBehaviour physicsBehaviour;
            if(collision.gameObject.TryGetComponent(out physicsBehaviour))
            {
                _rb.isKinematic = true;
                _charCollisionTimer = TimerSystem.Instance.CreateTimer(bounceDuration, TimerDirection.DECREASE, onTimerDecreaseComplete: () =>
                {
                    _rb.isKinematic = false;
                });
                Vector3 collisionNormal = -collision.contacts[0].normal;
                Vector3 finalForce = collisionNormal * chickenForce;
                physicsBehaviour.AddForce(finalForce, ForceMode.Impulse);
            }
            if(collision.gameObject.TryGetComponent(out StatsController statsController))
            {
                statsController.TakeEndurance(ENDURANCE_DAMAGE_ON_COLLISION,false,gameObject);
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
            _isGrounded = true;
            _lastGroundedTime = Time.time;
              
        }

        public void UpdateGroundState()
        {
            _isGrounded = Time.time < _lastGroundedTime + GROUNDED_GRACE_TIME;
        }

        public void ApplyRotation(float rotationSpeed)
        {
            Vector3 currentEuler = _rb.rotation.eulerAngles;

            // calculate only the X rotation we want
            float slopeAngle = Vector3.Angle(_groundNormal, Vector3.up);
            float slopeSign = Mathf.Sign(Vector3.Dot(transform.right, _groundNormal));
            float targetXRotation = Mathf.Clamp(slopeAngle * slopeSign, -45f, 45f);

            // create new rotation (only X changes, Y/Z remain unchanged)
            Quaternion targetRot = Quaternion.Euler(
                targetXRotation,  
                currentEuler.y,   
                currentEuler.z    
            );

            // apply the rotation
            _rb.MoveRotation(Quaternion.Slerp(
                _rb.rotation,
                targetRot,
                rotationSpeed * Time.fixedDeltaTime
            ));
        }

    }
}

