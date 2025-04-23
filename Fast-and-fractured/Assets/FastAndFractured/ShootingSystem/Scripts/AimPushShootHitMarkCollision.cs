using System.Collections;
using System.Collections.Generic;
using Enums;
using UnityEngine;
using UnityEngine.Events;
using Utilities;

public class AimPushShootHitMarkCollision : MonoBehaviour
{
    private bool _colliding = false;

    private LayerMask _groundMask = 3;
    private LayerMask _staticMask = 10;
    private LayerMask combinedMask;
    public UnityEvent<Vector3,Vector3,bool> onCollision;
    public Vector3 PositionReference {set=>_positionReferenece=value; }
    private Vector3 _positionReferenece;
    private Rigidbody rb;
    private bool _useCustomGravity = true;
    private Vector3 _customGravity = new Vector3(0,-30,0);
    private bool simulating = false;
    private void Start()
    {
        combinedMask = (1 << _groundMask) | (1 << _staticMask);
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
        _useCustomGravity = false;
    }
    private void FixedUpdate()
    {
        if (_useCustomGravity)
        {
            rb.velocity += _customGravity * Time.fixedDeltaTime;
        }
    }
    private void Update()
    {
        //if (!_colliding)
        //{
        //    Ray downRay = new Ray(transform.position, Vector3.down);
        //    RaycastHit hit;
        //    if (Physics.Raycast(downRay, out hit, Mathf.Infinity, combinedMask))
        //    {
        //        moveMyPosition?.Invoke(hit.point);
        //    }
        //    else
        //    {
        //        Ray upRay = new Ray(transform.position, Vector3.up);
        //        if (Physics.Raycast(upRay, out hit, Mathf.Infinity, combinedMask))
        //        {
        //            moveMyPosition?.Invoke(hit.point);
        //        }
        //    }
        //}
        //transform.position = _positionReferenece;
    }
    public void SimulateThrow(Vector3 velocity,Vector3 initialPosition,Vector3 customGravity)
    {
        if (!simulating)
        {
            transform.position = initialPosition;
            if (rb == null)
                rb = GetComponent<Rigidbody>();
            rb.velocity = velocity;
            _useCustomGravity = true;
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
            _colliding = true;
            onCollision?.Invoke(contactPoint.point, contactPoint.normal, true);
            rb.velocity = Vector3.zero;
            _useCustomGravity = false;
            simulating = false;
        }
    }
    private void OnCollisionExit(Collision collision)
    {
    }
}
