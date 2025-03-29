using DG.Tweening;
using UnityEngine;
using Utilities;

namespace FastAndFractured
{
    public class McChicken : MonoBehaviour
    {

        // ask if maybe it is better to devide the chicken logic in 3 different scripts (base handles its swpawn and scale anim / effects) one for movements and 1 for collisions and maybe the use of physicsbehabiour if we decide to use a statesController for the ckicken

        [Header("Launch Settings")]
        [SerializeField] private float launchTime = 1f;
        [SerializeField] private float jumpHeight = 2f;
        [SerializeField] private float maxLaunchTime = 3f;

        [Header("Scaling")]
        [SerializeField] private float finalScaleDuration = 0.5f;
        [SerializeField] private Vector3 finalScale = Vector3.one;

        [Header("Collision Settings")]
        [SerializeField] private Collider mainCollider;
        [SerializeField] private LayerMask groundLayerMask;
        [SerializeField] private LayerMask wallLayerMask;
        [SerializeField] private float groundCheckOffset = 0.1f;
        [SerializeField] private int maxContacts = 4;

        [Header("Movement Settings")]
        [SerializeField] private float maxSpeed = 30f;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float wallClimbSpeed = 3f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float speedForce = 10f;

        [Header("Wall Climbing")]
        [SerializeField] private float bounceForce = 8f;
        [SerializeField] private float bounceCooldown = 0.5f;
        [SerializeField] private float climbStartDelay = 0.3f;
        [SerializeField] private float footPlacementSpeed = 5f;
        [SerializeField] private float wallDetectionThreshold = 0.7f;

        // state variables
        private bool _hasLanded = false;
        private bool _isGrounded = false;
        private bool _isClimbing = false;
        private bool _isPreparingToClimb = false;
        private bool _isOnCeiling = false;

        // physics references
        private Rigidbody _rb;
        private ContactPoint[] _contactPoints;

        // movement tracking
        private Vector3 _moveDirection;
        private Vector3 _groundNormal;
        private Vector3 _currentWallNormal;
        private Vector3 _wallContactPoint;
        private Quaternion _preClimbRotation;

        // timers
        private float _lastBounceTime;
        private float _lastGroundedTime;
        private ITimer _climbStartTimer;
        private Tween _jumpTween;

        private const float GROUNDED_GRACE_TIME = 0.2f; // avoid false true on air when just slightly off the floor for a moment

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _contactPoints = new ContactPoint[maxContacts];
            mainCollider.enabled = false;
            _rb.isKinematic = true;
        }

        private void FixedUpdate()
        {
            if (!_hasLanded) return;

            UpdateGroundState();

            if (_isPreparingToClimb)
            {
                HandleClimbPreparation();
            }
            else
            {
                HandleMovement();
                ApplyRotation();
                LimitRbSpeed();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_hasLanded && IsInLayerMask(collision.gameObject.layer, groundLayerMask))
            {
                Land();
            }

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
        #region Initialize Chicken

        public void InitializeChicken(Vector3 targetPosition, Vector3 direction)
        {
            _moveDirection = direction.normalized;
            _rb.freezeRotation = true;

            _jumpTween = transform.DOJump(targetPosition, jumpHeight, 1, launchTime)
               .SetEase(Ease.InSine)
               .OnComplete(Land);
        }

        private void Land()
        {
            if (_hasLanded) return;

            mainCollider.enabled = true;
            _rb.freezeRotation = false;
            _rb.isKinematic = false;
            _rb.velocity = Vector3.zero;

            transform.DOScale(finalScale, finalScaleDuration)
                .SetEase(Ease.OutBack)
                .OnComplete(() => _hasLanded = true);
        }

        private void UpdateGroundState()
        {
            _isGrounded = Time.time < _lastGroundedTime + GROUNDED_GRACE_TIME;

            if (_isClimbing && !_isGrounded && Time.time > _lastGroundedTime + 0.5f) // no longer detecting wall as ground so has to start moving forward again
            {
                StopClimbing();
            }
        }

        #endregion

        #region Movement
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

        #endregion

       
        #region WallClimbing and Collision
        private void HandleWallCollision(Collision collision)
        {
            if (Time.time < _lastBounceTime + bounceCooldown) return;

            _lastBounceTime = Time.time;
            _currentWallNormal = collision.contacts[0].normal;
            _wallContactPoint = collision.contacts[0].point;

            Vector3 bounceDir = Vector3.Reflect(_moveDirection, _currentWallNormal);
            _rb.AddForce(bounceDir * bounceForce, ForceMode.Impulse);

            if (Vector3.Dot(_moveDirection, -_currentWallNormal) > wallDetectionThreshold) // if this value is 1 it means weve hit a 90 degree wall comparing it to our movement direcition
            {
                PrepareClimbing();
            }
        }

        private void PrepareClimbing() // so that it doesnt directly jump to the weall but theres a small delay for better visuals
        {
            _isPreparingToClimb = true;
            _preClimbRotation = transform.rotation;

            _climbStartTimer = TimerSystem.Instance.CreateTimer(
                climbStartDelay,
                onTimerDecreaseComplete: StartClimbing
            );
        }

        private void StartClimbing() // needs to be upgraded to not just tp it to the position nbut lerp it and properly rotate the obj couse now it isnt doing it
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

        private void StopClimbing()
        {
            _isClimbing = false;
            _rb.useGravity = true;
            transform.DORotateQuaternion(_preClimbRotation, 0.3f).SetEase(Ease.OutBack);
        }

        #endregion
        private void ApplyRotation()
        {
            Quaternion targetRot = _isClimbing ?
                Quaternion.LookRotation(-_currentWallNormal, Vector3.up) :
                Quaternion.LookRotation(_moveDirection, _isGrounded ? _groundNormal : Vector3.up);

            _rb.rotation = Quaternion.Slerp(_rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
        }

        private void LimitRbSpeed()
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


        private void OnDestroy()
        {
            _jumpTween?.Kill();
            _climbStartTimer?.StopTimer();
        }
    }

}


