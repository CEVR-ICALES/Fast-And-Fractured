using Enums;
using FMODUnity;
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

    //private EventReference destroySound; FUTURE USE
    #endregion
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

            if (propType == PropType.GENERIC)
            {
                this.gameObject.SetActive(false);
            }
            else if (propType == PropType.TREE)
            {
                if (intactTreeModel != null) intactTreeModel.SetActive(false);
                if (trunkTreeModel != null) intactTreeModel.SetActive(true);
            }
        }
        else
        {
            if (propType == PropType.TREE)
                collisionParticles.Play();
        }
    }
}
