using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PushBulletBehaviour : BulletBehaivour
{
    [SerializeField] private LayerMask bouncingLayers;
    [SerializeField] private LayerMask characterLayers;
    public float Angle { set { _angle = value; } }
    private float _angle;
    private float _bouncingNum;
    private float _bounceStrenght;
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
            var newDirection = collision.GetContact(0).normal * 10f;
            rb.velocity += newDirection;
        Debug.Log(rb.velocity);
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
