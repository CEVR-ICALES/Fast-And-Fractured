using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Windmill : MonoBehaviour
{
    [SerializeField] float maxSpeed =10f;
    [SerializeField] float windmillInitialSpeed;
    [SerializeField] float speedIncreasePerSecond = 1;
    private float _currentSpeed;
    [SerializeField] GameObject transformVisual;
    [SerializeField] Vector3 rotationDirection = Vector3.forward;

    private void Start()
    {
        currentSpeed = windmillInitialSpeed;
    }
    void Update()
    {
        currentSpeed = Mathf.Min(_currentSpeed + Time.deltaTime, maxSpeed);
        transformVisual.transform.Rotate(rotationDirection, _currentSpeed,Space.Self);

    }
}
