using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game;

public class PlayerBehaviour  : MonoBehaviour, IRequestPool
{
    [SerializeField] private GameObject projectile;
    [SerializeField] private float projectileSpeed = 10f;
    [SerializeField] private float modMaxSpeed = 0.5f;
    [SerializeField] private float bulletDamage = 10f;
    [SerializeField]
    private float bulletMult = 1.5f;
    [SerializeField] private float temporalBulletDamage = 5f;
    [SerializeField] private float temporalMaxSpeedDow = 5f;
    [SerializeField] private float temporalTimer = 0.5f;
     private Rigidbody _rb;
    private StatsController _statsController;
    [SerializeField] private float _speed;
    [SerializeField] private Pooltype pooltype;
    public Pooltype PoolType { get => pooltype; }

    public GameObject RequestPool()
    {
        return ObjectPoolManager.Instance.GivePooledObject(PoolType);
    }



    #region UnityEvents
    // Start is called before the first frame update
    void Start()
    {
        _statsController = transform.GetComponent<StatsController>();
        _rb = transform.GetComponent<Rigidbody>();
        _speed = _statsController.MinSpeed;
        bulletDamage = _statsController.NormalShootDamage;
        Debug.Log("Controllers");
        Debug.Log("RightMouse: Shoot");
        Debug.Log("LeftShit: Accelerate");
        Debug.Log("LeftControll: Decelerate");
        Debug.Log("E: AugmentMaxSpeed");
        Debug.Log("Q: AugmentShootDamage");
        Debug.Log("Space: TemporalAugmentShootDamage");
        Debug.Log("F: Temporal MaxSpeed Down");
    }

    

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            GameObject projectile = RequestPool();
            if (projectile != null)
            {
                projectile.transform.position = transform.position;
                projectile.GetComponent<PlayerProjectile>().InitProjectile(projectileSpeed, _statsController.NormalShootDamage, transform.forward);
            }
        }
        if (Input.GetKey(KeyCode.LeftShift)){
             _speed += _statsController.Acceleration * Time.deltaTime;

            if (_speed > _statsController.MaxSpeed)
                _speed = _statsController.MaxSpeed;

        }
        if (Input.GetKey(KeyCode.LeftControl))
        {
                _speed -= _statsController.Acceleration * Time.deltaTime;
            if (_speed < _statsController.MinSpeed)
                _speed = _statsController.MinSpeed;
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            _statsController.UpgradeCharStat(STATS.MAX_SPEED, modMaxSpeed);
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            _statsController.ProductCharStats(STATS.NORMAL_DAMAGE,bulletMult);
            bulletDamage = _statsController.NormalShootDamage;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            _statsController.TemporalStatUp(STATS.NORMAL_DAMAGE,temporalBulletDamage,temporalTimer);
            bulletDamage = _statsController.NormalShootDamage;
        }
        if (Input.GetKeyDown(KeyCode.F))
        {
            _statsController.TemporalStatDown(STATS.MAX_SPEED, temporalMaxSpeedDow, temporalTimer);
        }
        if(Input.GetAxis("Horizontal") != 0 || Input.GetAxis("Vertical") != 0)
        {
            if (_speed > _statsController.MaxSpeed)
                _speed = _statsController.MaxSpeed;
            else if (_speed < _statsController.MinSpeed)
                    _speed = _statsController.MinSpeed;
        }

        _rb.velocity = Input.GetAxis("Horizontal") * _speed * Vector3.right + Input.GetAxis("Vertical") * _speed * Vector3.forward;
    }
    #endregion
    }
