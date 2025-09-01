using FMODUnity;
using UnityEngine;

public class DestructibleProp : MonoBehaviour
{
    [SerializeField] private float propHealth;

    private float _damageAmount = 1f;

    [SerializeField] private LayerMask damagingLayer;

    [SerializeField] private ParticleSystem destroyedParticles;

    //private EventReference destroySound; FUTURE USE

    private const int MINIMUM_HP_TO_DESTROY = 0;
    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Entered Collision");
        GameObject other = collision.gameObject;

        if (((1 << other.layer) & damagingLayer) == 0)
            return;

        Rigidbody rb = other.GetComponentInChildren<Rigidbody>();

        if (rb == null)
            return;

        TakeDamage(_damageAmount);
    }

    private void TakeDamage(float damage)
    {
        propHealth -= damage;

        if (propHealth <= MINIMUM_HP_TO_DESTROY)
        {
            destroyedParticles.transform.position = transform.position;
            destroyedParticles.Play();
            this.gameObject.SetActive(false);
            Debug.Log("Damage Done");
        }
    }
}
