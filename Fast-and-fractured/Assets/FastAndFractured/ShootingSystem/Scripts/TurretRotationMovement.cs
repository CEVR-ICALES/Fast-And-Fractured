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
        Quaternion lookAtTargetDirection = Quaternion.LookRotation(_targetDirection);
        Vector3 eulersLookAtTargetDirection = lookAtTargetDirection.eulerAngles;
        float yawAngle = eulersLookAtTargetDirection.y;
        Debug.Log(yawAngle);
        float clampYawAngle = Mathf.Clamp(yawAngle, (-statsController.NormalShootAngle), statsController.NormalShootAngle);
        Quaternion lookAtTargetDirectionYawClamped = Quaternion.Euler(eulersLookAtTargetDirection.x, clampYawAngle, 0);
        _canon.transform.rotation = Quaternion.Slerp(_canon.transform.rotation, lookAtTargetDirectionYawClamped, Time.deltaTime*canonRotationSpeed);
    }
}
