using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured {
    public class RollPrevention : MonoBehaviour
    {
        [Header("Reference")]
        [SerializeField] private StatsController statsController;

        private bool _canApplyRollPrevention = false;
        private Rigidbody _rb;
        private float _steeringInputMagnitude;
        private bool _canApplyAirFricction = false;
        private bool _canApplyCustomGravity = false;

        private const float CUSTOM_GRAVITY = 


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

        public void ApplyCustomGravity()
        {
            _rb.AddForce(Vector3.down * )
        }
    }
}
