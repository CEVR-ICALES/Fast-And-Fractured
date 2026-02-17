using System;
using FastAndFractured;
using UnityEngine;
using Utilities;

public class TurretRotationMovement : MonoBehaviour
{
    [SerializeField]
    private Transform _yawRotation;
    [SerializeField] 
    private Transform _pitchRotation;
    [SerializeField]
    private GameObject _canon;
    public Vector3 TargetDirection { get=> _targetDirection;  set=> _targetDirection = value; }
    private Vector3 _targetDirection;
    private Quaternion initialRotationYaw;
    private Quaternion initialRotationPitch;
    private Quaternion finalRotationYaw;
    private Quaternion finalRotationPitch;
    [SerializeField]
    private float rotationTime = 0.5f;
    private ITimer _rotateCanon;

    private const float SLERP_MAX_VALUE = 1.0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
       Quaternion worldRotationFromCanonToDirection = Quaternion.FromToRotation(_canon.transform.position, _targetDirection);
       Quaternion localRotationFromCanonToDirection = Quaternion.Inverse(_canon.transform.rotation) * worldRotationFromCanonToDirection;
        if (_rotateCanon == null)
        {
            initialRotationYaw = _yawRotation.rotation;
            initialRotationPitch = _pitchRotation.rotation;
            Vector3 initialRotationYawEulers = initialRotationYaw.eulerAngles;
            Vector3 initialRotationPitchEulers = initialRotationPitch.eulerAngles;

            Vector3 finalRotationYawEulers = new Vector3(localRotationFromCanonToDirection.x, initialRotationYawEulers.y, initialRotationYaw.z);
            Vector3 finalRotationPitchEulers = new Vector3(initialRotationPitchEulers.x, localRotationFromCanonToDirection.y, initialRotationPitchEulers.z);
            finalRotationYaw = Quaternion.Euler(finalRotationYawEulers);
            finalRotationPitch = Quaternion.Euler(initialRotationPitchEulers);
            _rotateCanon = TimerSystem.Instance.CreateTimer(rotationTime, onTimerDecreaseUpdate:(float time) =>
            {
                //Yaw rotation
                Rotate(_yawRotation, initialRotationYaw, finalRotationYaw, time);
                //Pitch rotation 
                Rotate(_pitchRotation, initialRotationPitch, finalRotationPitch, time);
            });
        }
    }

    void Rotate(Transform rotator,Quaternion initialRotation, Quaternion newRotation,float currentTime)
    {
        float slerpValue = currentTime * (SLERP_MAX_VALUE / rotationTime);
       rotator.rotation = Quaternion.Slerp(initialRotation,newRotation,slerpValue);
    }
}
