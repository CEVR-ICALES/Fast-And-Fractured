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
    [Tooltip("Listener is prepared only for players with a camera. Since is waiting form something to impact. The Source will cause impact only if the characte have a camera.")]
    [SerializeField]
    private ScreenShakeOnCollisionType type = ScreenShakeOnCollisionType.Source;
    

    [SerializeField]
    private Collider shakeCollider;

    [SerializeField]
    private ScreenShakeProfile screenShakeProfile;

    [Header("Distance")]
    [SerializeField]
    private bool itDependOnDistance = false;

    [Tooltip("If the distance from the object to the other object is close to this distance, the shake will be lower")]
    [SerializeField]
    private float maxDistanceReference = 50f;

    [Tooltip("The quantity in percentage the distance will affect the result.")]
    [SerializeField]
    [Range(10,100)]
    private float strenghtOfFactorDistance = 50f;

    [Header("Speed")]
    [SerializeField]
    private bool itDependOnSpeed = false;

    [Tooltip("If the speed from the two objects combined is close to this speed, the shake will be higher")]
    [SerializeField]
    private float referenceHighSpeed = 300f;

    [Tooltip("The quantity in percentage the speed will affect the result.")]
    [SerializeField]
    [Range(10,100)]
    private float strenghtOfFactorSpeed = 80f;

    [SerializeField]
    private Rigidbody ownRigydbody;

    [Header("References")]

    [SerializeField]
    private ScreenShakeSourceController screenShakeSourceController;

    [Tooltip("This value is only needed if you're the listener. The source don't own a camera behaviour")]
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
            if((ownCameraBehaviour = transform.parent.parent.GetComponentInChildren<CameraBehaviours>())==null){
            shakeCollider.enabled=false;
            }
        }
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
            Vector3 direction = (other.transform.position - transform.position).normalized;
            float baseImpactForce = screenShakeProfile.impactForce;
            Vector3 baseDirection = screenShakeProfile.defaultVelocity;
            if(itDependOnDistance)
            {
                float distanceToCenter = (other.transform.position - shakeCollider.bounds.center).magnitude;
                float distanceFactor = (1 + ((maxDistanceReference - distanceToCenter )/distanceToCenter))*(strenghtOfFactorDistance/MAX_PERCENTAGE);
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
            screenShakeProfile.defaultVelocity = direction;
            screenShakeSourceController.PlayLocalShakeFromProfile(cameraBehaviours,screenShakeProfile);
            screenShakeProfile.impactForce = baseImpactForce;
            screenShakeProfile.defaultVelocity = baseDirection;
        }
    }
}
    
