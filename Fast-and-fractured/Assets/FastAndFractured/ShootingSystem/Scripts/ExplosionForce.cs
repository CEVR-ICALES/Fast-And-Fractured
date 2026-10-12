using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enums;
using Utilities;
namespace FastAndFractured
{
    public class ExplosionForce : MonoBehaviour
    {
        private float _pushForce;

        private float _explosionDamagePercentage = 0;
        public SphereCollider ExplosionCollider { set => _explosionCollider = value; }
        private SphereCollider _explosionCollider;
        [SerializeField] private Transform _explosionVFX;

        [Header("Provisional Values for Calculate Force")]
        [SerializeField] private AnimationCurve enduranceFactorEvaluate;
        [SerializeField] private float averageCarWeight = 1150f;
        [SerializeField] private float carWeightImportance = 0.2f;
        [SerializeField, Range(0f, 100f)] private float forceToOtherObjects = 10f;
        [SerializeField] private ForceMode forceMode = ForceMode.Impulse;
        [SerializeField]
        private float explosionImpulseTime = 1f;
        [SerializeField]
        private bool stopSpeedOnHit = false;
        [SerializeField]
        private bool limitRbSpeed = false;
        private ITimer _explosionTimer;
        [SerializeField] private float limitRbSpeedLimit = 1000f;

        private GameObject _author;


        //Provisinal value to select the type force aplication 
        [SerializeField] private bool isGrounded = true;
        public void ActivateExplosionHitbox(float radius, float pushForce, Vector3 center, float startHitTime,float endHitTime, GameObject author)
        {
            if (_explosionCollider != null)
            {
                gameObject.SetActive(true);
                _pushForce = pushForce;
                _explosionCollider.center = center;
                _explosionCollider.radius = radius;
                _explosionCollider.enabled = false;
                _explosionVFX.localScale = Vector3.one * radius;
                _author = author;
                _explosionTimer = TimerSystem.Instance.CreateTimer(startHitTime, onTimerDecreaseComplete: () =>
                {
                  _explosionCollider.enabled = true; 
                  float realExplosionTime = endHitTime - startHitTime; 
                 _explosionTimer =  TimerSystem.Instance.CreateTimer(realExplosionTime,
                  onTimerDecreaseComplete: () =>
                  {
                      _explosionTimer = null;
                      _explosionCollider.enabled = false;
                  });
                 
                });
            }
        }

        public void ActivateExplosionHitbox(float radius, float pushForce, float damagePercentage, Vector3 center, float startHitTime,float endHitTime, GameObject author)
        {
            if (_explosionCollider != null)
            {
                gameObject.SetActive(true);
                _pushForce = pushForce;
                _explosionCollider.center = center;
                _explosionCollider.radius = radius;
                _explosionCollider.enabled = false;
                _explosionVFX.localScale = Vector3.one * radius;
                _explosionDamagePercentage = damagePercentage;
                _author = author;
                _explosionTimer = TimerSystem.Instance.CreateTimer(startHitTime, onTimerDecreaseComplete: () =>
                {
                  _explosionCollider.enabled = true; 
                  float realExplosionTime = endHitTime - startHitTime; 
                 _explosionTimer =  TimerSystem.Instance.CreateTimer(realExplosionTime,
                  onTimerDecreaseComplete: () =>
                  {
                      _explosionTimer = null;
                      _explosionCollider.enabled = false;
                  });
                 
                });
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
                StatsController otherComponentStatsController = otherComponentPhysicsBehaviours.StatsController;
                float otherCarEnduranceFactor = otherComponentStatsController.Endurance / otherComponentStatsController.MaxEndurance; // calculate current value of the other car endurance
                float otherCarWeight = otherComponentStatsController.Weight;
                float otherCarEnduranceImportance = otherComponentStatsController.EnduranceImportanceWhenColliding;
                float forceToApply;
                
                Vector3 closestPoint = _explosionCollider.ClosestPointOnBounds(other.bounds.max);

                Vector3 vectorCenterToContactPoint = closestPoint - transform.position;

                Vector3 direction = vectorCenterToContactPoint.normalized;

                forceToApply = otherComponentPhysicsBehaviours.CalculateForceToApplyToOtherCar(otherCarEnduranceFactor, otherCarWeight, otherCarEnduranceImportance,_pushForce);

                if (!otherComponentPhysicsBehaviours.HasBeenPushed)
                {
                    otherComponentPhysicsBehaviours.ApplyImpulse(direction, closestPoint, forceToApply , forceMode,limitRbSpeed,explosionImpulseTime,stopSpeedOnHit,limitRbSpeedLimit); // for now we just apply an offset on the y axis provisional
                    otherComponentPhysicsBehaviours.CarImpactHandler.OnHasBeenPushed(otherComponentPhysicsBehaviours);
                    if(_explosionDamagePercentage!=0)
                    otherComponentStatsController.TakeEndurance(_explosionDamagePercentage * otherComponentStatsController.MaxEndurance,false,_author);
                    if(transform.parent.gameObject.TryGetComponent(out PushBulletBehaviour pushBullet))
                    {
                        if(other.gameObject != pushBullet.Creator)
                        {
                            otherComponentStatsController.lastEnemyThatPushedMe = _author;
                        }
                    }
                    
                }
            }
            else if(other.gameObject.TryGetComponent(out PointExplosion pointExplosion))
            {
                pointExplosion.ExplodePoint();
            }
            else if(other.gameObject.TryGetComponent(out GolfBallBehaviour golfBall))
            {
                Vector3 closestPoint = _explosionCollider.ClosestPointOnBounds(other.bounds.max);
                Vector3 vectorCenterToContactPoint = closestPoint - transform.position;
                Vector3 direction = vectorCenterToContactPoint.normalized;
                golfBall.OnCollide(_pushForce,direction);
            }
            else if (other.gameObject.TryGetComponent(out Rigidbody otherRigidbody))
            {
                Vector3 otherPosition = other.transform.position;

                Vector3 contactPoint = _explosionCollider.ClosestPointOnBounds(other.bounds.max);

                Vector3 vectorCenterToContactPoint = contactPoint - transform.position;

                Vector3 direction = vectorCenterToContactPoint.normalized;

                otherRigidbody.AddForceAtPosition(forceToOtherObjects * direction, contactPoint, forceMode);
            }
        }
    }
}
