using System.Collections;
using System.Collections.Generic;
using Game;
using UnityEngine;

public class PlayerProjectile : MonoBehaviour, IPooledObject
{
    private float _damage;
    private Rigidbody _rb;
    private float _speed;
    [SerializeField]
    private float destroyTime = 3.5f;
    public IPooledObject.EndAction OnEndAction { get => onEndaction; set => onEndaction += value; }
    public Pooltype Pooltype { get => _pooltype; set => _pooltype = value; }
    private Pooltype _pooltype;

    public IPooledObject.EndAction onEndaction;
    private void Start()
    {
    }
    public void InitProjectile(float speed,float damage, Vector3 direction)
    {
        _damage = damage;
        _speed = speed;
        _rb = GetComponent<Rigidbody>();
        _rb.velocity = direction * speed;
        Destroy(gameObject, destroyTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.TryGetComponent<ShootEnemy>(out var target))
        {
            if(target.TryGetComponent<StatsController>(out var targetHP))
            {
                targetHP.TakeEndurance(_damage,false);
                onEndaction(gameObject,_pooltype);
            }
        }
    }
}
