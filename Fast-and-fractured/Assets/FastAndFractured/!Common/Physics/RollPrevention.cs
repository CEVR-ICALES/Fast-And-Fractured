using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured {
    public class RollPrevention : MonoBehaviour
    {
        [Header("RollPrevention")]
        [SerializeField] private float _baseDownardForce; //strenght of the force to apply to keep the car on the ground
        [SerializeField] private float _turninDownwardForceMultiplier; //multiplier when turnin (force has to be greater since the car tends to roll over)
        [SerializeField] private float _speedDownwardForceMultiplier; //force based on speed
        [Header("Reference")]
        [SerializeField] private StatsController statsController;

        private bool _canApplyRollPrevention = false;
        private Rigidbody _rb;
        private float _steeringInputMagnitude;


        private void FixedUpdate()
        {
            if(_canApplyRollPrevention)
            {
                ApplyRollPrevention();
            }
        }

        public void ToggleRollPrevention(bool canApplyRollPrevention, Rigidbody rb, float steeringInputMagnitude)
        {
            _canApplyRollPrevention = canApplyRollPrevention;
            if(canApplyRollPrevention)
            {
                if (_rb == null)
                    _rb = rb;
                _steeringInputMagnitude = steeringInputMagnitude;
            }  
        }


        public void ApplyRollPrevention()
        {
            //calculate downward foce based 
            float speedFactor = _rb.velocity.magnitude * statsController.SpeedForceMultiplier ;
            float downWardForce = statsController.BaseDownwardForce + (_steeringInputMagnitude * statsController.TurningForceMultiplier * speedFactor);

            //apply force
            Vector3 forceDirection = -_rb.transform.up;
            _rb.AddForce(forceDirection * downWardForce, ForceMode.Impulse);
            //Debug.Log("Roll Prevention" + gameObject.name);
        }
    }
}
