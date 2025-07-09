using FMODUnity;
using UnityEngine;

public class DestructibleProp : MonoBehaviour
{
    [SerializeField] private float propHealth;
    [SerializeField] private float requiredSpeedToDamage;

    private float _damageAmount = 1f;

    //private EventReference destroySound; FUTURE USE

    private const int MINIMUM_HP_TO_DESTROY = 0;
    private void OnCollisionEnter(Collision collision)
    {
        Transform otherRoot = collision.collider.transform.root;

        Rigidbody rb = otherRoot.GetComponentInChildren<Rigidbody>();

        if (rb == null)
            return;

        float impactSpeed = rb.linearVelocity.magnitude;

        if (impactSpeed >= requiredSpeedToDamage)
            TakeDamage(_damageAmount);
    }

    private void TakeDamage(float damage)
    {
        propHealth -= damage;

        if (propHealth <= MINIMUM_HP_TO_DESTROY)
        {
            this.gameObject.SetActive(false);
        }
    }
}
