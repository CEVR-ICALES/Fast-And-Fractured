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
    [SerializeField] protected ParticleSystem particles;
    //ForPushShoot Handle class
    //public float PushStrengh { set => pushStrengh = value; }
    //protected float pushStrengh;
    // Update is called once per frame
    protected abstract void FixedUpdate();

    public virtual void InitBulletTrayectory()
    {
        rb = GetComponent<Rigidbody>();
    }

    protected void OnBulletEndTrayectory()
    {
        ObjectPoolManager.Instance.DesactivatePooledObject(this, gameObject);
    }

    protected abstract void OnTriggerEnter(Collider other);
}
