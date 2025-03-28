using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Utilities;

public class McChicken : MonoBehaviour
{
    [Header("Prabole Settings")]
    [SerializeField] private float launchTime;
    [SerializeField] private float jumpHeight;
    [SerializeField] private LayerMask groundLayerMask;

    [Header("Scaling")]
    [SerializeField] private float biggerScaleDuration;
    [SerializeField] private float finalScaleDuration;
    [SerializeField] private Vector3 biggerScale;
    [SerializeField] private Vector3 finalScale;

    [Header("Colliders")]
    [SerializeField] private Collider mainCollider;

    private Rigidbody _rb;
    private bool _hasLanded = false;
    private ITimer _fallTimer;
    private float _objectBottomOffset;
    private Vector3 _targetPosition;
    private Quaternion _lockedRotation;
    private Tween _jumpTween; 

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        mainCollider.enabled = false;
    }

    private void FixedUpdate()
    {
        if (_hasLanded) return;

        //_rb.MoveRotation(_lockedRotation);
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
           .OnComplete(ForceLanding);

        _fallTimer = TimerSystem.Instance.CreateTimer(launchTime, onTimerDecreaseComplete: ForceLanding);
    }


    private void Land()
    {
        if (_hasLanded) return;
        _fallTimer?.StopTimer();
        _hasLanded = true;
        mainCollider.enabled = true;
        _rb.velocity = Vector3.zero;

        transform.DOScale(biggerScale, biggerScaleDuration)
            .OnComplete(() =>
            {
                transform.DOScale(finalScale, finalScaleDuration)
                .OnComplete(() =>
                {

                });
            });
        
    }

    private void ForceLanding()
    {
        if(!_hasLanded) Land();
    }

    private void OnDestroy()
    {
        _fallTimer?.StopTimer();
    }

}
