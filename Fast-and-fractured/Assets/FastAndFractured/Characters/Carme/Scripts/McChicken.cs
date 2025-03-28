using DG.Tweening;
using UnityEngine;
using Utilities;

namespace FastAndFractured
{
    public class McChicken : MonoBehaviour
    {

        // ask if maybe it is better to devide the chicken logic in 3 different scripts (base handles its swpawn and scale anim / effects) one for movements and 1 for collisions and maybe the use of physicsbehabiour if we decide to use a statesController for the ckicken

        [Header("Prabole Settings")]
        [SerializeField] private float launchTime;
        [SerializeField] private float jumpHeight;
        [SerializeField] private float maxLaunchTime;
        private bool _hasLanded = false;
        private Tween _jumpTween;
        private Quaternion _lockedRotation;

        [Header("Scaling")]
        [SerializeField] private float finalScaleDuration;
        [SerializeField] private Vector3 finalScale;

        [Header("Colliders")]
        [SerializeField] private Collider mainCollider;
        [SerializeField] private float groundCheckOffset = 0.1f;
        [SerializeField] private int maxContacts = 4;
        private ContactPoint[] _contactPoints;
        private float _lastBounceTime;

        [Header("Movement")]
        [SerializeField] private float maxSpeed;
        [SerializeField] private float speedForce;
        [SerializeField] private float moveSpeed;
        [SerializeField] private float wallClimbSpeed;
        [SerializeField] private float rotationSpeed;
        [SerializeField] private LayerMask groundLayerMask;
        [SerializeField] private LayerMask wallLayerMask;
        private Vector3 _moveDirection;
        private bool _isClimbing = false;
        private bool _isOnCeiling = false;
        private bool _isGrounded = false;
        private Vector3 _groundNormal;
        private Vector3 _currentWallNormal;
        private float _lastGroundedTime;
        private const float GROUNDED_REFRESH_RATE = 0.2f; //avoid false true air states when slightly off the floor

        [Header("BounceSettings")]
        [SerializeField] private float bounceForce;
        [SerializeField] private float bounceCooldown;


        [Header("Pushing")]

        //private PhysicsBehaviour _physicsBehaviour;
        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            mainCollider.enabled = false;
            
        }

        private void FixedUpdate()
        {
            if (!_hasLanded) return;

            UpdateGroundState();
            HandleMovement();
            ApplyRotation();
            LimitRbSpeed();
        }

        private void OnCollisionEnter(Collision collision)
        {
            //if (!_hasLanded)
            //{
            //    if ((groundLayerMask.value & (1 << collision.gameObject.layer)) != 0) // layer detection code i got from a forum
            //    {
            //        Land();
            //    }
            //}

            if ((wallLayerMask.value & (1 << collision.gameObject.layer)) != 0)
            {
                HandleWallCollision(collision);
            }
            CacheGroundContacts(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            CacheGroundContacts(collision);
        }

        public void InitializeChicken(Vector3 targetPosition, Vector3 direction)
        {

            _rb.freezeRotation = true;
            _lockedRotation = Quaternion.LookRotation(direction.normalized);
            _moveDirection = transform.forward;
            _contactPoints = new ContactPoint[maxContacts];

            _jumpTween = transform.DOJump(targetPosition, jumpHeight, 1, launchTime)
               .SetEase(Ease.InSine)
               .OnComplete(Land);

        }


        private void Land()
        {
            if (_hasLanded) return;
            mainCollider.enabled = true;
            _rb.velocity = Vector3.zero;

            transform.DOScale(finalScale, finalScaleDuration)
                .SetEase(Ease.OutBack)
                .OnComplete(() =>
                {
                    _hasLanded = true;
                });

        }

        private void UpdateGroundState()
        {
            _isGrounded = Time.time < _lastGroundedTime + GROUNDED_REFRESH_RATE;
        }


        private void HandleMovement()
        {
            //_rb.AddForce(transform.forward * speedForce, ForceMode.Acceleration);

            if (_isClimbing)
            {
                // wall climbing movement
                Vector3 climbDir = Vector3.ProjectOnPlane(Vector3.up, _currentWallNormal).normalized;
                _rb.velocity = climbDir * wallClimbSpeed + _moveDirection * 0.5f;

                // check for ceiling transition
                CheckCeilingTransition();
            }
            else if (!_isGrounded)
            {
                // falling with forward momentum
                _rb.AddForce(Physics.gravity * 2f + _moveDirection * 2f, ForceMode.Acceleration);
            }
            else
            {
                // standard grounded movement
                _rb.velocity = _moveDirection * moveSpeed;
            }
        }

        private void ApplyRotation()
        {
            Quaternion targetRot = _isClimbing ?
            Quaternion.LookRotation(-_currentWallNormal, Vector3.up) :
            Quaternion.LookRotation(_moveDirection, _isGrounded ? _groundNormal : Vector3.up);

            _rb.rotation = Quaternion.Slerp(_rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
        }

        private void CheckCeilingTransition()
        {
            _isOnCeiling = Vector3.Dot(_currentWallNormal, Vector3.down) > 0.7f;
            if (_isOnCeiling) _isClimbing = false;
        }

        private void CacheGroundContacts(Collision collision)
        {
            if ((groundLayerMask.value & (1 << collision.gameObject.layer)) == 0) return;

            int contactCount = collision.GetContacts(_contactPoints);
            if (contactCount == 0) return;

            // Calculate average ground normal
            Vector3 avgNormal = Vector3.zero;
            for (int i = 0; i < contactCount; i++)
            {
                avgNormal += _contactPoints[i].normal;
            }
            _groundNormal = (avgNormal / contactCount).normalized;
            _lastGroundedTime = Time.time;
        }

        private void HandleWallCollision(Collision collision)
        {
            if (Time.time < _lastBounceTime + bounceCooldown) return;

            _lastBounceTime = Time.time;
            _currentWallNormal = collision.contacts[0].normal;

            // bounce effect
            Vector3 bounceDir = Vector3.Reflect(_moveDirection, _currentWallNormal);
            _rb.AddForce(bounceDir * bounceForce, ForceMode.Impulse);

            // check if we should start climbing after bounce
            if (Vector3.Dot(_moveDirection, -_currentWallNormal) > 0.7f)
            {
                _isClimbing = true;
            }
        }

        private void LimitRbSpeed()
        {
            Vector3 clampedVelocity = _rb.velocity;

            if (clampedVelocity.magnitude > (maxSpeed / 3.6f))
            {
                clampedVelocity = clampedVelocity.normalized * (maxSpeed / 3.6f);
                _rb.velocity = clampedVelocity;
            }
        }

    }
}


