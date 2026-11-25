using Enums;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    public sealed class GhostShot : NetworkBehaviour
    {
        public const float COOLDOWN_SECONDS = 6f;
        public const float RANGE = 80f;
        public const float FORCE_SCALE = 0.6f;
        private const float RADIUS = 1.5f;
        private const float UPWARD_LIFT = 0.25f;
        private const float IMPULSE_SECONDS = 1f;
        private const float SPEED_LIMIT = 1000f;
        // Lag between the client's cooldown and the server's must not eat a shot.
        private const float SERVER_COOLDOWN_SLACK = 0.25f;
        private const float TRAIL_SECONDS = 0.45f;
        private const float TRAIL_START_WIDTH = 0.6f;
        private const float TRAIL_END_WIDTH = 0.15f;
        private const float IMPACT_LIFETIME = 2f;
        private static readonly Color TRAIL_COLOR = new Color(0.75f, 0.95f, 1f, 0.85f);

        [SerializeField] private Material _trailMaterial;
        [SerializeField] private GameObject _impactVfx;

        public static GhostShot Local { get; private set; }

        private float _localReadyAt;
        private float _serverReadyAt;

        public float CooldownLeft => Mathf.Max(0f, _localReadyAt - Time.time);

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (base.IsOwner) Local = this;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (Local == this) Local = null;
        }

        public bool TryFire(Vector3 origin, Vector3 direction)
        {
            if (CooldownLeft > 0f || !IsValidDirection(direction)) return false;
            _localReadyAt = Time.time + COOLDOWN_SECONDS;
            // A [ServerRpc] called from the host never runs its logic.
            if (base.IsServerInitialized) ServerFire(base.Owner, origin, direction);
            else FireServerRpc(origin, direction);
            return true;
        }

        [ServerRpc]
        private void FireServerRpc(Vector3 origin, Vector3 direction) => ServerFire(base.Owner, origin, direction);

        [Server]
        private void ServerFire(NetworkConnection shooter, Vector3 origin, Vector3 direction)
        {
            if (Time.time < _serverReadyAt || !IsValidDirection(direction) || !IsFinite(origin)) return;
            var ownCar = CarOf(shooter);
            if (ownCar == null || !ownCar.IsDead) return;
            _serverReadyAt = Time.time + COOLDOWN_SECONDS - SERVER_COOLDOWN_SLACK;

            direction.Normalize();
            var victim = FirstAliveCar(origin, direction, ownCar, out Vector3 hitPoint);
            Vector3 end = victim != null ? hitPoint : origin + direction * RANGE;
            if (victim != null)
                Push(victim, direction, hitPoint, ownCar);
            RpcShowShot(origin, end, victim != null);
        }

        private static NetworkedStatsController CarOf(NetworkConnection connection)
        {
            if (connection == null) return null;
            foreach (var stats in FindObjectsByType<NetworkedStatsController>(FindObjectsSortMode.None))
                if (stats.Owner == connection) return stats;
            return null;
        }

        // Walls do not stop a ghost: the nearest living car along the line is hit.
        private static PhysicsBehaviour FirstAliveCar(Vector3 origin, Vector3 direction, NetworkedStatsController ownCar, out Vector3 hitPoint)
        {
            hitPoint = Vector3.zero;
            PhysicsBehaviour best = null;
            float bestDistance = float.MaxValue;
            foreach (var hit in Physics.SphereCastAll(origin, RADIUS, direction, RANGE, ~0, QueryTriggerInteraction.Ignore))
            {
                var car = hit.collider.GetComponentInParent<PhysicsBehaviour>();
                if (car == null || hit.distance >= bestDistance) continue;
                var stats = car.GetComponentInParent<NetworkedStatsController>();
                if (stats == null) stats = car.GetComponentInChildren<NetworkedStatsController>();
                if (stats == ownCar || NetworkedStatsController.IsDeadCar(car.StatsController)) continue;
                best = car;
                bestDistance = hit.distance;
                hitPoint = hit.distance > 0f ? hit.point : car.transform.position;
            }
            return best;
        }

        private static void Push(PhysicsBehaviour car, Vector3 direction, Vector3 hitPoint, NetworkedStatsController ownCar)
        {
            var impact = car.CarImpactHandler;
            if (impact != null && impact.CheckForModifiedCarState() == ModifiedCarState.JOSEFINO_INVULNERABLE)
            {
                impact.OnHasBeenPushed(car);
                return;
            }
            if (car.HasBeenPushed) return;

            car.CancelDash();
            var stats = car.StatsController;
            var shooterStats = ownCar.GetComponentInChildren<StatsController>(true);
            float baseForce = shooterStats != null ? shooterStats.PushShootForce : 0f;
            float force = car.CalculateForceToApplyToOtherCar(stats.Endurance / stats.MaxEndurance, stats.Weight,
                stats.EnduranceImportanceWhenColliding, baseForce * FORCE_SCALE);
            Vector3 push = (direction + Vector3.up * UPWARD_LIFT).normalized;
            car.ApplyImpulse(push, hitPoint, force, ForceMode.Impulse, false, IMPULSE_SECONDS, false, SPEED_LIMIT);
            if (impact != null) impact.OnHasBeenPushed(car);
        }

        [ObserversRpc]
        private void RpcShowShot(Vector3 from, Vector3 to, bool hit)
        {
            ShowTrail(from, to);
            if (hit && _impactVfx != null)
                Destroy(Instantiate(_impactVfx, to, Quaternion.identity), IMPACT_LIFETIME);
        }

        private void ShowTrail(Vector3 from, Vector3 to)
        {
            if (_trailMaterial == null) return;
            var go = new GameObject("GhostShotTrail");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = _trailMaterial;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = TRAIL_START_WIDTH;
            line.endWidth = TRAIL_END_WIDTH;
            line.startColor = line.endColor = TRAIL_COLOR;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<GhostTrailFade>().Begin(line, TRAIL_SECONDS);
        }

        internal static bool IsValidDirection(Vector3 direction) => IsFinite(direction) && direction.sqrMagnitude > 0.0001f;

        private static bool IsFinite(Vector3 v) =>
            !float.IsNaN(v.x) && !float.IsInfinity(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.y) &&
            !float.IsNaN(v.z) && !float.IsInfinity(v.z);

        private sealed class GhostTrailFade : MonoBehaviour
        {
            private LineRenderer _line;
            private float _duration;
            private float _elapsed;
            private Color _start;

            public void Begin(LineRenderer line, float duration)
            {
                _line = line;
                _duration = duration;
                _start = line.startColor;
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(_elapsed / _duration);
                var color = new Color(_start.r, _start.g, _start.b, _start.a * alpha);
                _line.startColor = _line.endColor = color;
                if (_elapsed >= _duration) Destroy(gameObject);
            }
        }
    }
}
