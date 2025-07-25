using System.Threading;
using UnityEngine;
using Utilities;

namespace FastAndFractured
{

    public class PointExplosion : MonoBehaviour
    {
        // itll have radius, push force and center
        [SerializeField] private float explosionRadius = 10f;
        [SerializeField] private float pushForce = 1000000f;
        [SerializeField] private Vector3 explosionCenterOffset = Vector3.zero;
        [SerializeField] private ExplosionForce explosionHitbox;
        [SerializeField] private float damagePercentage = 0.5f;
        [SerializeField] private GameObject visuals;

        private void Start()
        {
            explosionHitbox.ExplosionCollider = explosionHitbox.GetComponent<SphereCollider>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.TryGetComponent(out PhysicsBehaviour physicsBehaviour) && physicsBehaviour.StatsController != null)
            {
                if (explosionHitbox != null)
                {
                    explosionHitbox.ActivateExplosionHitbox(explosionRadius, pushForce, explosionCenterOffset);
                    visuals.SetActive(false);
                    TimerSystem.Instance.CreateTimer(0.5f, onTimerDecreaseComplete: () =>
                    {
                        physicsBehaviour.StatsController.TakeEndurance(physicsBehaviour.StatsController.MaxEndurance * damagePercentage, false, gameObject);
                        gameObject.SetActive(false);
                    });
                }
            }
        }
    }
}
