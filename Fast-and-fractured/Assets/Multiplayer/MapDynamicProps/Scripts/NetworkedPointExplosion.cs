using System.Collections.Generic;
using System.Reflection;
using FishNet.Object;
using UnityEngine;
using Utilities;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Server-authoritative TNT (PointExplosion): the legacy component is removed on every peer after its settings
    /// are copied, and only the server explodes it, damaging through the networked pipeline.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkedPointExplosion : NetworkBehaviour
    {
        private const float DAMAGE_SCAN_INTERVAL = 0.1f;

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
        // ExplosionForce.ActivateExplosionHitbox takes these PointExplosion timings.
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

            // PointExplosion.Start's initialization, kept before the component is removed.
            if (_explosionHitbox != null)
                _explosionHitbox.ExplosionCollider = _explosionHitbox.GetComponent<SphereCollider>();

            RemoveLegacyBehindColliderQuarantine();
        }

        private float ReadFloat(FieldInfo field, float fallback)
        {
            return field != null ? (float)field.GetValue(_wrapped) : fallback;
        }

        private readonly List<Collider> _collidersToRestore = new List<Collider>();
        private readonly HashSet<NetworkedStatsController> _damaged = new HashSet<NetworkedStatsController>();
        private ITimer _damageTimer;
        private ITimer _deactivateTimer;

        private void RemoveLegacyBehindColliderQuarantine()
        {
            foreach (var collider in GetComponentsInChildren<Collider>(true))
            {
                if (collider == null || !collider.enabled)
                    continue;

                collider.enabled = false;
                _collidersToRestore.Add(collider);
            }

            Destroy(_wrapped);
            _wrapped = null;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            RestoreColliders();
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            MatchTimers.Stop(ref _damageTimer);
            MatchTimers.Stop(ref _deactivateTimer);
        }

        private void RestoreColliders()
        {
            foreach (var collider in _collidersToRestore)
            {
                if (collider != null)
                    collider.enabled = true;
            }
            _collidersToRestore.Clear();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!base.IsServerInitialized || _exploded) return;
            // As PointExplosion: anything with physics sets it off.
            if (!collision.gameObject.TryGetComponent(out PhysicsBehaviour physics)) return;

            _exploded = true;

            if (_explosionHitbox != null)
            {
                // No damage here (it goes through the networked pipeline below); the TNT is the author.
                _explosionHitbox.ActivateExplosionHitbox(
                    _explosionRadius, _pushForce, _explosionCenter, _startHitTime, _endHitTime, gameObject);
            }

            StartRadiusDamage();

            RpcExplosionTriggered();
        }

        [Server]
        private void StartRadiusDamage()
        {
            if (_damagePercentage <= 0f) return;
            _damaged.Clear();
            float window = Mathf.Max(0f, _endHitTime - _startHitTime);
            _damageTimer = MatchTimers.After(_startHitTime, () => ScanRadiusDamage(window));
        }

        private void ScanRadiusDamage(float remainingWindow)
        {
            _damageTimer = null;
            if (this == null || !base.IsServerInitialized) return;

            Transform origin = _explosionHitbox != null ? _explosionHitbox.transform : transform;
            Vector3 center = origin.TransformPoint(_explosionCenter);
            Vector3 scale = origin.lossyScale;
            float radius = _explosionRadius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
            foreach (var hit in Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.TryGetComponent(out StatsController core)) continue;   // the car body collider
                var stats = hit.GetComponentInParent<NetworkedStatsController>();
                if (stats == null) stats = hit.GetComponentInChildren<NetworkedStatsController>();
                if (stats == null || !_damaged.Add(stats)) continue;
                stats.DealEnvironmentDamageServer(core.MaxEndurance * _damagePercentage);
            }

            float nextWindow = remainingWindow - DAMAGE_SCAN_INTERVAL;
            if (nextWindow > 0f)
                _damageTimer = MatchTimers.After(DAMAGE_SCAN_INTERVAL, () => ScanRadiusDamage(nextWindow));
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcExplosionTriggered()
        {
            // The hitbox that raises the shake only runs on the server: shake every peer here.
            NetworkedScreenShake.PlayAtPosition(ScreenShakeProfileType.ImpactCollision, transform.position);

            if (_visuals != null) _visuals.SetActive(false);
            if (!base.IsServerInitialized) ShowClientBlast();

            MatchTimers.Stop(ref _deactivateTimer);
            _deactivateTimer = MatchTimers.After(_endingTime, DeactivateAfterExplosion);
        }

        private void DeactivateAfterExplosion()
        {
            _deactivateTimer = null;
            MatchTimers.Stop(ref _damageTimer);
            if (this != null) gameObject.SetActive(false);
        }

        private static readonly FieldInfo _explosionVfxField =
            typeof(ExplosionForce).GetField("_explosionVFX", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>Clients: the blast VFX lives on the server-only hitbox, so show it here with its colliders off.</summary>
        private void ShowClientBlast()
        {
            if (_explosionHitbox == null) return;
            foreach (var collider in _explosionHitbox.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            if (_explosionVfxField?.GetValue(_explosionHitbox) is Transform vfx)
                vfx.localScale = Vector3.one * _explosionRadius;
            _explosionHitbox.gameObject.SetActive(true);
        }

    }
}
