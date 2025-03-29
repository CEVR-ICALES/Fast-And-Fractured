using UnityEngine;
using UnityEngine.EventSystems;
using Utilities;

namespace FastAndFractured
{
    [RequireComponent(typeof(Collider))]
    public class McChickenPhysicsHandler : MonoBehaviour
    {
        [Header("Collision Settings")]
        [SerializeField] private LayerMask groundLayerMask;
        [SerializeField] private LayerMask wallLayerMask;
        [SerializeField] private float groundCheckOffset = 0.1f;
        [SerializeField] private int maxContacts = 4;
        private const float GROUNDED_GRACE_TIME = 0.2f;

        [Header("Wall Detection")]
        [SerializeField] private float bounceForce = 8f;
        [SerializeField] private float bounceCooldown = 0.5f;
        [SerializeField] private float wallDetectionThreshold = 0.7f;

        private Collider _mainCollider;
        private Rigidbody _rb;
        private ContactPoint[] _contactPoints;
        private Vector3 _groundNormal;
        private Vector3 _currentWallNormal;
        private Vector3 _wallContactPoint;
        private float _lastBounceTime;
        private float _lastGroundedTime;
        private bool _isGrounded;
        

        private McChickenMovement _movementHandler;
        private McChicken _core;

        public void Initialize(Rigidbody rb, McChickenMovement movement, McChicken core)
        {
            _contactPoints = new ContactPoint[maxContacts];
            _rb = rb;
            _core = core;
            _movementHandler = movement;
        } 

        private void OnCollisionEnter(Collision collision)
        {
            if (IsInLayerMask(collision.gameObject.layer, wallLayerMask))
            {
                HandleWallCollision(collision);
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
            _currentWallNormal = collision.contacts[0].normal;
            _wallContactPoint = collision.contacts[0].point;

            Vector3 bounceDir = Vector3.Reflect(_movementHandler.MoveDirection, _currentWallNormal);
            _rb.AddForce(bounceDir * bounceForce, ForceMode.Impulse);

            if (Vector3.Dot(_movementHandler.MoveDirection, -_currentWallNormal) > wallDetectionThreshold) // if this value is 1 it means weve hit a 90 degree wall comparing it to our movement direcition
            {
                _movementHandler.PrepareClimbing();
            }
        }

        public void LimitRbSpeed(float maxSpeed)
        {
            if (_rb.velocity.magnitude > (maxSpeed / 3.6f))
            {
                _rb.velocity = _rb.velocity.normalized * (maxSpeed / 3.6f);
            }
        }

        private bool IsInLayerMask(int layer, LayerMask layerMask)
        {
            return (layerMask.value & (1 << layer)) != 0;
        }

        private void CacheGroundContacts(Collision collision)
        {
            if (!IsInLayerMask(collision.gameObject.layer, groundLayerMask)) return;

            int contactCount = collision.GetContacts(_contactPoints);
            if (contactCount == 0) return;

            Vector3 avgNormal = Vector3.zero;
            for (int i = 0; i < contactCount; i++)
            {
                avgNormal += _contactPoints[i].normal;
            }
            _groundNormal = (avgNormal / contactCount).normalized;
            _lastGroundedTime = Time.time;
        }

        public void UpdateGroundState()
        {
            _isGrounded = Time.time < _lastGroundedTime + GROUNDED_GRACE_TIME;

            if (_movementHandler.IsClimbing && !_isGrounded && Time.time > _lastGroundedTime + 0.5f) // no longer detecting wall as ground so has to start moving forward again
            {
                _movementHandler.StopClimbing();
            }
        }

        public void ApplyRotation(float rotationSpeed)
        {
            Quaternion targetRot = _movementHandler.IsClimbing ?
                Quaternion.LookRotation(-_currentWallNormal, Vector3.up) :
                Quaternion.LookRotation(_movementHandler.MoveDirection, _isGrounded ? _groundNormal : Vector3.up);

            _rb.rotation = Quaternion.Slerp(_rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
        }

    }
}

