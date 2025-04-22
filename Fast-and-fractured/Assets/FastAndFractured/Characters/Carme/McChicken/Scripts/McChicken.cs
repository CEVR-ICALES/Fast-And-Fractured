using DG.Tweening;
using UnityEngine;
using Utilities;

namespace FastAndFractured
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public class McChicken : MonoBehaviour
    {
        [Header("Launch Settings")]
        [SerializeField] private float launchTime = 1f;
        [SerializeField] private float jumpHeight = 2f;

        [Header("Scaling")]
        [SerializeField] private float finalScaleDuration = 0.5f;
        [SerializeField] private Vector3 finalScale = Vector3.one;

        private Collider _mainCollider;

        // state variables
        private bool _hasLanded = false;

        // physics references
        private Rigidbody _rb;
        private McChickenMovement _movementHandler;
        private McChickenPhysicsHandler _phyisicsHandler;

        // movement tracking
        private Vector3 _moveDirection;

        // timers
        private ITimer _climbStartTimer;
        private Tween _jumpTween;
        private const int NUMBER_OF_JUMPS = 1;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _mainCollider = GetComponent<Collider>();
            _movementHandler = GetComponent<McChickenMovement>();
            _phyisicsHandler = GetComponent<McChickenPhysicsHandler>();
            _mainCollider.enabled = false;
            _rb.isKinematic = true;

            _movementHandler.Initialize(_rb, _phyisicsHandler);
            _phyisicsHandler.Initialize(_rb, _movementHandler);
        }

        public void InitializeChicken(Vector3 targetPosition, Vector3 direction)
        {
            _moveDirection = direction.normalized;
            _rb.freezeRotation = true;

            _jumpTween = transform.DOJump(targetPosition, jumpHeight, NUMBER_OF_JUMPS, launchTime)
               .SetEase(Ease.InSine)
               .OnComplete(Land);
        }

        private void Land()
        {
            if (_hasLanded) return;

            _mainCollider.enabled = true;
            _rb.freezeRotation = false;
            _rb.isKinematic = false;
            _rb.velocity = Vector3.zero;

            transform.DOScale(finalScale, finalScaleDuration)
                .SetEase(Ease.OutBack)
                .OnComplete(() => _movementHandler.StartMoving(_moveDirection));
        }

        private void OnDestroy()
        {
            _jumpTween?.Kill();
            _climbStartTimer?.StopTimer();
        }
    }

}





