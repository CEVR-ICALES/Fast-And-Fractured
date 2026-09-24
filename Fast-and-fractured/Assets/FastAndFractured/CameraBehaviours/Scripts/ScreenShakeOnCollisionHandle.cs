using FastAndFractured;
using StateMachine;
using Unity.Cinemachine;
using UnityEngine;

public enum ScreenShakeOnCollisionType
{
    Listener,
    Source,
}
public class ScreenShakeOnCollisionHandle : MonoBehaviour
{
    [SerializeField]
    private ScreenShakeOnCollisionType type = ScreenShakeOnCollisionType.Source;
    
    [SerializeField]
    private Collider shakeCollider;

    [SerializeField]
    private ScreenShakeProfile screenShakeProfile;

    [SerializeField]
    private bool itDependOnDistance = false;

    [SerializeField]
    [Range(10,100)]
    private float strenghtOfFactorDistance = 50f;

    [SerializeField]
    private float maxDistanceDifference = 50f;

    private float _boundToCenter;

    [SerializeField]
    private bool itDependOnSpeed = false;

    [SerializeField]
    private float referenceHighSpeed = 300f;
    [SerializeField]
    [Range(10,100)]
    private float strenghtOfFactorSpeed = 80f;

    [SerializeField]
    private Rigidbody ownRigydbody;

    [SerializeField]
    private ScreenShakeSourceController screenShakeSourceController;

    [Tooltip("This value is only needed if you're the listener.")]
    [SerializeField]
    private CameraBehaviours ownCameraBehaviour;

    private const float MAX_PERCENTAGE = 100f;

    private const float SPEED_TO_METER_PER_SECOND = 3.6f;

    
    private void Start()
    {
        if (shakeCollider == null)
        {
            shakeCollider = GetComponent<Collider>();
        }
        if (screenShakeSourceController == null)
        {
            screenShakeSourceController = GetComponent<ScreenShakeSourceController>();
        }
        if (ownCameraBehaviour == null&&type==ScreenShakeOnCollisionType.Listener)
        {
            if((ownCameraBehaviour = transform.parent.parent.GetComponentInChildren<CameraBehaviours>())==null)
            shakeCollider.enabled=false;
        }
        _boundToCenter = shakeCollider.bounds.max.magnitude;
    }

    private void OnTriggerEnter(Collider other)
    {
        CameraBehaviours cameraBehaviours;
        if(type== ScreenShakeOnCollisionType.Source)
        {
        Transform carBaseReference = other.transform.parent;
         cameraBehaviours = carBaseReference != null ? carBaseReference.GetComponentInChildren<CameraBehaviours>() : null;
        }
        else
        {
            cameraBehaviours = ownCameraBehaviour;
        }

        HandleSourceCollision(cameraBehaviours,other);
    }

    private void HandleSourceCollision(CameraBehaviours cameraBehaviours,Collider other)
    {
        
        if(cameraBehaviours!=null)
        {
            float baseImpactForce = screenShakeProfile.impactForce;
            if(itDependOnDistance)
            {
                float distanceToCenter = (other.transform.position - shakeCollider.bounds.center).magnitude;
                float distanceFactor = (1 + ((maxDistanceDifference - distanceToCenter )/distanceToCenter))*(strenghtOfFactorDistance/MAX_PERCENTAGE);
                screenShakeProfile.impactForce*=distanceFactor;
            }
            if (itDependOnSpeed)
            {
                if (ownRigydbody == null)
                {
                    Debug.LogWarning("The variable ownRigydbody form " + this + " is null. Assign the corresponding rigydbody or untrigger the itDependOnSpeedFlag.");
                    return;
                }
                float combinedSpeed = other.attachedRigidbody!=null ? ownRigydbody.linearVelocity.magnitude + other.attachedRigidbody.linearVelocity.magnitude : ownRigydbody.linearVelocity.magnitude;
                float referenceHighSpeedInUnitsPerSecond = referenceHighSpeed/SPEED_TO_METER_PER_SECOND;
                float speedFactor = combinedSpeed/referenceHighSpeedInUnitsPerSecond;
                float speedFactorWithPercentageApplied = speedFactor * (strenghtOfFactorSpeed/MAX_PERCENTAGE);
                screenShakeProfile.impactForce*=speedFactorWithPercentageApplied;
            }
            screenShakeSourceController.PlayLocalShakeFromProfile(cameraBehaviours,screenShakeProfile);
            screenShakeProfile.impactForce = baseImpactForce;
        }
    }

    private void HandleListenerCollision(Collider other)
    {
        
    }
}
    
