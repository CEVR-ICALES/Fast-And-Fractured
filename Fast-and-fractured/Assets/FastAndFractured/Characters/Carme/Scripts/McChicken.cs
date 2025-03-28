using DG.Tweening;
using FastAndFractured;
using UnityEngine;
using Utilities;

public class McChicken : MonoBehaviour
{
    [Header("Prabole Settings")]
    [SerializeField] private float launchTime;
    [SerializeField] private float jumpHeight;
    [SerializeField] private LayerMask groundLayerMask;
    [SerializeField] private float maxLaunchTime;

    [Header("Scaling")]
    [SerializeField] private float finalScaleDuration;
    [SerializeField] private Vector3 finalScale;

    [Header("Colliders")]
    [SerializeField] private Collider mainCollider;

    [Header("Movement")]
    [SerializeField] private float maxSpeed;
    [SerializeField] private float speedForce;

    [Header("Pushing")]

    //private PhysicsBehaviour _physicsBehaviour;
    private Rigidbody _rb;
    private bool _hasLanded = false;
    private Quaternion _lockedRotation;
    private Tween _jumpTween; 

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        //_physicsBehaviour = GetComponent<PhysicsBehaviour>();
        mainCollider.enabled = false;
    }

    private void FixedUpdate()
    {
        if (!_hasLanded) return;

        LimitRbSpeed();
        Move();
        //_physicsBehaviour.LimitRigidBodySpeed(maxSpeed);

    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!_hasLanded)
        {
            if ((groundLayerMask.value & (1 << collision.gameObject.layer)) != 0) // layer detection code i got from a forum
            {
                Land();
            }
        }
    }

    public void InitializeChicken(Vector3 targetPosition, Vector3 direction)
    {

        _rb.freezeRotation = true;
        _lockedRotation = Quaternion.LookRotation(direction.normalized);

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

    private void Move()
    {
        _rb.AddForce(transform.forward * speedForce, ForceMode.Acceleration);
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
