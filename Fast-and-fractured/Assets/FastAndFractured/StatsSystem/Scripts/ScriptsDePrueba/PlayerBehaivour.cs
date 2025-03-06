using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game;

public class PlayerBehaivour : MonoBehaviour
{
    [SerializeField] private GameObject proyectile;
    [SerializeField] private float proyectileSpeed = 10f;
    [SerializeField] private float modMaxSpeed = 0.5f;
    [SerializeField] private float bulletDamage = 10f;
    [SerializeField]
    private float bulletMult = 1.5f;
    [SerializeField] private float temporalBulletDamage = 5f;
    [SerializeField] private float temporalMaxSpeedDow = 5f;
    [SerializeField] private float temporalTimer = 0.5f;
     private Rigidbody _rb;
    private StatsController _statsController;
    [SerializeField]
    private float _speed;
    // Start is called before the first frame update
    void Start()
    {
        _statsController = transform.GetComponent<StatsController>();
        _rb = transform.GetComponent<Rigidbody>();
        _speed = _statsController.MinSpeed;
        bulletDamage = _statsController.NormalShootDamage;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            GameObject proyectile = Instantiate(this.proyectile, transform.position,Quaternion.identity);
            proyectile.GetComponent<PlayerProyectile>().damage = bulletDamage;
            proyectile.GetComponent<Rigidbody>().velocity = transform.forward * proyectileSpeed;
            Destroy(proyectile,1f);
        }
        if (Input.GetKey(KeyCode.LeftShift)){
             _speed += _statsController.Acceleration * Time.deltaTime;

            if (_speed > _statsController.MaxSpeed)
                _speed = _statsController.MaxSpeed;

        }
        if (Input.GetKey(KeyCode.RightShift))
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
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            _statsController.TemporalStatDown(STATS.MAX_SPEED, temporalMaxSpeedDow, temporalTimer);
        }
        _rb.velocity = Input.GetAxis("Horizontal") * _speed * Vector3.right + Input.GetAxis("Vertical") * _speed * Vector3.forward;
    }
}
