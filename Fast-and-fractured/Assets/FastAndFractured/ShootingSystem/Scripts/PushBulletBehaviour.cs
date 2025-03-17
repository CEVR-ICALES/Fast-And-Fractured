using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PushBulletBehaviour : BulletBehaivour
{
    [SerializeField] private LayerMask bouncingLayers;
    [SerializeField] private LayerMask characterLayers;
    public int BouncingNum { set => _bouncingNum = value; }
    private int _bouncingNum;
    private int _currentBouncingNum;
    public float BouncingStrenght {set => _bounceStrenght = value; }
    private float _bounceStrenght;
    private float _currentBounceStrenght;
    private Vector3 initialPosition;
    private bool _firstTime = true;
    public Vector3 CustomGravity { set => _customGravity = value; }
    private Vector3 _customGravity;
    private bool _useCustomGravity;
    protected override void FixedUpdate()
    {
        if (_useCustomGravity)
        {
            rb.velocity += _customGravity * Time.fixedDeltaTime;
        }
    }

    protected override void OnTriggerEnter(Collider other)
    {
    }

    private void OnCollisionEnter(Collision collision)
    {
        BouncingHandle(collision);
    }

    private void BouncingHandle(Collision collision)
    {
        if (_bouncingNum > _currentBouncingNum)
        {
            _currentBouncingNum++;
            _currentBounceStrenght = _bounceStrenght * (1f - (_currentBouncingNum / _bouncingNum));
            var newDirection = collision.GetContact(0).normal * _currentBounceStrenght;
            rb.velocity += newDirection;
            if (_firstTime)
            {
                Debug.Log((transform.position.z - initialPosition.z));
                _firstTime = false;
            }
        }
        rb.velocity = Vector3.zero;
    }

    public override void InitBulletTrayectory()
    {
        base.InitBulletTrayectory();
        _currentBounceStrenght = _bounceStrenght;
        _currentBouncingNum = 0;
        _useCustomGravity = true;
    }

    // Start is called before the first frame update
    void Start()
    {
        initialPosition = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
