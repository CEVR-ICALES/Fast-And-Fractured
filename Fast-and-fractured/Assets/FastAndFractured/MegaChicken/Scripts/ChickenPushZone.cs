using UnityEngine;
using Utilities;
using Enums;
namespace FastAndFractured
{
    public class ChickenPushZone : MonoBehaviour
    {
        private SphereCollider _selfCollider;
        [Header("Push related")]
        [SerializeField] private bool isGrounded = true;
        [SerializeField] private float applyForceYOffset = 1f;
        [SerializeField] private ForceMode forceMode = ForceMode.Force;
        [SerializeField, Range(0f, 100f)] private float forceMultiplier = 10f;
        private const float HITBOX_TIME = 0.4f;
        void Start()
        {
            _selfCollider = GetComponent<SphereCollider>();
            _selfCollider.enabled = false;
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
                float otherCarEnduranceFactor = otherComponentPhysicsBehaviours.StatsController.Endurance / otherComponentPhysicsBehaviours.StatsController.MaxEndurance;
                float otherCarWeight = otherComponentPhysicsBehaviours.StatsController.Weight;
                float otherCarEnduranceImportance = otherComponentPhysicsBehaviours.StatsController.EnduranceImportanceWhenColliding;
                float forceToApply;

                Vector3 otherPosition = other.transform.position;

                Vector3 contactPoint = _selfCollider.ClosestPoint(otherPosition);

                Vector3 vectorCenterToContactPoint = contactPoint - transform.position;

                Vector3 direction = vectorCenterToContactPoint.normalized;

                direction.y += 0.5f;

                direction = isGrounded ? Vector3.ProjectOnPlane(direction, Vector3.up) : direction;

                float distanceToCenter = vectorCenterToContactPoint.magnitude;

                forceToApply = -forceMultiplier * otherComponentPhysicsBehaviours.CalculateForceToApplyToOtherCar(otherCarEnduranceFactor, otherCarWeight, otherCarEnduranceImportance);

                if (!otherComponentPhysicsBehaviours.HasBeenPushed)
                {
                    otherComponentPhysicsBehaviours.ApplyForce(direction, contactPoint, forceToApply * (1 - distanceToCenter / _selfCollider.radius), forceMode);
                    otherComponentPhysicsBehaviours.CarImpactHandler.OnHasBeenPushed(otherComponentPhysicsBehaviours);
                }
            }
        }
        public void ActivatePushZone()
        {
            _selfCollider.enabled = true;
            TimerSystem.Instance.CreateTimer(HITBOX_TIME, onTimerDecreaseComplete: () =>
            {
                _selfCollider.enabled = false;
            });
        }
    }
}