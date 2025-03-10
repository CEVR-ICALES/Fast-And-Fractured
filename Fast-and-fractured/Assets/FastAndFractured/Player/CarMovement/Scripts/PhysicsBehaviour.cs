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
        PhysicsBehaviour otherComponenPhysicsBehaviours;
        if(collision.gameObject.TryGetComponent(out otherComponenPhysicsBehaviours))
        {
            ContactPoint contactPoint = collision.contacts[0];
            Vector3 collisionPos = contactPoint.point;
            Vector3 collisionNormal = contactPoint.normal;
            float otherCarEnduranceFactor = otherComponenPhysicsBehaviours._endurance / otherComponenPhysicsBehaviours._maxEndurance; // calculate current value of the other car endurance
            float otherCarWeight = otherComponenPhysicsBehaviours.weight;
            //detect if the contact was frontal
            if (Vector3.Angle(transform.forward, -collision.gameObject.transform.forward) <= _frontalHitAnlgeThreshold)
            {
                // logic to determine forc 
                float forceToApply = CalculateForceToApplyToOtherCar(otherCarEnduranceFactor, otherCarWeight);
                ApplyForce(collisionNormal, collisionPos, forceToApply);
               
            } else
            {
                float forceToApply = CalculateForceToApplyToOtherCar(otherCarEnduranceFactor, otherCarWeight);
                ApplyForce(collisionNormal, collisionPos, forceToApply);
            }
        }
        
       

    }

    public void ApplyForce(Vector3 forceDirection, Vector3 forcePoint, float forceToApply)
    {
        _rb.AddForceAtPosition(forceDirection * forceToApply, forcePoint, ForceMode.Impulse);
    }


    private float CalculateForceToApplyToOtherCar(float oCarEnduranceFactor, float oCarWeight)
    {
        float force = _baseForce * (1 - oCarEnduranceFactor) * (oCarWeight / 10);
        Debug.Log(force);
        return force;
    }

    public void BlockRigidBodyRotations()
    {

    }

    public void UnblockRigidBodyRotations()
    {

    }


    

}
