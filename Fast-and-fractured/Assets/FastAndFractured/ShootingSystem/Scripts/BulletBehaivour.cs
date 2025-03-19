using System;
using System.Collections;
using System.Collections.Generic;
using Game;
using UnityEngine;

public abstract class BulletBehaivour : MonoBehaviour, IPooledObject
{
    protected Rigidbody rb;
    protected Collider ownCollider;
    private MeshRenderer _meshRenderer;
    [SerializeField] protected Pooltype pooltype;
    public Pooltype Pooltype { get => pooltype; set => pooltype = value; }
    public Vector3 Velocity { set => velocity = value; }
    protected Vector3 velocity;
    public float Range { set => range = value; }
    protected float range;
    public float Damage { set => damage = value; }
    protected float damage;
    protected Vector3 initPosition;
    [SerializeField] protected GameObject particles;
    public bool InitValues => initValues;
    [SerializeField ]private bool initValues = true;
    [SerializeField] private float delayProyectileEnd = 1.5f;
   
    // Update is called once per frame
    protected abstract void FixedUpdate();
    public virtual void InitializeValues()
    {
        rb = GetComponent<Rigidbody>();
        ownCollider = GetComponent<Collider>();
        _meshRenderer = GetComponent<MeshRenderer>();
    }
    public virtual void InitBulletTrayectory()
    {
        if (particles != null)
        {
            particles.SetActive(false);
        }
        initPosition = transform.position;
        rb.velocity = velocity;
    }

    protected virtual void OnBulletEndTrayectory()
    {
        if (particles != null)
        {
            ownCollider.enabled = false;
            _meshRenderer.enabled = false;
            rb.constraints = RigidbodyConstraints.FreezePosition;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            transform.rotation = new Quaternion(0,0,0,0);
            particles.SetActive(true);
            TimerManager.Instance.StartTimer(delayProyectileEnd, () =>
            {
                ObjectPoolManager.Instance.DesactivatePooledObject(this, gameObject);
                ownCollider.enabled = true;
                _meshRenderer.enabled = true;
                rb.constraints = RigidbodyConstraints.None;
            }, null, "BulletTimerTillParticles " + gameObject.name, false, false);
        }
        else
            ObjectPoolManager.Instance.DesactivatePooledObject(this, gameObject);
    }

    protected virtual void OnTriggerEnter(Collider other)
    {

    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        
    }


}
