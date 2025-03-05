using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarMovementController : MonoBehaviour
{
    public WheelController[] wheels;

    [Header("Motor Settings")]
    public STEERING_MODE SteeringMode = STEERING_MODE.FrontWheel;
    [SerializeField] private float _motorTorque;
    [SerializeField] private float _maxSteerAngle;
    [SerializeField] private float _steeringSmoothness;


    [Header("Brake Settings")]
    public BRAKE_MODE brakeMode = BRAKE_MODE.AllWheels;
    [SerializeField] private float _brakeTorque;

    [Header("Dashing Settings")]

    [SerializeField] private float _dashSpeed;
    [SerializeField] private float _dashForce;
    [SerializeField] private float _maxRbVelocityWhileDashing;
    private bool isDashing { get; set; } = false;

}
