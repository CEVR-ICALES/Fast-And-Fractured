using Enums;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;
using Enums;

namespace FastAndFractured
{
    public class PhysicsBehaviour : MonoBehaviour
    {
        [SerializeField] private float isMovingThreshold;
        public bool IsCurrentlyDashing = false;
        public bool HasBeenPushed { get => _hasBeenPushed; }
        private bool _hasBeenPushed = false;
        const float SPEED_TO_METER_PER_SECOND = 3.6f;

        [Header("Provisional Values for Calculate Force")]
        [SerializeField] private AnimationCurve enduranceFactorEvaluate;
        [SerializeField] private float averageCarWeight = 1150f;
        [SerializeField] private float carWeightImportance = 0.2f;
        [Tooltip("Small offset that will be applied to the vector of the direction so that the car doesnt stop fast by dragging through the floor")]
        [SerializeField] private float applyForceYOffset = 0.2f;
        public Transform PushApplyPoint; // if we decide to not use the collision point as the starting point to generate the force this variable will be used

        [Header("Wall Collision Detection")]
        [SerializeField] private float wallCollisionAngleThreshold;
        [SerializeField] private float wallBounceForce = 12000f;

        [Header("Ground Detection")]
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private LayerMask staticLayer;
        [SerializeField] private float _checkGroundTime = 0.5f;
        private ITimer _groundTimer;
        public bool IsTouchingGround { get => _isTouchingGround; }
        private bool _isTouchingGround = false;
        public Vector3 TouchingGroundPoint { get => _touchingGroundPoint; set => _touchingGroundPoint = value; }
        public Vector3 TouchingGroundNormal { get => _touchingGroundNormal; set => _touchingGroundNormal = value; }
        private Vector3 _touchingGroundPoint;
        private Vector3 _touchingGroundNormal;

        [Header("Air rotation")]
        [SerializeField] private float slowDownFactor;

        [Header("Reference")]
        [SerializeField] private StatsController statsController;
        public StatsController StatsController { get => statsController; }
        public Rigidbody Rb { get => _rb; set => _rb = value; }
        private Rigidbody _rb;
        private CarMovementController _carMovementController;
        public CarImpactHandler CarImpactHandler { get => _carImpactHandler; }
        private CarImpactHandler _carImpactHandler;

        const string PUSHED_EFFECT_NAME = "Broken_Crystal";
        const float TIME_UNTIL_CAR_PUSH_STATE_RESET = 0.5f;

        GameObject hudEffect;

        private void OnEnable()
        {
            if (!_rb)
            {
                _rb = GetComponent<Rigidbody>();
            }

            _carImpactHandler = GetComponent<CarImpactHandler>();
            _carMovementController = GetComponent<CarMovementController>();
            _rb.mass = StatsController.Weight;
        }

        private void Start()
        {
            _touchingGroundNormal = transform.up.normalized;
            _touchingGroundPoint = transform.position;
        }
        private void OnTriggerEnter(Collider other)
        {
            // add on trigger enter logic
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsCurrentlyDashing)
            {
                RefactoredVehicleCollision(collision);
                CheckWallCollision(collision);
            }
            GroundCheck(collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            ExitGroundCheck(collision);
        }

        private void RefactoredVehicleCollision(Collision collision)
        {

            PhysicsBehaviour otherComponentPhysicsBehaviours = collision.gameObject.GetComponentInChildren<PhysicsBehaviour>();
            if (otherComponentPhysicsBehaviours != null)
            {
                CancelDash();
                otherComponentPhysicsBehaviours.CancelDash();
                if (otherComponentPhysicsBehaviours.HasBeenPushed)
                    return;
                
                // impact info
                ContactPoint contactPoint = collision.contacts[0];
                Vector3 collisionPos = contactPoint.point;
                Vector3 collisionNormal = contactPoint.normal;

                // other car information
                float otherCarEnduranceFactor = otherComponentPhysicsBehaviours.StatsController.Endurance / otherComponentPhysicsBehaviours.StatsController.MaxEndurance; // calculate current value of the other car endurance
                float otherCarWeight = otherComponentPhysicsBehaviours.StatsController.Weight;
                float otherCarEnduranceImportance = otherComponentPhysicsBehaviours.StatsController.EnduranceImportanceWhenColliding;

                // conditionals
                ModifiedCarState carModifiedState = _carImpactHandler.CheckForModifiedCarState();
                ModifiedCarState otherCarModifiedState = otherComponentPhysicsBehaviours.CarImpactHandler.CheckForModifiedCarState();
                bool isFrontalHit = Vector3.Angle(transform.forward, -collision.gameObject.transform.forward) <= statsController.FrontalHitAnlgeThreshold;
                bool isOtherCarDashing = otherComponentPhysicsBehaviours.IsCurrentlyDashing;
                bool isTheOneToPush = true;

                float forceToApply = 0f;

                if(isOtherCarDashing)
                {
                    if(isFrontalHit)
                    {
                        if(DecideIfWinsFrontalCollision(otherCarEnduranceFactor, otherCarWeight, otherCarEnduranceImportance, Rb.velocity.magnitude))
                        {
                            forceToApply = CalculateForceToApplyToOtherCarWhenFrontalCollision(otherCarEnduranceFactor, otherCarWeight, otherCarEnduranceImportance);
                        } else
                        {
                            forceToApply = 0;
                        }
                    } else
                    {
                        forceToApply = CalculateForceToApplyToOtherCar(otherCarEnduranceFactor, otherCarWeight, otherCarEnduranceImportance);
                    }
                } else
                {
                    forceToApply = CalculateForceToApplyToOtherCar(otherCarEnduranceFactor, otherCarWeight, otherCarEnduranceImportance);
                }

                Debug.Log($"Impact Before Modifier {forceToApply}");
                forceToApply = _carImpactHandler.ApplyModifierToPushForceAsAttacker(forceToApply, otherCarModifiedState, isFrontalHit, isOtherCarDashing); // chheck modifier for attacker
                forceToApply = otherComponentPhysicsBehaviours.CarImpactHandler.ApplyModifierToPushForceAsPushed(forceToApply, carModifiedState, isFrontalHit, true); // check modifier for dash reciver

                Debug.Log($"Impact After Modifier {forceToApply}");
                otherComponentPhysicsBehaviours.ApplyForce((-collisionNormal + Vector3.up * applyForceYOffset).normalized, collisionPos, forceToApply); // for now we just apply an offset on the y axis provisional
                _carImpactHandler.HandleOnCarImpact(isTheOneToPush, otherComponentPhysicsBehaviours);
                otherComponentPhysicsBehaviours.CarImpactHandler.HandleOnCarImpact(false, otherComponentPhysicsBehaviours);
            }  
            
        }

        private void GroundCheck(Collision collision)
        {
            if ((((groundLayer & 1 << collision.gameObject.layer) == 1 << collision.gameObject.layer)||
                ((staticLayer & 1 << collision.gameObject.layer) == 1 << collision.gameObject.layer))
                &&!_carMovementController.IsGrounded()) {
                    _isTouchingGround = true;
            }
        }

        private void ExitGroundCheck(Collision collision)
        {
            if (((groundLayer & 1 << collision.gameObject.layer) == 1 << collision.gameObject.layer) ||
                ((staticLayer & 1 << collision.gameObject.layer) == 1 << collision.gameObject.layer))
            {
                if (_isTouchingGround)
                    _isTouchingGround = false;
            }
        }

        public void CancelDash()
        {
            if (IsCurrentlyDashing)
            {
                _carMovementController.CancelDash();
            }
        }

        #region Dash Collisions Checkers

        private void CheckWallCollision(Collision collision)
        {
            ContactPoint contact = collision.contacts[0];
            float angle = Vector3.Angle(contact.normal, transform.forward); // angle btween collision normal and .forward

            if (angle > wallCollisionAngleThreshold)
            {
                _carMovementController.CancelDash();
                Vector3 bounceDirection = Vector3.Reflect(transform.forward, contact.normal);
                AddForce(bounceDirection * wallBounceForce, ForceMode.Impulse);
            }
        }

        #endregion

        #region Force Applier
        public void ApplyForce(Vector3 forceDirection, Vector3 forcePoint, float forceToApply)
        {

            _rb.AddForceAtPosition(forceDirection * forceToApply, forcePoint, ForceMode.Impulse);
            Debug.DrawRay(forcePoint, forceDirection * 5f, Color.red, 5f);
            if(StatsController.IsPlayer)
            {
                HUDManager.Instance.UpdateUIElement(UIElementType.BAD_EFFECTS, ResourcesManager.Instance.GetResourcesSprite(PUSHED_EFFECT_NAME));
                hudEffect = HUDManager.Instance.GetEffectGameObject(ResourcesManager.Instance.GetResourcesSprite(PUSHED_EFFECT_NAME));
                hudEffect.SetActive(!hudEffect.activeSelf);
                TimerSystem.Instance.CreateTimer(0.5f, onTimerDecreaseComplete: () =>
                {
                    if (hudEffect != null)
                    {
                        hudEffect.SetActive(!hudEffect.activeSelf);
                        hudEffect = null;
                    }
                });
            }
        }

        public void AddForce(Vector3 force, ForceMode forceMode)
        {
            if (_rb != null)
            {
                _rb.AddForce(force, forceMode);
            }
        }

        public void AddTorque(Vector3 torque, ForceMode forceMode)
        {
            _rb.AddTorque(torque, forceMode);
        }

        public void SlowDownAngularMomentum()
        {
            Vector3 initialAngularVelocity = _rb.angularVelocity;
            if (initialAngularVelocity.magnitude < 0.01f)
            {
                _rb.angularVelocity = Vector3.zero;
                return;
            }

            _rb.angularVelocity *= slowDownFactor;
        }
        #endregion

        #region Force Calculations
        private float CalculateForceToApplyToOtherCarWhenFrontalCollision(float oCarEnduranceFactor, float oCarWeight, float oCarEnduranceImportance)
        {
            float force = CalculateForceToApplyToOtherCar(oCarEnduranceFactor, oCarWeight, oCarEnduranceImportance);
            // TO DO decide how we want this to behave (stronger push for the one who looses, more equitative approach¿?)
            Debug.Log("This is a frontal Push");
            return force;
        }

        private float CalculateForceToApplyToOtherCar(float oCarEnduranceFactor, float oCarWeight, float oCarEnduranceImportance)
        {
            float weightFactor = 1 + ((oCarWeight - averageCarWeight) / averageCarWeight) * carWeightImportance; // is for example the car importance is 0.2 (20 %) and the car weights 1200 the final force will be multiplied by 1.05 or something close to that value since the car is heavier (number will be big so a 0.05 is enough for now)

            float enduranceFactor = enduranceFactorEvaluate.Evaluate(oCarEnduranceFactor);
            float enduranceContribution = enduranceFactor * oCarEnduranceImportance; // final endurance contribution considering how important is it for that car

            float force = statsController.BaseForce * weightFactor * enduranceContribution; // generate the force number from the BaseForce (base force should be the highest achiveable force)

            Debug.Log(force);
            return force;
        }

        private bool DecideIfWinsFrontalCollision(float oCarEnduranceFactor, float oCarWeight, float oEnduranceImportance, float oCurrentRbSpeed)
        {
            if (CalculateCurrentSimulationWeight((statsController.MaxEndurance / statsController.Endurance), statsController.Weight, statsController.EnduranceImportanceWhenColliding, _rb.velocity.magnitude) > CalculateCurrentSimulationWeight(oCarEnduranceFactor, oCarWeight, oEnduranceImportance, oCurrentRbSpeed))
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

        #endregion

        public void OnCarHasBeenPushed()
        {
            _hasBeenPushed = true;
            TimerSystem.Instance.CreateTimer(TIME_UNTIL_CAR_PUSH_STATE_RESET, onTimerDecreaseComplete: () =>
            {
                _hasBeenPushed = false;
            }, onTimerDecreaseUpdate: (progress) => {

            });
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
            _rb.constraints = RigidbodyConstraints.FreezeRotationY;
            _rb.constraints = RigidbodyConstraints.FreezeRotationZ;
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
