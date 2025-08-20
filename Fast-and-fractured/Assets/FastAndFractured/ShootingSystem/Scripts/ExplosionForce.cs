using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enums;
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
        [SerializeField, Range(0f, 100f)] private float forceMultiplier = 10f;
        [SerializeField] private ForceMode forceMode = ForceMode.Force;


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
        public void DeactivateExplosionHitbox()
        {
            gameObject.SetActive(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.TryGetComponent(out PhysicsBehaviour otherComponentPhysicsBehaviours))
            {
                if (otherComponentPhysicsBehaviours.CarImpactHandler.CheckForModifiedCarState() == ModifiedCarState.JOSEFINO_INVULNERABLE)
                {
                    otherComponentPhysicsBehaviours.CarImpactHandler.OnHasBeenPushed(otherComponentPhysicsBehaviours);
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

                forceToApply = forceMultiplier * otherComponentPhysicsBehaviours.CalculateForceToApplyToOtherCar(otherCarEnduranceFactor, otherCarWeight, otherCarEnduranceImportance);

                if (!otherComponentPhysicsBehaviours.HasBeenPushed)
                {
                    otherComponentPhysicsBehaviours.ApplyForce(direction + Vector3.up * applyForceYOffset, contactPoint, forceToApply * (1 - distanceToCenter / _explosionCollider.radius), forceMode); // for now we just apply an offset on the y axis provisional
                    otherComponentPhysicsBehaviours.CarImpactHandler.OnHasBeenPushed(otherComponentPhysicsBehaviours);
                }
            }
            else if (other.gameObject.TryGetComponent(out Rigidbody otherRigidbody))
            {
                Vector3 otherPosition = other.transform.position;

                Vector3 contactPoint = _explosionCollider.ClosestPoint(otherPosition);

                Vector3 vectorCenterToContactPoint = contactPoint - transform.position;

                Vector3 direction = vectorCenterToContactPoint.normalized;

                direction = isGrounded ? Vector3.ProjectOnPlane(direction, Vector3.up) : direction;

                otherRigidbody.AddForceAtPosition(_pushForce / 10 * direction, contactPoint, forceMode);
            }
        }
    }
}
