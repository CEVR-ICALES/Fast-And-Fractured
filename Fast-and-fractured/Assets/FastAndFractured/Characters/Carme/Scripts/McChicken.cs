using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;

public class McChicken : MonoBehaviour
{
    [Header("Prabole Settings")]
    [SerializeField] private float launchTime;
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
    private Quaternion _lockedRotation;

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

    public void InitializeChicken(Vector3 direction)
    {
        if(direction == Vector3.zero)
            direction = transform.forward;

        _rb.freezeRotation = true;
        _lockedRotation = Quaternion.LookRotation(direction.normalized);


        _fallTimer = TimerSystem.Instance.CreateTimer(launchTime, onTimerDecreaseComplete: ForceLanding);
    }


    private void Land()
    {
        if (_hasLanded) return;
        _fallTimer?.StopTimer();
        _hasLanded = true;

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
