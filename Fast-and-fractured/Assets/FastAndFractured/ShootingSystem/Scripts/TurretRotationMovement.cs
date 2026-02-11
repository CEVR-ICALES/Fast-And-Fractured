using FastAndFractured;
using StateMachine;
using UnityEngine;
using Utilities;

public class TurretRotationMovement : MonoBehaviour
{
    [SerializeField]
    private GameObject _yawRotation;
    [SerializeField] 
    private GameObject _pitchRotation;
    [SerializeField]
    private GameObject _canon;
    private Controller _characterController;
    public Vector3 TargetDirection { get=> _targetDirection;  set=> _targetDirection = value; }
    private Vector3 _targetDirection;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
       Quaternion worldRotationFromCanonToDirection = Quaternion.FromToRotation(_canon.transform.position, _targetDirection);
       Quaternion localRotationFromCanonToDirection = Quaternion.Inverse(_canon.transform.rotation) * worldRotationFromCanonToDirection;
    }
}
