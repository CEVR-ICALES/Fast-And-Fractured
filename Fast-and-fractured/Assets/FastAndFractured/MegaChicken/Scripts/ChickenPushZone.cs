using UnityEngine;
using Utilities;
using Enums;
namespace FastAndFractured
{
    public class ChickenPushZone : MonoBehaviour
    {
        public float pushForce = 10f;
        private SphereCollider _selfCollider;
        [SerializeField] private bool isGrounded = true;
        [SerializeField] private float applyForceYOffset = 1f;
        [SerializeField] private ForceMode forceMode = ForceMode.Force;
        [SerializeField, Range(0f, 100f)] private float forceMultiplier = 10f;
        void Start()
        {
            _selfCollider = GetComponent<SphereCollider>();
            _selfCollider.enabled = false;
        }

        void Update()
        {

        }

        void OnTriggerEnter(Collider other)
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

                Vector3 contactPoint = _selfCollider.ClosestPoint(otherPosition);

                Vector3 vectorCenterToContactPoint = contactPoint - transform.position;

                Vector3 direction = vectorCenterToContactPoint.normalized;

                direction = isGrounded ? Vector3.ProjectOnPlane(direction, Vector3.up) : direction;

                float distanceToCenter = vectorCenterToContactPoint.magnitude;

                forceToApply = forceMultiplier * otherComponentPhysicsBehaviours.CalculateForceToApplyToOtherCar(otherCarEnduranceFactor, otherCarWeight, otherCarEnduranceImportance);

                if (!otherComponentPhysicsBehaviours.HasBeenPushed)
                {
                    otherComponentPhysicsBehaviours.ApplyForce(direction + Vector3.up * applyForceYOffset, contactPoint, forceToApply * (1 - distanceToCenter / _selfCollider.radius), forceMode); // for now we just apply an offset on the y axis provisional
                    otherComponentPhysicsBehaviours.CarImpactHandler.OnHasBeenPushed(otherComponentPhysicsBehaviours);
                }
            }
        }
        public void ActivatePushZone()
        {
            _selfCollider.enabled = true;
        }
    }
}