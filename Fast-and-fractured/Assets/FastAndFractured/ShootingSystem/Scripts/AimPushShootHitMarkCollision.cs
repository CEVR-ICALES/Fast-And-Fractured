using System.Collections;
using System.Collections.Generic;
using Enums;
using UnityEngine;
using UnityEngine.Events;
using Utilities;

public class AimPushShootHitMarkCollision : MonoBehaviour
{
    public UnityEvent<Vector3> onCollision;
    private Rigidbody rb;
    private Vector3 _customGravity = new Vector3(0,-30,0);
    private bool simulating = false;
    private Vector3 _initialPosition = Vector3.zero;
    private Vector3 _initialSpeed;
    private float _simulationTime = 0;
    private void Start()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
    }
    private void FixedUpdate()
    {
        if (simulating)
        {
            _simulationTime += Time.fixedDeltaTime;
            rb.MovePosition(_initialPosition + _initialSpeed * _simulationTime + 0.5f * _customGravity * _simulationTime * _simulationTime);
        }
    }
    public void SimulateThrow(Vector3 velocity,Vector3 initialPosition,Vector3 customGravity)
    {
        if (!simulating)
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
            }
            rb.MovePosition(initialPosition);
            _initialPosition = initialPosition;
            _initialSpeed = velocity;
            _customGravity = customGravity;
            simulating = true;
            _simulationTime = 0;
        }
    }
    private void OnCollisionEnter(Collision collision)
    {
        ContactPoint contactPoint = collision.contacts[0];
        if (gameObject.activeSelf)
        {
            onCollision?.Invoke(contactPoint.point);
            simulating = false;
            rb.MovePosition(contactPoint.point);
        }
    }
}
