using FMODUnity;
using UnityEngine;

public class DestructibleProp : MonoBehaviour
{
    [Header("Destruction Settings")]
    [SerializeField] private float propHealth;
    [SerializeField] private float vehicleMinForce;
    [SerializeField] private float damageMultiplier;

    [Header("Destruction State")]
    [SerializeField] private bool hasDamagedState = false;
    [SerializeField] private float damagedHealthThreshold; // Health in which will change state to damaged, for future shader with cracks

    //[SerializeField] private EventReference breakingSound;
    private bool _damagedStateShown = false;

    /// <summary>
    /// Takes Damage
    /// </summary>
    public void TakeDamage(float damage)
    {
        ApplyDamage(damage);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (IsVehicleCollision(collision, out float force) && force >= vehicleMinForce)
        {
            float damage = force * damageMultiplier;
            ApplyDamage(force);
        }
    }

    /// <summary>
    /// Checks if the collision was caused by a vehicle
    /// </summary>
    /// <returns></returns>
    private bool IsVehicleCollision(Collision col, out float force)
    {
        force = 0f;
        if (col.rigidbody != null && col.gameObject.CompareTag("Vehicle"))
        {
            force = col.relativeVelocity.magnitude * col.rigidbody.mass;
            return true;
        }

        return false;
    }

    private void ApplyDamage(float damage)
    {
        propHealth -= damage;

        if (hasDamagedState && !_damagedStateShown && propHealth <= damagedHealthThreshold)
        {
            this.gameObject.SetActive(false);
            _damagedStateShown = true;
            return;
        }

        if (propHealth <= 0f)
        {
            this.gameObject.SetActive(false);
        }
    }
}
