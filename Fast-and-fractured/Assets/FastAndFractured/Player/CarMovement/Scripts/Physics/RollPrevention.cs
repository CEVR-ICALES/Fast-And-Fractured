using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Game {
    public class RollPrevention : MonoBehaviour
    {
        [Header("RollPrevention")]
        [SerializeField] private float _baseDownardForce; //strenght of the force to apply to keep the car on the ground
        [SerializeField] private float _turninDownwardForceMultiplier; //multiplier when turnin (force has to be greater since the car tends to roll over)
        [SerializeField] private float _speedDownwardForceMultiplier; //force based on speed
        [Header("Reference")]
        [SerializeField] private StatsController statsController;

        public void ApplyRollPrevention(Rigidbody rb, float steeringInputMagnitude)
        {
            //calculate downward foce based 
            float speedFactor = rb.velocity.magnitude * statsController.SpeedForceMultiplier ;
            float downWardForce = statsController.BaseDownwardForce + (steeringInputMagnitude * statsController.TurningForceMultiplier * speedFactor);

            //apply force
            Vector3 forceDirection = -rb.transform.up;
            rb.AddForce(forceDirection * downWardForce, ForceMode.Impulse);
            Debug.Log("Roll Prevention" + gameObject.name);
        }
    }
}
