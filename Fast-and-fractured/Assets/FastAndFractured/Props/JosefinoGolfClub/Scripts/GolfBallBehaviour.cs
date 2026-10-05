using FastAndFractured;
using UnityEngine;
using UnityEngine.Events;
using Utilities;

public class GolfBallBehaviour : MonoBehaviour, ICanBeImpulseByTrampoline
{
    public Rigidbody Rb {get=>rb;}
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
    [Range(10f,100f)]
    private float reductionYValueOfBaseCarStrenghtVector = 50f;
    [SerializeField]
    private AnimationCurve massFactor;

    [SerializeField]
    private float baseForceToApplyToCars = 50000;

    [SerializeField]
    private float baseForceToApplyToOtherBalls = 1700;

    [SerializeField]
    private AnimationCurve speedFactor;
    
    const float SPEED_TO_METER_PER_SECOND = 3.6f;

    const float PERCENTAGE_TO_DECIMALS = 100f;


    private ITimer _impulsedTimer;

    public UnityEvent<Vector3,GolfBallBehaviour> onOutOfBounds;

    public UnityEvent<GolfBallBehaviour> onCancelOutOfBounds;

    private Vector3 _initialPosition;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
        _maxSpeedOnUnitsXSecond = _maxSpeedOnUnitsXSecond * SPEED_TO_METER_PER_SECOND;
        _initialPosition = transform.position;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if(_isBeenImpulsed && rb.linearVelocity.magnitude != 0){
        if(collision.gameObject.TryGetComponent(out PhysicsBehaviour otherComponentPhysicsBehaviours))
        {
            float otherCarEnduranceFactor = otherComponentPhysicsBehaviours.StatsController.Endurance / otherComponentPhysicsBehaviours.StatsController.MaxEndurance; // calculate current value of the other car endurance
            float otherCarWeight = otherComponentPhysicsBehaviours.StatsController.Weight;
            float otherCarEnduranceImportance = otherComponentPhysicsBehaviours.StatsController.EnduranceImportanceWhenColliding;
            Vector3 direction = (collision.transform.position - transform.position).normalized;
            float massStrenght = massFactor.Evaluate(rb.mass/collision.rigidbody.mass);
            float speedStrenght = speedFactor.Evaluate(rb.linearVelocity.magnitude*SPEED_TO_METER_PER_SECOND/maxPosibleSpeed);
            float totalBallForce = baseForceToApplyToCars * speedStrenght * massStrenght;
            float forceToApply = otherComponentPhysicsBehaviours.CalculateForceToApplyToOtherCar(otherCarEnduranceFactor, otherCarWeight, otherCarEnduranceImportance,totalBallForce);
            if (!otherComponentPhysicsBehaviours.HasBeenPushed)
            {
                otherComponentPhysicsBehaviours.ApplyImpulse(forceToApply*direction, ForceMode.Force,false,1.5f,false,Mathf.Infinity); // for now we just apply an offset on the y axis provisional
                otherComponentPhysicsBehaviours.CarImpactHandler.OnHasBeenPushed(otherComponentPhysicsBehaviours);
            }
        }
        }
        else if(collision.gameObject.TryGetComponent(out GolfBallBehaviour golfBallBehaviour))
        {
            golfBallBehaviour.OnCollide(baseForceToApplyToCars,rb.linearVelocity.magnitude,maxPosibleSpeed,rb.mass,rb.linearVelocity.normalized);
        }
    }

    public void OnCollide(float otherBaseForce, float otherCurrentSpeed, float otherMaxSpeed, float otherMass,Vector3 direction)
    {
        float maxForceToApplyToBall = baseForceToApplyToOtherBalls +  baseForceToApplyToOtherBalls *(reductionBaseCarStrenght/PERCENTAGE_TO_DECIMALS);
        bool reduceImpulseStrenght = otherBaseForce > maxForceToApplyToBall;
        if (reduceImpulseStrenght)
        {
            otherBaseForce -= otherBaseForce * (reductionBaseCarStrenght/PERCENTAGE_TO_DECIMALS); 
        }
        float otherCurrentSpeedMeterPerSecond = otherCurrentSpeed * SPEED_TO_METER_PER_SECOND;
        float speedStrenght = speedFactor.Evaluate(otherCurrentSpeed/otherMaxSpeed);
        float massStrenght = massFactor.Evaluate(rb.mass/otherMass);
        Vector3 ballImpulse = otherBaseForce * direction * speedStrenght * massStrenght;
        if (reduceImpulseStrenght)
        {
            ballImpulse = new Vector3(ballImpulse.x, ballImpulse.y - (ballImpulse.y*(reductionYValueOfBaseCarStrenghtVector/PERCENTAGE_TO_DECIMALS)),ballImpulse.z);
        }
        rb.AddForce(ballImpulse,ForceMode.Impulse);
         _isBeenImpulsed = true;
        _impulsedTimer = TimerSystem.Instance.CreateTimer(timeOnMaxSpeed,
        onTimerDecreaseComplete: () =>
        {
            _isBeenImpulsed = false;
            _impulsedTimer = null;
        });
    }

    public void OnCollide(float otherBaseForce, Vector3 direction)
    {
        float maxForceToApplyToBall = baseForceToApplyToOtherBalls +  baseForceToApplyToOtherBalls *(reductionBaseCarStrenght/PERCENTAGE_TO_DECIMALS);
        bool reduceImpulseStrenght = otherBaseForce > maxForceToApplyToBall;
        if (reduceImpulseStrenght)
        {
            otherBaseForce -= otherBaseForce * (reductionBaseCarStrenght/PERCENTAGE_TO_DECIMALS); 
        }
         Vector3 ballImpulse = otherBaseForce * direction;
        if (reduceImpulseStrenght)
        {
            ballImpulse = new Vector3(ballImpulse.x, ballImpulse.y - (ballImpulse.y*(reductionYValueOfBaseCarStrenghtVector/PERCENTAGE_TO_DECIMALS)),ballImpulse.z);
        }
        rb.AddForce(ballImpulse,ForceMode.Impulse);
         _isBeenImpulsed = true;
        _impulsedTimer = TimerSystem.Instance.CreateTimer(timeOnMaxSpeed,
        onTimerDecreaseComplete: () =>
        {
            _isBeenImpulsed = false;
            _impulsedTimer = null;
        });
    }

    public Rigidbody GetRigidbody()
    {
        return rb;
    }

    public float GetMassReference()
    {
        return rb.mass;
    }

    public void OutOfBoundsEvent()
    {
        onOutOfBounds?.Invoke(_initialPosition,this);
    }

    public void CancelOutOfBoundsEvent()
    {
        onCancelOutOfBounds?.Invoke(this);
    }
}
