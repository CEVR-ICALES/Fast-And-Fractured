using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured {
    public class ApplyForceByState : MonoBehaviour
    {
        [Header("Reference")]
        [SerializeField] private StatsController statsController;

        private bool _canApplyRollPrevention = false;
        private Rigidbody _rb;
        private float _steeringInputMagnitude;
        private bool _canApplyAirFricction = false;
        private bool _canApplyCustomGravity = false;

        private const float CUSTOM_GRAVITY = 16.8f;

        private const float AIR_FRICTION = 9.8f;

        private const float FLIP_FORCE = 35000f;

        private const float FLIP_FORCE_OFFSET = 0.4f;

        private void Start()
        {
            _rb = GetComponent<Rigidbody>();
        }
        private void FixedUpdate()
        {
            if(_canApplyRollPrevention)
            {
                ApplyRollPrevention();
            }

            if (_canApplyCustomGravity)
            {
                ApplyCustomGravity();
            }

            if (_canApplyAirFricction)
            {
                ApplyAirFricction();
            }
            
        }

        public void ToggleRollPrevention(bool canApplyRollPrevention, float steeringInputMagnitude)
        {
            _canApplyRollPrevention = canApplyRollPrevention;
            if(canApplyRollPrevention)
            {
                _steeringInputMagnitude = 1;
            }  
        }

        public void ToggleCustomGravity(bool canApplyCustomGravity)
        {
            _canApplyCustomGravity = canApplyCustomGravity;
        }

        public void ToggleAirFriction(bool canApplyAirFriction)
        {
            _canApplyAirFricction = canApplyAirFriction;
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
            _rb.AddForce(Vector3.down * CUSTOM_GRAVITY, ForceMode.Acceleration);
        }

        public void ApplyAirFricction()
        {
            _rb.AddForce(-_rb.transform.forward * AIR_FRICTION, ForceMode.Acceleration);
        }

        public void ApplyFlipStateForce()
        {
            _rb.AddForce((_rb.transform.up + FLIP_FORCE_OFFSET * Vector3.up) * FLIP_FORCE , ForceMode.Impulse);
        }
    }
}
