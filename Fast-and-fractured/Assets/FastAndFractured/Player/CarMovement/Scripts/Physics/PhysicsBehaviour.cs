using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysicsBehaviour : MonoBehaviour
{
    [Header("Provisional Values")] //values that are not going to be on this script (player stats / car stats)
    public float _maxEndurance;
    public float _endurance;
    [SerializeField] private float _frontalHitAnlgeThreshold; // angle to detect whether the dash hit was frontal or not
    
    public float weight { get; private set; }
    [SerializeField] private float _baseForce;

    public bool isCurrentlyDashing = false;
    private CarMovementController _carMovementController;
    private Rigidbody _rb;


    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();   
        _carMovementController = GetComponent<CarMovementController>();
        weight = _rb.mass;
    }

    private void OnTriggerEnter(Collider other)
    {
        // add on trigger enter logic
    }

    private void OnCollisionEnter(Collision collision)
    {
        if(isCurrentlyDashing)
        {
            PhysicsBehaviour otherComponenPhysicsBehaviours;
            if (collision.gameObject.TryGetComponent(out otherComponenPhysicsBehaviours))
            {
                CancelDash();
                otherComponenPhysicsBehaviours.CancelDash();
                ContactPoint contactPoint = collision.contacts[0];
                Vector3 collisionPos = contactPoint.point;
                Vector3 collisionNormal = contactPoint.normal;
                float otherCarEnduranceFactor = otherComponenPhysicsBehaviours._endurance / otherComponenPhysicsBehaviours._maxEndurance; // calculate current value of the other car endurance
                float otherCarWeight = otherComponenPhysicsBehaviours.weight;
                //detect if the contact was frontal
                if (Vector3.Angle(transform.forward, -collision.gameObject.transform.forward) <= _frontalHitAnlgeThreshold) //frontal hit, to add how we are going to exatly handle who wins the frontal hit
                {
                    // logic to determine forc 
                    float forceToApply = CalculateForceToApplyToOtherCar(otherCarEnduranceFactor, otherCarWeight);
                    otherComponenPhysicsBehaviours.ApplyForce(collisionNormal, collisionPos, forceToApply);

                }
                else
                {
                    float forceToApply = CalculateForceToApplyToOtherCar(otherCarEnduranceFactor, otherCarWeight);
                    otherComponenPhysicsBehaviours.ApplyForce(-collisionNormal, collisionPos, forceToApply);
                }
            }
        
        }
        

    }

    public void CancelDash()
    {
        if(isCurrentlyDashing)
        {
            _carMovementController.CancleDash();
        }
    }

    public void ApplyForce(Vector3 forceDirection, Vector3 forcePoint, float forceToApply)
    {
        _rb.AddForceAtPosition(forceDirection * forceToApply, forcePoint, ForceMode.Impulse);
    }

    public void AddForce(Vector3 force, ForceMode forceMode)
    {
        _rb.AddForce(force, forceMode);
    }

    public void AddTorque(Vector3 torque, ForceMode forceMode)
    {
        _rb.AddTorque(torque, forceMode);
    }

    private float CalculateForceToApplyToOtherCar(float oCarEnduranceFactor, float oCarWeight)
    {
        float force = _baseForce * (1 - oCarEnduranceFactor) * (oCarWeight / 20); // this 20 value should become a variable as to how important the weight is going to be, the bigger the number the less important the weight
        Debug.Log(force);
        return force;
    }

    public void LimitRigidBodySpeed(float maxSpeed)
    {
        Vector3 clampedVelocity = _rb.velocity;

        if (clampedVelocity.magnitude > (maxSpeed / 3.6f))
        {
            clampedVelocity = clampedVelocity.normalized * (maxSpeed / 3.6f);
            _rb.velocity = clampedVelocity;
        }
    }

    public void LimitRigidBodyRotation(float maxAngularVelocity)
    {
        if (_rb.angularVelocity.magnitude > maxAngularVelocity)
        {
            _rb.angularVelocity = _rb.angularVelocity.normalized * maxAngularVelocity;
        }
    }

    public void SetKinematic(bool isKinematic)
    {
        _rb.isKinematic = isKinematic;
    }

    public void BlockRigidBodyRotations()
    {
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    public void UnblockRigidBodyRotations()
    {
        _rb.constraints = RigidbodyConstraints.None;
    }



}
