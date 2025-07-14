using FastAndFractured.Utilities;
using FishNet.Object.Prediction;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using Utilities;

[RequireComponent(typeof(Rigidbody))]
[DefaultExecutionOrder(-100)]
public class UnityRigidbodyAdapter : MonoBehaviour, ICustomRigidbody
{
    private Rigidbody _rb;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

     public Vector3 position { get => _rb.position; set => _rb.position = value; }
    public Quaternion rotation { get => _rb.rotation; set => _rb.rotation = value; }
    public Vector3 linearVelocity { get => _rb.linearVelocity; set => _rb.linearVelocity = value; }
    public Vector3 angularVelocity { get => _rb.angularVelocity; set => _rb.angularVelocity = value; }
    public float mass { get => _rb.mass; set => _rb.mass = value; }
    public float linearDamping { get => _rb.linearDamping; set => _rb.linearDamping = value; }
    public float angularDamping { get => _rb.angularDamping; set => _rb.angularDamping = value; }
    public bool isKinematic { get => _rb.isKinematic; set => _rb.isKinematic = value; }
    public RigidbodyConstraints constraints { get => _rb.constraints; set => _rb.constraints = value; }
    public new Transform transform => _rb.transform;  
     
    public void AddForce(Vector3 force, ForceMode mode = ForceMode.Force) => _rb.AddForce(force, mode);
    public void AddTorque(Vector3 torque, ForceMode mode = ForceMode.Force) => _rb.AddTorque(torque, mode);
    public void AddForceAtPosition(Vector3 force, Vector3 position, ForceMode mode = ForceMode.Force) => _rb.AddForceAtPosition(force, position, mode);

     public void MovePosition(Vector3 position) => _rb.MovePosition(position);
    public void MoveRotation(Quaternion rot) => _rb.MoveRotation(rot);

     public void Simulate()
    {
     }
} 

namespace FastAndFractured.Utilities
{ 
    public interface ICustomRigidbody
    {
         Vector3 position { get; set; }
        Quaternion rotation { get; set; }
        Vector3 linearVelocity { get; set; }
        Vector3 angularVelocity { get; set; }
        float mass { get; set; }
        float linearDamping { get; set; }
        float angularDamping { get; set; }
        bool isKinematic { get; set; }
        RigidbodyConstraints constraints { get; set; }
        Transform transform { get; } 
        void AddForce(Vector3 force, ForceMode mode = ForceMode.Force);
        void AddTorque(Vector3 torque, ForceMode mode = ForceMode.Force);
        void AddForceAtPosition(Vector3 force, Vector3 position, ForceMode mode = ForceMode.Force);

        void MovePosition(Vector3 position);
        void MoveRotation(Quaternion rot);

        void Simulate();
    }
}
