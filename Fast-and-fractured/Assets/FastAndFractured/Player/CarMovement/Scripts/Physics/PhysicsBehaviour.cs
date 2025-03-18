using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class PhysicsBehaviour : MonoBehaviour
    {
        [SerializeField] private float isMovingThreshold;
        public bool IsCurrentlyDashing = false;
        public bool HasBeenPushed { get => _hasBeenPushed; }
        private bool _hasBeenPushed = false;
        const float SPEED_TO_METER_PER_SECOND = 3.6f;

        [Header("Reference")]
        [SerializeField] private StatsController statsController;
        public Rigidbody Rb { get => _rb; }
        private Rigidbody _rb;
        private CarMovementController _carMovementController;
        public StatsController StatsController { get => statsController;}

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _carMovementController = GetComponent<CarMovementController>();
            _rb.mass = StatsController.Weight;
        }

        private void OnTriggerEnter(Collider other)
        {
            // add on trigger enter logic
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsCurrentlyDashing)
            {
                PhysicsBehaviour otherComponenPhysicsBehaviours;
                if (collision.gameObject.TryGetComponent(out otherComponenPhysicsBehaviours))
                {
                    CancelDash();
                    otherComponenPhysicsBehaviours.CancelDash();
                    ContactPoint contactPoint = collision.contacts[0];
                    Vector3 collisionPos = contactPoint.point;
                    Vector3 collisionNormal = contactPoint.normal;
                    float otherCarEnduranceFactor = otherComponenPhysicsBehaviours.StatsController.Endurance / otherComponenPhysicsBehaviours.StatsController.MaxEndurance; // calculate current value of the other car endurance
                    float otherCarWeight = otherComponenPhysicsBehaviours.StatsController.Weight;
                    float forceToApply;
                    //detect if the contact was frontal
                    if (Vector3.Angle(transform.forward, -collision.gameObject.transform.forward) <= statsController.FrontalHitAnlgeThreshold) //frontal hit, to add how we are going to exatly handle who wins the frontal hit
                    {
                        if(otherComponenPhysicsBehaviours.IsCurrentlyDashing)
                        {
                            if (DecideIfWinsFrontalCollision(otherCarEnduranceFactor, otherCarWeight, otherComponenPhysicsBehaviours.StatsController.EnduranceImportanceWhenColliding, otherComponenPhysicsBehaviours.Rb.velocity.magnitude))
                            {
                                forceToApply = CalculateForceToApplyToOtherCarWhenFrontalCollision(otherCarEnduranceFactor, otherCarWeight);
                            } else
                            {
                                forceToApply = 0f; //lost frontal hit so u apply no force (the other car will sliughtly bounce by its own)
                            }
                            
                        } else
                        {
                            forceToApply = CalculateForceToApplyToOtherCar(otherCarEnduranceFactor, otherCarWeight);
                        }
                    }
                    else
                    {
                        forceToApply = CalculateForceToApplyToOtherCar(otherCarEnduranceFactor, otherCarWeight);
                    }

                    otherComponenPhysicsBehaviours.ApplyForce(-collisionNormal, collisionPos, forceToApply);
                    otherComponenPhysicsBehaviours.OnCarHasBeenPushed();
                }

            }


        }

        public void CancelDash()
        {
            if (IsCurrentlyDashing)
            {
                _carMovementController.CancelDash();
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
            if(oCarEnduranceFactor == 0)
            {
                oCarEnduranceFactor = 0.95f; // provisional
            }
            float force = statsController.BaseForce * (1 - oCarEnduranceFactor) * (oCarWeight / 20); // this 20 value should become a variable as to how important the weight is going to be, the bigger the number the less important the weight
            Debug.Log(force);
            return force;
        }

        private float CalculateForceToApplyToOtherCarWhenFrontalCollision(float oCarEnduranceFactor, float oCarWeight)
        {
            if(oCarEnduranceFactor == 0)
            {
                oCarEnduranceFactor = 0.95f;
            }
            float baseFrontalForce = statsController.BaseForce * 2f; // double the base force for frontal collisions

            // factor in the other car's endurance and weight
            float enduranceWeightFactor = (1 - oCarEnduranceFactor) * (oCarWeight / 20f); // if we end up using this formula this 20 sholb become a "weightImportanceFactor varialbe in the statsController o physicsBehaviour"

            // factor in the speed of the other car (higher speed = more force)
            float speedFactor = _rb.velocity.magnitude / SPEED_TO_METER_PER_SECOND;

            float force = baseFrontalForce * enduranceWeightFactor * speedFactor;

            Debug.Log("Frontal collision force: " + force);
            return force;
        }

        private bool DecideIfWinsFrontalCollision(float oCarEnduranceFactor, float oCarWeight, float oEnduranceImportance, float oCurrentRbSpeed)
        {
            if(CalculateCurrentSimulationWeight((statsController.MaxEndurance / statsController.Endurance), statsController.Weight, statsController.EnduranceImportanceWhenColliding, _rb.velocity.magnitude) > CalculateCurrentSimulationWeight(oCarEnduranceFactor, oCarWeight, oEnduranceImportance, oCurrentRbSpeed))
            {
                return true;
            } else
            {
                return false;
            }
        }

        private float CalculateCurrentSimulationWeight(float oCarEnduranceFactor, float oCarWeight, float oEnduranceImportance, float currentRbSpeed) // will return a "fake weight" considering the car weight, its endureance factor(current endurance / maxEndurance) and how important rhe endurance is
        {
            float simulatedWeightImportance = oCarWeight * oEnduranceImportance; // get how much weight is affected by the endurance
            float finalSimulatedWeight = oCarWeight - (simulatedWeightImportance * oCarEnduranceFactor); // simulated weight
            return finalSimulatedWeight + currentRbSpeed;
        }

        public void OnCarHasBeenPushed()
        {
            _hasBeenPushed = true;
            TimerManager.Instance.StartTimer(1.5f, () =>
            {
                _hasBeenPushed = false;
            }, (progress) => {

            }, "pushed", false, false);
        }

        public void LimitRigidBodySpeed(float maxSpeed)
        {
            Vector3 clampedVelocity = _rb.velocity;

            if (clampedVelocity.magnitude > (maxSpeed / SPEED_TO_METER_PER_SECOND))
            {
                clampedVelocity = clampedVelocity.normalized * (maxSpeed / SPEED_TO_METER_PER_SECOND);
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

        public Vector3 GetCurrentRbVelocity()
        {
            return _rb.velocity;
        }

        public bool IsVehicleMoving()
        {
            if (_rb.velocity.magnitude > isMovingThreshold)
            {
                return true;
            }
            else return false;
        }
    }
}
