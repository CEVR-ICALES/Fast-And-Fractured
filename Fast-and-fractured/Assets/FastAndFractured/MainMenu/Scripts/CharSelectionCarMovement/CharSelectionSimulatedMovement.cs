using UnityEngine;
using Utilities;

public class CharSelectionSimulatedMovement : MonoBehaviour
{
    private Rigidbody _rb;
    [SerializeField] private float forceToApply = 250;
    [SerializeField] private float forceDuration;

    [Header("WheelMeshes")]
    public GameObject[] WheelsMesh { set => wheelsMeshes=value; }
    [SerializeField] private GameObject[] wheelsMeshes;
    [SerializeField] private float rbSpeedThreshold = 2;
    [SerializeField] private float rotationSpeed = 180;


    private void OnEnable()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public void MoveCarForward()
    {
        _rb.AddForce(transform.forward * forceToApply, ForceMode.Impulse);
    }

    private void Update()
    {
        if(_rb.linearVelocity.magnitude > rbSpeedThreshold)
        {
            foreach(GameObject wheel in wheelsMeshes)
            {
                wheel.transform.Rotate(rotationSpeed * Time.deltaTime, 0, 0);
            }
        }
    }
}
    
