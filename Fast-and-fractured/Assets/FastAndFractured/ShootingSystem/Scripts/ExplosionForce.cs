using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public class ExplosionForce : MonoBehaviour
    {
        private float _pushForce;
        public SphereCollider ExplosionCollider { set => _explosionCollider = value; }
        private SphereCollider _explosionCollider;
        [SerializeField] private Transform _explosionVFX;

        [Header("Provisional Values for Calculate Force")]
        [SerializeField] private AnimationCurve enduranceFactorEvaluate;
        [SerializeField] private float averageCarWeight = 1150f;
        [SerializeField] private float carWeightImportance = 0.2f;
        [SerializeField] private float applyForceYOffset = 1f;
        //Provisinal value to select the type force aplication 
        [SerializeField] private bool isGrounded = true;
        public void ActivateExplosionHitbox(float radius, float pushForce, Vector3 center)
        {
            if (_explosionCollider != null)
            {
                gameObject.SetActive(true);
                _pushForce = pushForce;
                _explosionCollider.center = center;
                _explosionCollider.radius = radius;
                _explosionVFX.localScale = Vector3.one * radius;
            }
        }
        public void DesactivateExplostionHitbox()
        {
            gameObject.SetActive(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if(other.TryGetComponent(out PhysicsBehaviour otherComponentPhysicsBehaviours))
            {
                if(otherComponentPhysicsBehaviours.StatsController.IsInvulnerable)
                {
                    otherComponentPhysicsBehaviours.StatsController.IsInvulnerable=false;
                    return;
                }
                
                otherComponentPhysicsBehaviours.CancelDash();
                float otherCarEnduranceFactor = otherComponentPhysicsBehaviours.StatsController.Endurance / otherComponentPhysicsBehaviours.StatsController.MaxEndurance; // calculate current value of the other car endurance
                float otherCarWeight = otherComponentPhysicsBehaviours.StatsController.Weight;
                float otherCarEnduranceImportance = otherComponentPhysicsBehaviours.StatsController.EnduranceImportanceWhenColliding;
                float forceToApply;

                Vector3 otherPosition = other.transform.position;

                Vector3 contactPoint = _explosionCollider.ClosestPoint(otherPosition);

                Vector3 vectorCenterToContactPoint = contactPoint - transform.position;

                Vector3 direction = vectorCenterToContactPoint.normalized;
                
                direction = isGrounded ? Vector3.ProjectOnPlane(direction, Vector3.up) : direction;

                float distanceToCenter = vectorCenterToContactPoint.magnitude;

                forceToApply = CalculateForceToApplyToOtherCar(otherCarEnduranceFactor, otherCarWeight, otherCarEnduranceImportance);

                if (!otherComponentPhysicsBehaviours.HasBeenPushed)
                {
                    otherComponentPhysicsBehaviours.ApplyForce(direction + Vector3.up * applyForceYOffset, contactPoint, forceToApply * 1 - ((distanceToCenter / _explosionCollider.radius))); // for now we just apply an offset on the y axis provisional
                    otherComponentPhysicsBehaviours.OnCarHasBeenPushed();
                }
            }
        }

        private float CalculateForceToApplyToOtherCar(float oCarEnduranceFactor, float oCarWeight, float oCarEnduranceImportance)
        {
            float weightFactor = 1 + ((oCarWeight - averageCarWeight) / averageCarWeight) * carWeightImportance; // is for example the car importance is 0.2 (20 %) and the car weights 1200 the final force will be multiplied by 1.05 or something close to that value since the car is heavier (number will be big so a 0.05 is enough for now)

            float enduranceFactor = enduranceFactorEvaluate.Evaluate(oCarEnduranceFactor);
            float enduranceContribution = enduranceFactor * oCarEnduranceImportance; // final endurance contribution considering how important is it for that car

            float force = _pushForce * weightFactor * enduranceContribution; // generate the force number from the BaseForce (base force should be the highest achiveable force)

            return force;
        }
    }
}
