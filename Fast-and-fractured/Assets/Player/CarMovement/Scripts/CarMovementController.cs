using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarMovementController : MonoBehaviour
{
    public WheelController[] wheels;

    [Header("Motor Settings")]
    [SerializeField] private float _motorTorque;

    [Header("Brake Settings")]
    [SerializeField] private float _brakeTorque;
}
