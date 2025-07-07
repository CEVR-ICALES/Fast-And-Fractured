using FMODUnity;
using UnityEngine;

public class DestructibleProp : MonoBehaviour
{
    [Header("Destruction Settings")]
    [SerializeField] private float propHealth;
    //[SerializeField] private float vehicleMinForce;
    [SerializeField] private float damageMultiplier;

    [Header("Destruction State")]
    private bool _hasDamagedState = false;
    private float _damagedHealthThreshold; // Health in which will change state to damaged, for future shader with cracks will be serialized

    //[SerializeField] private EventReference breakingSound;
    private bool _damagedStateShown = false;

    private const string PLAYER_TAG_STRING = "Player";

    /// <summary>
    /// Takes Damage
    /// </summary>
    public void TakeDamage(float damage)
    {
        ApplyDamage(damage);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (IsVehicleCollision(collision, out float force))/* && force >= vehicleMinForce)*/
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
        if (col.rigidbody != null && col.gameObject.CompareTag(PLAYER_TAG_STRING))
        {
            force = col.relativeVelocity.magnitude * col.rigidbody.mass;
            return true;
        }

        return false;
    }

    private void ApplyDamage(float damage)
    {
        propHealth -= damage;

        if (_hasDamagedState && !_damagedStateShown && propHealth <= _damagedHealthThreshold)
        {
            //this.gameObject.SetActive(false);
            Debug.LogError("Damage done to prop");
            _damagedStateShown = true;
            return;
        }

        if (propHealth <= 0f)
        {
            this.gameObject.SetActive(false);
            Debug.LogError("Object destroyed");
        }
    }
}
