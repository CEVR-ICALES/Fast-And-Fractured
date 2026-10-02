using FastAndFractured;
using UnityEngine;
using Utilities;

public class GolfBallBehaviour : MonoBehaviour
{
    [SerializeField]
    private Rigidbody rb;

    [SerializeField]
    private float maxPosibleSpeed = 500f;

    [SerializeField]
    private float baseSpeedOnHit = 100f;

    // private Vector3 reverseDirection;

    [SerializeField]
    private float reduceVelocityTime= 2f;

    private float _reduceVelocityTimeMod;

    [SerializeField]
    private float timeOnMaxSpeed = 1f;

    private bool _isBeenImpulsed;
    [SerializeField]
    private AnimationCurve massFactor;

    [SerializeField]
    private float baseForce = 50000;

    [SerializeField]
    private AnimationCurve speedFactor;
    const float SPEED_TO_METER_PER_SECOND = 3.6f;


    private ITimer _impulsedTimer;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }_reduceVelocityTimeMod = reduceVelocityTime;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (!_isBeenImpulsed && rb.linearVelocity.magnitude != 0)
        {
            if (_reduceVelocityTimeMod < 0)
            {
                _reduceVelocityTimeMod = 0;
            }
            float lerpMod = _reduceVelocityTimeMod/reduceVelocityTime;
            Vector3 lerpVector = Vector3.Lerp(Vector3.zero,rb.linearVelocity,lerpMod);
            rb.linearVelocity = new Vector3(lerpVector.x,rb.linearVelocity.y,lerpVector.z);
            _reduceVelocityTimeMod-=Time.fixedDeltaTime;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.TryGetComponent(out PhysicsBehaviour otherComponentPhysicsBehaviours))
        {
            if (_isBeenImpulsed && rb.linearVelocity.magnitude != 0)
            {
                float otherCarEnduranceFactor = otherComponentPhysicsBehaviours.StatsController.Endurance / otherComponentPhysicsBehaviours.StatsController.MaxEndurance; // calculate current value of the other car endurance
                float otherCarWeight = otherComponentPhysicsBehaviours.StatsController.Weight;
                float otherCarEnduranceImportance = otherComponentPhysicsBehaviours.StatsController.EnduranceImportanceWhenColliding;
                Vector3 direction = (collision.transform.position - transform.position).normalized;
                float massStrenght = massFactor.Evaluate(rb.mass/collision.rigidbody.mass);
                float speedStrenght = massFactor.Evaluate(rb.linearVelocity.magnitude/SPEED_TO_METER_PER_SECOND/maxPosibleSpeed);
                float totalBallForce = baseForce * speedStrenght * massStrenght;
                float forceToApply = otherComponentPhysicsBehaviours.CalculateForceToApplyToOtherCar(otherCarEnduranceFactor, otherCarWeight, otherCarEnduranceImportance,totalBallForce);
                if (!otherComponentPhysicsBehaviours.HasBeenPushed)
                {
                    otherComponentPhysicsBehaviours.ApplyImpulse(forceToApply*direction, ForceMode.Force,false,1.5f,false,Mathf.Infinity); // for now we just apply an offset on the y axis provisional
                    otherComponentPhysicsBehaviours.CarImpactHandler.OnHasBeenPushed(otherComponentPhysicsBehaviours);
                }
            }
            else
            {
                float combinedSpeed = collision.rigidbody.linearVelocity.magnitude + (baseSpeedOnHit/SPEED_TO_METER_PER_SECOND);
                Vector3 direction = collision.rigidbody.linearVelocity.normalized;
                float massStrenght = massFactor.Evaluate(rb.mass/collision.rigidbody.mass);
                Vector3 ballVelocity = direction * combinedSpeed * massStrenght;
                rb.AddForce(ballVelocity,ForceMode.VelocityChange);
                _isBeenImpulsed = true;
                _reduceVelocityTimeMod = reduceVelocityTime + timeOnMaxSpeed;
                _impulsedTimer = TimerSystem.Instance.CreateTimer(timeOnMaxSpeed,
                onTimerDecreaseComplete: () =>
                {
                    _isBeenImpulsed = false;
                    _impulsedTimer = null;
                });
            }
        }
    }
}
