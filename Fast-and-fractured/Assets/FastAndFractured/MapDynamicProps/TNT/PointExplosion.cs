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
        [SerializeField] private float endingTime = 1.5f;
        [SerializeField] private float startHitTime = 0.2f;
        [SerializeField] private float endHitTime = 1f;

        private void Start()
        {
            explosionHitbox.ExplosionCollider = explosionHitbox.GetComponent<SphereCollider>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.TryGetComponent(out PhysicsBehaviour physicsBehaviour))
            {
                ExplodePoint();
            }
        }

        public void ExplodePoint()
        {
            if (explosionHitbox != null)
                {
                    explosionHitbox.ActivateExplosionHitbox(explosionRadius, pushForce, explosionCenterOffset,startHitTime,endHitTime,gameObject);
                    visuals.SetActive(false);
                    TimerSystem.Instance.CreateTimer(endingTime, onTimerDecreaseComplete: () =>
                    {
                        gameObject.SetActive(false);
                    });
                }
        }
    }
}
