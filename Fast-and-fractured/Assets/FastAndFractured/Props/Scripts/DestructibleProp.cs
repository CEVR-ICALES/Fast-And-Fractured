using Enums;
using FMODUnity;
using Utilities;
using UnityEngine;

public class DestructibleProp : MonoBehaviour
{
    #region Variables and Constants
    [Header("Basic Variables")]
    [SerializeField] private float propHealth;
    [SerializeField] private LayerMask damagingLayer;
    [SerializeField] private ParticleSystem destroyedParticles;

    [SerializeField] private PropType propType = PropType.GENERIC;

    [Header("Only for Tree Models")]
    [SerializeField] private GameObject intactTreeModel;
    [SerializeField] private GameObject trunkTreeModel;
    [SerializeField] private ParticleSystem collisionParticles;

    private float _damageAmount = 1f;
    private const int MINIMUM_HP_TO_DESTROY = 0;

    private float _damageCooldown = 1f;
    private float _lastDamageTime = -999f;
    private float trunkLifetime = 5f;

    private bool _isColliding = false;
    private bool _isDestroyed = false;

    private Timer _trunkTimer;

    //private EventReference destroySound; FUTURE USE
    //private EventReference collisionSound; FUTURE USE
    #endregion

    private void Start()
    {
        if (trunkTreeModel != null)
        {
            TimerData data = new TimerData(
                id: "TrunkTimer",
                duration: trunkLifetime,
                direction: TimerDirection.INCREASE,
                onTimerComplete: null,
                onTimerUpdate: null
            );

            _trunkTimer = new Timer(data);
            _trunkTimer.GetData().OnTimerIncreaseComplete = () =>
            {
                trunkTreeModel.SetActive(false);
            };
        }
    }

    private void Update()
    {
        if (_trunkTimer != null)
        {
            _trunkTimer.UpdateTimer(Time.deltaTime);
        }
    }
    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Entered Collision");
        GameObject other = collision.gameObject;

        if (((1 << other.layer) & damagingLayer) == 0)
            return;

        Rigidbody rb = other.GetComponentInChildren<Rigidbody>();

        if (rb == null)
            return;

        _isColliding = true;

        TryApplyDamage(_damageAmount);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (((1 << collision.gameObject.layer) & damagingLayer) != 0)
            _isColliding = false;
    }

    private void TryApplyDamage(float damage)
    {
        if (_isDestroyed) return;

        if (_isColliding == true && Time.time < _lastDamageTime + _damageCooldown)
            return;

        _lastDamageTime = Time.time;
        TakeDamage(damage);
    }

    private void TakeDamage(float damage)
    {
        propHealth -= damage;

        if (propHealth <= MINIMUM_HP_TO_DESTROY)
        {
            _isDestroyed = true;

            if (propType == PropType.GENERIC)
            {
                this.gameObject.SetActive(false);
                destroyedParticles.transform.position = transform.position;

            }
            else if (propType == PropType.TREE)
            {
                if (intactTreeModel != null)
                    intactTreeModel.SetActive(false);

                if (trunkTreeModel != null)
                {
                    trunkTreeModel.SetActive(true);

                    _trunkTimer?.StartTimer();
                }
            }

            destroyedParticles.Play();
        }
        else
        {
            if (propType == PropType.TREE)
                collisionParticles.Play();
        }
    }
}
