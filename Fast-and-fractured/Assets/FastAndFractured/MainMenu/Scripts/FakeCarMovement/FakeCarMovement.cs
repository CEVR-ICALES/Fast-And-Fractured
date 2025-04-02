using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;

public class FakeCarMovement : MonoBehaviour
{
    private Rigidbody _rb;
    [SerializeField] private float forceToApply;
    [SerializeField] private float forceDuration;


    private void OnEnable()
    {
        _rb = GetComponent<Rigidbody>();
        //TimerSystem.Instance.CreateTimer(forceToApply)
        _rb.AddForce(transform.forward * forceToApply, ForceMode.Impulse);
    }
}
