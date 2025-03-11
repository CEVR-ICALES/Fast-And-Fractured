using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RollPrevention : MonoBehaviour
{
    [Header("RollPrevention")]
    [SerializeField] private float baseDownardForce; //strenght of the force to apply to keep the car on the ground
    [SerializeField] private float turninDownwardForceMultiplier; //multiplier when turnin (force has to be greater since the car tends to roll over)
    [SerializeField] private float speedDownwardForceMultiplier; //force based on speed

    public void ApplyRollPrevention(Rigidbody rb, float steeringInputMagnitude) 
    {
        //calculate downward foce based 
        float speedFactor = rb.velocity.magnitude * speedDownwardForceMultiplier;
        float downWardForce = baseDownardForce + (steeringInputMagnitude * turninDownwardForceMultiplier * speedFactor);

        //apply force
        Vector3 forceDirection = -rb.transform.up;
        rb.AddForce(forceDirection * downWardForce, ForceMode.Impulse);
    }
}
