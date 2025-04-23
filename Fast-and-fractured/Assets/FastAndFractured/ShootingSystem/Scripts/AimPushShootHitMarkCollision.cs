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
    public UnityEvent<Vector3> moveMyPosition;
    public UnityEvent<Vector3,Vector3,bool> onCollision;
    public Vector3 PositionReference {set=>_positionReferenece=value; }
    private Vector3 _positionReferenece;
    private void Start()
    {
        combinedMask = (1 << _groundMask) | (1 << _staticMask);
    }
    private void Update()
    {
        if (!_colliding)
        {
            Ray downRay = new Ray(transform.position, Vector3.down);
            RaycastHit hit;
            if (Physics.Raycast(downRay, out hit, Mathf.Infinity, combinedMask))
            {
                moveMyPosition?.Invoke(hit.point);
            }
            else
            {
                Ray upRay = new Ray(transform.position, Vector3.up);
                if (Physics.Raycast(upRay, out hit, Mathf.Infinity, combinedMask))
                {
                    moveMyPosition?.Invoke(hit.point);
                }
            }
        }
        transform.position = _positionReferenece;
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
        }
    }
    private void OnCollisionExit(Collision collision)
    {
        if (gameObject.activeSelf)
        {
            _colliding = false;
            onCollision?.Invoke(Vector3.zero, Vector3.zero, true);
        }
    }
}
