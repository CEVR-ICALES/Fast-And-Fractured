using System.Collections;
using System.Collections.Generic;
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
    /// This wrapper snapshots PointExplosion's serialized configuration, removes the legacy
    /// component on every peer, and owns the collision path. The server-only
    /// OnCollisionEnter:
    ///   1. Activates ExplosionForce on the server — FishNet Rigidbody prediction reconcile
    ///      propagates the resulting velocities to all clients automatically.
    ///   2. Routes damage through DealDamageServerRpc after the original endingTime delay.
    ///   3. Broadcasts RpcExplosionTriggered to hide the mesh and deactivate the object on
    ///      all peers simultaneously.
    ///
    /// Setup: add this component and a NetworkObject to each TNT/explosive GameObject.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkedPointExplosion : NetworkBehaviour
    {
        private PointExplosion _wrapped;
        private bool _exploded;

        private ExplosionForce _explosionHitbox;
        private float _explosionRadius = 10f;
        private float _pushForce = 1_000_000f;
        private Vector3 _explosionCenter;
        private float _damagePercentage = 0.5f;
        private GameObject _visuals;
        private float _endingTime = 1.5f;
        private float _startHitTime = 0.2f;
        private float _endHitTime = 1f;

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
        private static readonly FieldInfo _endingTimeField =
            typeof(PointExplosion).GetField("endingTime",          BindingFlags.NonPublic | BindingFlags.Instance);
        // Added by dev (turret/explosion timing): ExplosionForce.ActivateExplosionHitbox now
        // takes startHitTime + endHitTime. Mirror those serialized PointExplosion fields.
        private static readonly FieldInfo _startHitTimeField =
            typeof(PointExplosion).GetField("startHitTime",        BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _endHitTimeField =
            typeof(PointExplosion).GetField("endHitTime",          BindingFlags.NonPublic | BindingFlags.Instance);

        private void Awake()
        {
            _wrapped = GetComponent<PointExplosion>();
            if (_wrapped == null)
                return;

            _explosionHitbox = _explosionHitboxField?.GetValue(_wrapped) as ExplosionForce;
            _explosionRadius = ReadFloat(_explosionRadiusField, _explosionRadius);
            _pushForce = ReadFloat(_pushForceField, _pushForce);
            _explosionCenter = _explosionCenterField != null
                ? (Vector3)_explosionCenterField.GetValue(_wrapped)
                : Vector3.zero;
            _damagePercentage = ReadFloat(_damagePercentageField, _damagePercentage);
            _visuals = _visualsField?.GetValue(_wrapped) as GameObject;
            _endingTime = ReadFloat(_endingTimeField, _endingTime);
            _startHitTime = ReadFloat(_startHitTimeField, _startHitTime);
            _endHitTime = ReadFloat(_endHitTimeField, _endHitTime);

            // PointExplosion.Start used to perform this initialization. Preserve it before
            // removing the component so the network wrapper is self-contained.
            if (_explosionHitbox != null)
                _explosionHitbox.ExplosionCollider = _explosionHitbox.GetComponent<SphereCollider>();

            StartCoroutine(RemoveLegacyAfterColliderQuarantine());
        }

        private float ReadFloat(FieldInfo field, float fallback)
        {
            return field != null ? (float)field.GetValue(_wrapped) : fallback;
        }

        private IEnumerator RemoveLegacyAfterColliderQuarantine()
        {
            var collidersToRestore = new List<Collider>();
            foreach (var collider in GetComponentsInChildren<Collider>(true))
            {
                if (collider == null || !collider.enabled)
                    continue;

                collider.enabled = false;
                collidersToRestore.Add(collider);
            }

            Destroy(_wrapped);
            _wrapped = null;
            yield return null;

            foreach (var collider in collidersToRestore)
            {
                if (collider != null)
                    collider.enabled = true;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!base.IsServerInitialized || _exploded) return;
            // Like dev's PointExplosion: anything with physics sets it off (golf balls too).
            if (!collision.gameObject.TryGetComponent(out PhysicsBehaviour physics)) return;

            _exploded = true;

            // Activate the explosion hitbox on the server.
            // The resulting impulse forces propagate to clients via FishNet rigidbody reconcile.
            if (_explosionHitbox != null)
            {
                // No damage percentage here: damage goes through the networked pipeline below.
                // The TNT is the author (dev credits it as the last thing that pushed the victim).
                _explosionHitbox.ActivateExplosionHitbox(
                    _explosionRadius, _pushForce, _explosionCenter, _startHitTime, _endHitTime, gameObject);
            }

            // Route damage through the networked pipeline using the original endingTime.
            var networkedStats = physics.GetComponentInParent<NetworkedStatsController>();
            if (networkedStats == null) networkedStats = physics.GetComponentInChildren<NetworkedStatsController>();
            if (networkedStats != null && physics.StatsController != null)
            {
                float damage = physics.StatsController.MaxEndurance * _damagePercentage;
                StartCoroutine(DelayedDamage(networkedStats, damage));
            }

            // Hide visuals and deactivate on all clients.
            RpcExplosionTriggered();
        }

        private IEnumerator DelayedDamage(NetworkedStatsController stats, float damage)
        {
            yield return new WaitForSeconds(_endingTime);
            if (stats != null)
                stats.DealDamageServerRpc(damage, null);
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcExplosionTriggered()
        {
            // The TNT's ExplosionForce (which raises the Explosion shake profile) is only activated
            // on the server, so remote clients would never feel the blast. Emit it locally on each
            // peer at the TNT position; Cinemachine attenuates it by distance.
            NetworkedScreenShake.PlayAtPosition(ScreenShakeProfileType.ImpactCollision, transform.position);

            if (_visuals != null) _visuals.SetActive(false);

            StartCoroutine(DeactivateAfterDelay(_endingTime));
        }

        private IEnumerator DeactivateAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            gameObject.SetActive(false);
        }
    }
}
