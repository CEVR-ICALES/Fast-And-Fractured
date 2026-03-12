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
    private float currentTime = 0;
    [SerializeField]
    private float rotationTime = 0.5f;
    private ITimer _rotateCanon;
    private Vector3 _previoustargetDirection = new Vector3(0,0,-1);
    private const float SLERP_MAX_VALUE = 1.0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 directionToTarget = (_targetDirection - _canon.transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
        if (_rotateCanon == null&&_previoustargetDirection!=_targetDirection)
        {
            _previoustargetDirection = _targetDirection;
            initialRotationYaw = _yawRotation.rotation;
            initialRotationPitch = _pitchRotation.rotation;
           
            finalRotationYaw = Quaternion.Euler(targetRotation.x,0,0);
            finalRotationPitch = Quaternion.Euler(0,targetRotation.y,0);
            _rotateCanon = TimerSystem.Instance.CreateTimer(rotationTime, onTimerDecreaseUpdate:(float time) =>
            {

                //Yaw rotation
                Rotate(_yawRotation, initialRotationYaw, finalRotationYaw, currentTime);
                //Pitch rotation 
                Rotate(_pitchRotation, initialRotationPitch, finalRotationPitch, currentTime);
                currentTime += Time.deltaTime;
            }, onTimerDecreaseComplete: () =>
            {
                currentTime = 0;
                _rotateCanon = null;
            }
            );
        }
    }

    void Rotate(Transform rotator,Quaternion initialRotation, Quaternion newRotation,float currentTime)
    {
        float slerpValue = currentTime/rotationTime;
       rotator.localRotation = Quaternion.Slerp(initialRotation,newRotation,slerpValue);
    }
}
