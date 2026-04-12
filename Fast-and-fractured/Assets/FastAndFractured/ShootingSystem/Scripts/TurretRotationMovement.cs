using System;
using FastAndFractured;
using UnityEngine;
using Utilities;

public class TurretRotationMovement : MonoBehaviour
{
    [SerializeField]
    private GameObject _canon;
    public Vector3 TargetDirection { get=> _targetDirection;  set=> _targetDirection = value; }
    private Vector3 _targetDirection;
    [SerializeField]
    private float canonRotationSpeed = 5f;
    [SerializeField]
    private StatsController statsController;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (statsController == null)
        {
            statsController = GetComponentInParent<StatsController>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if(_targetDirection!=Vector3.zero){
        Quaternion lookAtTargetDirection = Quaternion.LookRotation(_targetDirection);
        Vector3 carForward = statsController.transform.forward;
        float angle = Vector3.Angle(_targetDirection,carForward);
        if(angle<statsController.NormalShootAngle&&angle>-statsController.NormalShootAngle)
        _canon.transform.rotation = Quaternion.Slerp(_canon.transform.rotation, lookAtTargetDirection, Time.deltaTime*canonRotationSpeed);
        }
    }
}
