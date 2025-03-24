using System.Collections;
using System.Collections.Generic;
using Game;
using UnityEngine;
using Utilities;
public class PlayerProjectile : MonoBehaviour, IPooledObject
{
    private float _damage;
    private Rigidbody _rb;
    private float _speed;
    [SerializeField]
    private float destroyTime = 3.5f;
    public Pooltype Pooltype { get => _pooltype; set => _pooltype = value; }

    public bool InitValues => false;

    private Pooltype _pooltype;
    private float _time = 0;
    private void OnEnable()
    {
        _time = 0;
    }
    private void Start()
    {
    }
    public void InitProjectile(float speed,float damage, Vector3 direction)
    {
        _damage = damage;
        _speed = speed;
        _rb = GetComponent<Rigidbody>();
        _rb.velocity = direction * speed;
    }
    private void Update()
    {
        if (_time >= destroyTime)
        {
            ObjectPoolManager.Instance.DesactivatePooledObject(this,gameObject);
        }
        else
            _time += Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.TryGetComponent<ShootEnemy>(out var target))
        {
            if(target.TryGetComponent<StatsController>(out var targetHP))
            {
                targetHP.TakeEndurance(_damage,false);
                ObjectPoolManager.Instance.DesactivatePooledObject(this, gameObject);
            }
        }
    }

    public void InitializeValues()
    {
        throw new System.NotImplementedException();
    }
}
