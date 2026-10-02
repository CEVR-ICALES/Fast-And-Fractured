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

    private float _maxSpeedOnUnitsXSecond;

    [SerializeField]
    private float reduceVelocityTime= 2f;

    [SerializeField]
    private float timeOnMaxSpeed = 1f;

    private bool _isBeenImpulsed;
    [SerializeField]
    [Range(10f,100f)]
    private float reductionBaseCarStrenght = 10f;
    [SerializeField]
    private AnimationCurve massFactor;

    [SerializeField]
    private float baseForceToApply = 50000;

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
        }
        _maxSpeedOnUnitsXSecond = _maxSpeedOnUnitsXSecond * SPEED_TO_METER_PER_SECOND;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.TryGetComponent(out PhysicsBehaviour otherComponentPhysicsBehaviours))
        {
            Rigidbody oterhRb = collision.rigidbody;
            if (_isBeenImpulsed && rb.linearVelocity.magnitude != 0)
            {
                float otherCarEnduranceFactor = otherComponentPhysicsBehaviours.StatsController.Endurance / otherComponentPhysicsBehaviours.StatsController.MaxEndurance; // calculate current value of the other car endurance
                float otherCarWeight = otherComponentPhysicsBehaviours.StatsController.Weight;
                float otherCarEnduranceImportance = otherComponentPhysicsBehaviours.StatsController.EnduranceImportanceWhenColliding;
                Vector3 direction = (collision.transform.position - transform.position).normalized;
                float massStrenght = massFactor.Evaluate(rb.mass/collision.rigidbody.mass);
                float speedStrenght = speedFactor.Evaluate(rb.linearVelocity.magnitude*SPEED_TO_METER_PER_SECOND/maxPosibleSpeed);
                float totalBallForce = baseForceToApply * speedStrenght * massStrenght;
                float forceToApply = otherComponentPhysicsBehaviours.CalculateForceToApplyToOtherCar(otherCarEnduranceFactor, otherCarWeight, otherCarEnduranceImportance,totalBallForce);
                if (!otherComponentPhysicsBehaviours.HasBeenPushed)
                {
                    otherComponentPhysicsBehaviours.ApplyImpulse(forceToApply*direction, ForceMode.Force,false,1.5f,false,Mathf.Infinity); // for now we just apply an offset on the y axis provisional
                    otherComponentPhysicsBehaviours.CarImpactHandler.OnHasBeenPushed(otherComponentPhysicsBehaviours);
                }
            }
            else
            {
                float otherCarBaseForce = otherComponentPhysicsBehaviours.StatsController.BaseForce-(otherComponentPhysicsBehaviours.StatsController.BaseForce*(reductionBaseCarStrenght/100));
                float otherCarMaxSpeed = otherComponentPhysicsBehaviours.StatsController.MaxSpeedDashing;
                float otherCarCurrentSpeed = oterhRb.linearVelocity.magnitude*SPEED_TO_METER_PER_SECOND;
                float speedStrenght = speedFactor.Evaluate(otherCarCurrentSpeed/otherCarMaxSpeed);
                Vector3 direction = collision.rigidbody.linearVelocity.normalized;
                float massStrenght = massFactor.Evaluate(rb.mass/collision.rigidbody.mass);
                Vector3 ballVelocity = otherCarBaseForce * direction * speedStrenght * massStrenght;
                rb.AddForce(ballVelocity,ForceMode.VelocityChange);
                _isBeenImpulsed = true;
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
