using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Game
{
    public class ExplosionForce : MonoBehaviour
    {
        private float _pushForce;
        public SphereCollider ExplosionCollider { set => _explosionCollider = value; }
        private SphereCollider _explosionCollider;
        [SerializeField] private Transform _explosionVFX;

        [Header("Explosion Angles")]
        [Header("Forward")]
        [SerializeField] private float forwardTo = 45f;
        [Header("Right")]
        [SerializeField] private float rightTo = 135f;
        [Header("Back")]
        [SerializeField] private float backTo = -135f;
        [Header("Left")]
        [SerializeField] private float leftTo = -45f;
        [Header("Provisional Values for Calculate Force")]
        [SerializeField] private AnimationCurve enduranceFactorEvaluate;
        [SerializeField] private float averageCarWeight = 1150f;
        [SerializeField] private float carWeightImportance = 0.2f;

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
            if (other.TryGetComponent(out StatsController statsController))
            {
                //float oCarWeight = statsController.Weight;
                Rigidbody oRB = other.GetComponentInParent<Rigidbody>();
                float oCarEnduranceFactor = statsController.Endurance;
                //Provisional Formula till real implentation
                float force = CalculateForceToApplyToOtherCar(oCarEnduranceFactor,statsController.Weight, statsController.EnduranceImportanceWhenColliding);
                Vector3 direction = CalculateDirectionByRegion(other.transform);
                float distanceToCenter = Vector3.Distance(other.transform.position, transform.position + _explosionCollider.center);
                oRB.AddForce(direction * force * (1 - (distanceToCenter / _explosionCollider.radius)));
            }
        }

        private Vector3 CalculateDirectionByRegion(Transform target)
        {
            var directionToObject = (target.position - (transform.position * _explosionCollider.radius)).normalized;

            Vector3 forwardDirection = transform.forward;
            float angle = Vector3.SignedAngle(forwardDirection, directionToObject, Vector3.up);

            if (angle >= leftTo && angle < forwardTo) // Forward
            {
                return transform.forward;
            }
            else if (angle >= forwardTo && angle < rightTo) // Right
            {
                return transform.right;
            }
            else if (angle >= rightTo || angle < backTo) // Back
            {
                return -transform.forward;
            }
            else if (angle >= backTo && angle < leftTo) // Left
            {
                return -transform.right;
            }
            else
                return transform.forward;
        }

        private float CalculateForceToApplyToOtherCar(float oCarEnduranceFactor, float oCarWeight, float oCarEnduranceImportance)
        {
            float weightFactor = 1 + ((oCarWeight - averageCarWeight) / averageCarWeight) * carWeightImportance; // is for example the car importance is 0.2 (20 %) and the car weights 1200 the final force will be multiplied by 1.05 or something close to that value since the car is heavier (number will be big so a 0.05 is enough for now)

            float enduranceFactor = enduranceFactorEvaluate.Evaluate(oCarEnduranceFactor);
            float enduranceContribution = enduranceFactor * oCarEnduranceImportance; // final endurance contribution considering how important is it for that car

            float force = _pushForce * weightFactor * enduranceContribution; // generate the force number from the BaseForce (base force should be the highest achiveable force)

            Debug.Log(force);
            return force;
        }
    }
}
