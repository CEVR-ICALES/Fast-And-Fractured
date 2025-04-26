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
            rb.velocity += _customGravity * Time.fixedDeltaTime;
        }
    }
    public void SimulateThrow(Vector3 velocity,Vector3 initialPosition,Vector3 customGravity)
    {
        if (!simulating)
        {
            transform.position = initialPosition;
            if (rb == null)
                rb = GetComponent<Rigidbody>();
            rb.velocity = velocity;
            _customGravity = customGravity;
            simulating = true;
        }
    }
    public void ToogleCollider(bool enable)
    {
        GetComponent<Collider>().enabled = enable;
    }
    private void OnCollisionEnter(Collision collision)
    {
        ContactPoint contactPoint = collision.contacts[0];
        if (gameObject.activeSelf)
        {
            onCollision?.Invoke(contactPoint.point);
            rb.velocity = Vector3.zero;
            simulating = false;
        }
    }
}
