using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;

public class McChicken : MonoBehaviour
{
    [Header("Prabole Settings")]
    [SerializeField] private float launchForce;
    [SerializeField] private float gravityScale;
    [SerializeField] private float maxFallTime;

    [Header("Scaling")]
    [SerializeField] private float scaleDuration;
    [SerializeField] private Vector3 finalScale;

    [Header("Colliders")]
    [SerializeField] private Collider footCollider;
    [SerializeField] private Collider mainCollider;

    private Rigidbody _rb;
    private bool _hasLanded = false;
    private ITimer _fallTimer;
    private Quaternion _lockedRotation;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        footCollider.enabled = true;
        mainCollider.enabled = false;
    }

    private void FixedUpdate()
    {

    }

    private void OnTriggerEnter(Collider other)
    {

    }

    public void InitializeChicken(Vector3 direction)
    {
        if(direction == Vector3.zero)
            direction = transform.forward;

        _rb.freezeRotation = true;
        _lockedRotation = Quaternion.LookRotation(direction);

        Vector3 force = direction.normalized + Vector3.up * 0.5f * launchForce;
        _rb.AddForce(force, ForceMode.Impulse);

        _fallTimer = TimerSystem.Instance.CreateTimer(maxFallTime, onTimerDecreaseComplete: ForceLanding);
    }

    private void Land()
    {

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
