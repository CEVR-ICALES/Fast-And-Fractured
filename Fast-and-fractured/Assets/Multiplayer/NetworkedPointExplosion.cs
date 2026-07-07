using System.Collections;
using System.Reflection;
using FishNet.Object;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Server-authoritative wrapper for PointExplosion (TNT crates and similar map hazards).
    ///
    /// PointExplosion.OnCollisionEnter runs on all clients because physics collision events
    /// fire everywhere. It calls StatsController.TakeEndurance directly (bypassing the
    /// networked damage pipeline) and activates ExplosionForce (applying AddForce to all
    /// rigidbodies in range). In multiplayer this produces double-damage and force desync.
    ///
    /// This wrapper disables PointExplosion on non-server clients. The server-only
    /// OnCollisionEnter:
    ///   1. Activates ExplosionForce on the server — FishNet Rigidbody prediction reconcile
    ///      propagates the resulting velocities to all clients automatically.
    ///   2. Routes damage through DealDamageServerRpc (same 0.5 s delay as the original).
    ///   3. Broadcasts RpcExplosionTriggered to hide the mesh and deactivate the object on
    ///      all peers simultaneously.
    ///
    /// Setup: add this component and a NetworkObject to each TNT/explosive GameObject.
    /// </summary>
    [RequireComponent(typeof(PointExplosion))]
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkedPointExplosion : NetworkBehaviour
    {
        private PointExplosion _wrapped;
        private bool _exploded;

        // Reflection handles — PointExplosion fields are [SerializeField] private.
        private static readonly FieldInfo _explosionHitboxField =
            typeof(PointExplosion).GetField("explosionHitbox",      BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _explosionRadiusField =
            typeof(PointExplosion).GetField("explosionRadius",      BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _pushForceField =
            typeof(PointExplosion).GetField("pushForce",            BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _explosionCenterField =
            typeof(PointExplosion).GetField("explosionCenterOffset", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _damagePercentageField =
            typeof(PointExplosion).GetField("damagePercentage",     BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _visualsField =
            typeof(PointExplosion).GetField("visuals",              BindingFlags.NonPublic | BindingFlags.Instance);

        private void Awake()
        {
            _wrapped = GetComponent<PointExplosion>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            // Non-server clients must not run the singleplayer explosion logic.
            if (_wrapped != null && !base.IsServerInitialized)
                _wrapped.enabled = false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!base.IsServerInitialized || _exploded) return;
            if (!collision.gameObject.TryGetComponent(out PhysicsBehaviour physics)) return;
            if (physics.StatsController == null) return;

            _exploded = true;

            // Activate the explosion hitbox on the server.
            // The resulting impulse forces propagate to clients via FishNet rigidbody reconcile.
            var hitbox = _explosionHitboxField?.GetValue(_wrapped) as ExplosionForce;
            if (hitbox != null)
            {
                float radius  = _explosionRadiusField  != null ? (float)_explosionRadiusField.GetValue(_wrapped)  : 10f;
                float force   = _pushForceField        != null ? (float)_pushForceField.GetValue(_wrapped)        : 1_000_000f;
                Vector3 center = _explosionCenterField != null ? (Vector3)_explosionCenterField.GetValue(_wrapped) : Vector3.zero;
                hitbox.ActivateExplosionHitbox(radius, force, center);
            }

            // Route damage through the networked pipeline (0.5 s delay matches original).
            var networkedStats = physics.GetComponentInParent<NetworkedStatsController>();
            if (networkedStats == null) networkedStats = physics.GetComponentInChildren<NetworkedStatsController>();
            if (networkedStats != null)
            {
                float pct    = _damagePercentageField != null ? (float)_damagePercentageField.GetValue(_wrapped) : 0.5f;
                float damage = physics.StatsController.MaxEndurance * pct;
                StartCoroutine(DelayedDamage(networkedStats, damage));
            }

            // Hide visuals and deactivate on all clients.
            RpcExplosionTriggered();
        }

        private IEnumerator DelayedDamage(NetworkedStatsController stats, float damage)
        {
            yield return new WaitForSeconds(0.5f);
            if (stats != null)
                stats.DealDamageServerRpc(damage, null);
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcExplosionTriggered()
        {
            var visuals = _visualsField?.GetValue(_wrapped) as GameObject;
            if (visuals != null) visuals.SetActive(false);

            StartCoroutine(DeactivateAfterDelay(0.6f));
        }

        private IEnumerator DeactivateAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            gameObject.SetActive(false);
        }
    }
}
