using System.Collections;
using System.Collections.Generic;
using Game;
using UnityEngine;

public abstract class BulletBehaivour : MonoBehaviour, IPooledObject
{
    protected Rigidbody rb;
    [SerializeField] protected Pooltype pooltype;
    public Pooltype Pooltype { get => pooltype; set => pooltype = value; }
    public Vector3 Velocity { set => velocity = value; }
    protected Vector3 velocity;
    public float Range { set => range = value; }
    protected float range;
    public float Damage { set => damage = value; }
    protected float damage;
    protected Vector3 initPosition;
    [SerializeField] protected ParticleSystem particles;
    public bool InitValues => initValues;
    [SerializeField ]private bool initValues = true;
    [SerializeField] private ParticleSystem ownParticles;
    [SerializeField] private float delayProyectileEnd = 1.5f;
   
    // Update is called once per frame
    protected abstract void FixedUpdate();
    public virtual void InitializeValues()
    {
        rb = GetComponent<Rigidbody>();
    }
    public virtual void InitBulletTrayectory()
    {
        if (ownParticles != null)
        {
            ownParticles.gameObject.SetActive(false);
        }
        initPosition = transform.position;
        rb.velocity = velocity;
    }

    protected void OnBulletEndTrayectory()
    {
        if (ownParticles != null)
        {
            ownParticles.gameObject.SetActive(true);
            TimerManager.Instance.StartTimer(delayProyectileEnd, () =>
            {
                ObjectPoolManager.Instance.DesactivatePooledObject(this, gameObject);
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
