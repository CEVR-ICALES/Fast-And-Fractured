using FastAndFractured;
using StateMachine;
using Unity.Cinemachine;
using UnityEngine;

public class ScreenShakeOnCollisionHandle : MonoBehaviour
{
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

    private const float MAX_PERCENTAGE = 100f;

    [SerializeField]
    private ScreenShakeSourceController screenShakeSourceController;
    private void Start()
    {
        if (shakeCollider == null)
        {
            shakeCollider = GetComponent<Collider>();
        }
        _boundToCenter = shakeCollider.bounds.max.magnitude;
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("He entrado y soy " + other.gameObject);
        Transform carBaseReference = other.transform.parent;
        CameraBehaviours otherCameraBehaviours = carBaseReference != null ? carBaseReference.GetComponentInChildren<CameraBehaviours>() : null;
        if(otherCameraBehaviours!=null)
        {
            float baseImpactForce = screenShakeProfile.impactForce;
            float baseImpactTime = screenShakeProfile.impactTime;
            if(itDependOnDistance)
            {
                float distanceToCenter = (other.transform.position - shakeCollider.bounds.center).magnitude;
                float distanceFactor = (1 + ((maxDistanceDifference - distanceToCenter )/distanceToCenter))*(strenghtOfFactorDistance/MAX_PERCENTAGE);
                screenShakeProfile.impactForce*=distanceFactor;
                Debug.Log("BoundToCenter : " + _boundToCenter + " DistanceToCenter: " + distanceToCenter + " distanceFactor: " + distanceFactor);
            }
            screenShakeSourceController.PlayLocalShakeFromProfile(otherCameraBehaviours,screenShakeProfile);
            screenShakeProfile.impactForce = baseImpactForce;
        }
    }
}
    
