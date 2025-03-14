using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PushBulletBehaviour : BulletBehaivour
{
    [SerializeField] private LayerMask bouncingLayers;
    [SerializeField] private LayerMask characterLayers;
    public float Angle { set { _angle = value; } }
    private float _angle;
    protected override void FixedUpdate()
    {

    }

    protected override void OnTriggerEnter(Collider other)
    {
       if(other.gameObject.layer == bouncingLayers)
        {
            
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
            var newDirection = Vector3.ProjectOnPlane(velocity,collision.GetContact(0).normal);
            rb.velocity += newDirection;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
