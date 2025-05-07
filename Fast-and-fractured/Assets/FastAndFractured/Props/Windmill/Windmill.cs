using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Windmill : MonoBehaviour
{
    [SerializeField] float maxSpeed =10f;
    [SerializeField] float windmillInitialSpeed;
    [SerializeField] float speedIncreasePerSecond = 1;
    private float currentSpeed;
    [SerializeField] GameObject transformVisual;
    // Update is called once per frame
    void Update()
    {
        currentSpeed = Mathf.Min(currentSpeed + Time.deltaTime, maxSpeed);
        transformVisual.transform.Rotate(Vector3.right, currentSpeed,Space.Self);

    }
}
