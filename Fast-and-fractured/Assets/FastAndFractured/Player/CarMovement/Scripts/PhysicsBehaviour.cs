using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysicsBehaviour : MonoBehaviour
{
    [SerializeField] private float _baseForce;

    private CarMovementController _carMovementController;
    private void OnTriggerEnter(Collider other)
    {
        // add on trigger enter logic
    }

    private void OnCollisionEnter(Collision collision)
    {
        //collision.
    }

    /*public void ApplyForce(float )
    {

    }*/


    

}
