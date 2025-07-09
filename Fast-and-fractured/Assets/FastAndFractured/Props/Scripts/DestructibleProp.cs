using FMODUnity;
using UnityEngine;

public class DestructibleProp : MonoBehaviour
{
    [SerializeField] private float propHealth;
    [SerializeField] private float requiredSpeedToDamage;

    private float _damageAmount = 1f;

    //private EventReference destroySound; FUTURE USE

    private const string PLAYER_TAG_STRING = "Player";
    private const int MINIMUM_HP_TO_DESTROY = 0;
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(PLAYER_TAG_STRING))
        {
            Rigidbody rb = collision.rigidbody;

            if (rb != null)
            {
                float impactVelocity = rb.linearVelocity.magnitude;

                if (impactVelocity >= requiredSpeedToDamage)
                    TakeDamage(_damageAmount);
            }
        }
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
