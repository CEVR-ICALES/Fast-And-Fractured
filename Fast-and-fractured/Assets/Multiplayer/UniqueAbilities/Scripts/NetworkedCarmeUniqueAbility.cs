using System.Collections;
using System.Reflection;
using FishNet.Object;
using UnityEngine;
using FishNet;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Server-authoritative handler for Carme's unique ability (the McChicken creature).
    ///
    /// The singleplayer CarmeUniqueAbility Instantiates a local McChicken with an aim direction
    /// and landing point only the owner computes, so remote peers never see the chicken and the
    /// host never simulates it authoritatively. Mirrors the NetworkedPepeUniqueAbility pattern:
    ///
    /// 1. Owner activates the ability (SP flow runs: VFX, cooldown, local chicken spawn).
    /// 2. This hook recomputes the same aim/landing raycast the SP code just ran (reading the
    ///    serialized config via reflection), destroys the owner's local cosmetic chicken and
    ///    asks the server to spawn the networked variant instead.
    /// 3. NetworkedMcChickenBehaviour on the spawned prefab syncs position/state to all clients
    ///    and RpcInitializeChicken launches it everywhere.
    /// 4. The server despawns it after AbilityData.Duration (same lifetime as SP).
    /// </summary>
    public class NetworkedCarmeUniqueAbility : NetworkBehaviour
    {
        [Header("Networked Prefabs")]
        [SerializeField] private GameObject _networkedChickenPrefab;

        private CarmeUniqueAbility _localAbility;

        private static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly FieldInfo _rayCastDistanceField = typeof(CarmeUniqueAbility).GetField("rayCastDistance", PrivateInstance);
        private static readonly FieldInfo _obstacleMaskField = typeof(CarmeUniqueAbility).GetField("obstacleLayerMask", PrivateInstance);
        private static readonly FieldInfo _groundMaskField = typeof(CarmeUniqueAbility).GetField("groundLayerMask", PrivateInstance);
        private static readonly FieldInfo _groundCheckDistanceField = typeof(CarmeUniqueAbility).GetField("groundCheckDistance", PrivateInstance);
        private static readonly FieldInfo _landPointYOffsetField = typeof(CarmeUniqueAbility).GetField("landPointYOffset", PrivateInstance);
        private static readonly FieldInfo _physicsBehaviourField = typeof(CarmeUniqueAbility).GetField("physicsBehaviour", PrivateInstance);
        private static readonly FieldInfo _shootingHandleField = typeof(CarmeUniqueAbility).GetField("shootingHandle", PrivateInstance);

        private void Awake()
        {
            _localAbility = GetComponentInChildren<CarmeUniqueAbility>(true);
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            if (_localAbility == null)
            {
                Debug.LogWarning($"[NetworkedCarmeUniqueAbility] No CarmeUniqueAbility found on {gameObject.name}");
                enabled = false;
                return;
            }

            _localAbility.onAbilityActivated.AddListener(OnAbilityActivated);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (_localAbility != null)
                _localAbility.onAbilityActivated.RemoveListener(OnAbilityActivated);
        }

        private void OnAbilityActivated()
        {
            if (!base.IsOwner) return;
            StartCoroutine(InterceptLocalChicken());
        }

        /// <summary>
        /// Runs one frame after activation so the SP flow has finished its raycasts and (maybe)
        /// spawned its local chicken.
        /// </summary>
        private IEnumerator InterceptLocalChicken()
        {
            yield return null;

            Transform shootPoint = _localAbility.uniqueAbilityShootPoint != null
                ? _localAbility.uniqueAbilityShootPoint
                : _localAbility.transform;

            // Destroy the owner's local cosmetic chicken: the networked one replaces it for
            // everyone. (Replicas never spawn one — Carme keeps the events-only RPC path.)
            bool localChickenSpawned = false;
            foreach (var chicken in FindObjectsByType<McChicken>(FindObjectsSortMode.None))
            {
                if (chicken.GetComponent<NetworkObject>() != null) continue;
                if ((chicken.transform.position - shootPoint.position).sqrMagnitude > 400f) continue;
                localChickenSpawned = true;
                Destroy(chicken.gameObject);
            }

            // No local chicken: the SP flow found no landing point and refunded the cooldown.
            // Re-running the raycasts a frame later could still find ground and launch a free one.
            if (!localChickenSpawned)
                yield break;

            // Reproduce CarmeUniqueAbility.CalculateLandingPoint with the same serialized config.
            if (!TryComputeAim(out Vector3 aimDirection, out Vector3 landPoint, shootPoint))
                yield break;

            // HOST: [ServerRpc] from a server context never delivers (FishNet pitfall,
            // live-debugged on Pepe's tomato 2026-07-11). Call the server logic directly.
            if (base.IsServerInitialized)
                SpawnChickenOnServer(shootPoint.position, landPoint, aimDirection);
            else
                SpawnChickenPredicted(shootPoint.position, landPoint, aimDirection);
        }

        private void SpawnChickenPredicted(Vector3 spawnPosition, Vector3 landPoint, Vector3 aimDirection)
        {
            if (_networkedChickenPrefab == null) return;
            var go = Instantiate(_networkedChickenPrefab, spawnPosition,
                aimDirection.sqrMagnitude > 0.001f ? Quaternion.LookRotation(aimDirection) : Quaternion.identity);
            var payload = go.GetComponent<NetworkedPredictedAbilitySpawn>();
            if (payload == null) { Destroy(go); return; }
            payload.ConfigureCarme(base.NetworkObject, landPoint, aimDirection);
            InstanceFinder.ServerManager.Spawn(go, base.Owner);
        }

        private bool TryComputeAim(out Vector3 aimDirection, out Vector3 landPoint, Transform shootPoint)
        {
            aimDirection = Vector3.zero;
            landPoint = Vector3.zero;

            var shootingHandle = _shootingHandleField?.GetValue(_localAbility) as ShootingHandle;
            var physicsBehaviour = _physicsBehaviourField?.GetValue(_localAbility) as PhysicsBehaviour;
            if (shootingHandle == null || physicsBehaviour == null) return false;

            aimDirection = shootingHandle.CurrentShootDirection + physicsBehaviour.Rb.linearVelocity;
            aimDirection = Vector3.ProjectOnPlane(aimDirection, Vector3.up).normalized;

            float rayDistance = _rayCastDistanceField != null ? (float)_rayCastDistanceField.GetValue(_localAbility) : 50f;
            LayerMask obstacleMask = _obstacleMaskField != null ? (LayerMask)_obstacleMaskField.GetValue(_localAbility) : default;
            LayerMask groundMask = _groundMaskField != null ? (LayerMask)_groundMaskField.GetValue(_localAbility) : default;
            float groundCheckDistance = _groundCheckDistanceField != null ? (float)_groundCheckDistanceField.GetValue(_localAbility) : 100f;
            float yOffset = _landPointYOffsetField != null ? (float)_landPointYOffsetField.GetValue(_localAbility) : 0f;

            bool hitObstacle = Physics.Raycast(shootPoint.position, aimDirection, out RaycastHit primaryHit, rayDistance, obstacleMask);
            Vector3 checkPoint = hitObstacle ? primaryHit.point : shootPoint.position + aimDirection * rayDistance;

            if (!Physics.Raycast(checkPoint, Vector3.down, out RaycastHit groundHit, groundCheckDistance, groundMask))
                return false;

            landPoint = groundHit.point;
            landPoint.y += yOffset;
            return true;
        }

        [ServerRpc]
        private void SpawnChickenServerRpc(Vector3 spawnPosition, Vector3 landPoint, Vector3 aimDirection)
        {
            SpawnChickenOnServer(spawnPosition, landPoint, aimDirection);
        }

        [Server]
        private void SpawnChickenOnServer(Vector3 spawnPosition, Vector3 landPoint, Vector3 aimDirection)
        {
            if (!base.IsServerInitialized) return;
            if (_networkedChickenPrefab == null)
            {
                Debug.LogError("[NetworkedCarmeUniqueAbility] _networkedChickenPrefab not assigned. Run Tools > Fast And Fractured > Ability Networking Setup.");
                return;
            }

            var go = Instantiate(_networkedChickenPrefab, spawnPosition,
                aimDirection.sqrMagnitude > 0.001f ? Quaternion.LookRotation(aimDirection) : Quaternion.identity);
            base.ServerManager.Spawn(go, base.Owner);

            var networkedChicken = go.GetComponent<NetworkedMcChickenBehaviour>();
            if (networkedChicken != null)
                networkedChicken.ServerInitializeChicken(landPoint, aimDirection);

            var abilityData = _localAbility != null
                ? SingleplayerReflectionCache.BaseUniqueAbility_AbilityData?.GetValue(_localAbility) as AbilityData
                : null;
            float lifetime = abilityData != null && abilityData.Duration > 0f ? abilityData.Duration : 15f;
            StartCoroutine(DespawnAfter(go, lifetime));
        }

        // How far past the ability's own raycast a requested landing point may be: the owner's car
        // keeps moving between aiming and the server receiving the spawn.
        private const float LANDING_RANGE_SLACK = 20f;

        /// <summary>
        /// Accepts the owner's landing point when it is one the ability could have produced from this
        /// car: within the ability's reach, with ground under it.
        ///
        /// The server used to recompute the whole aim itself and compare, but that aim is the OWNER's
        /// camera direction plus its car velocity - neither of which the server has - so the recomputed
        /// point was elsewhere and every predicted chicken was rejected (ShootingProbe 2026-09-30:
        /// "PredictedAbility REJECT payload", the chicken vanished on Carme's screen after 70 ms).
        /// </summary>
        internal bool InitializeValidatedPredictedChicken(NetworkedMcChickenBehaviour chicken,
            Vector3 requestedLandPoint, Vector3 requestedAim)
        {
            if (!base.IsServerInitialized || chicken == null || _localAbility == null) return false;
            Transform shootPoint = _localAbility.uniqueAbilityShootPoint != null
                ? _localAbility.uniqueAbilityShootPoint : _localAbility.transform;

            if (!IsFinite(requestedLandPoint) || !IsFinite(requestedAim)) return false;

            float rayDistance = _rayCastDistanceField != null ? (float)_rayCastDistanceField.GetValue(_localAbility) : 50f;
            Vector3 flatOffset = Vector3.ProjectOnPlane(requestedLandPoint - shootPoint.position, Vector3.up);
            if (flatOffset.magnitude > rayDistance + LANDING_RANGE_SLACK) return false;

            LayerMask groundMask = _groundMaskField != null ? (LayerMask)_groundMaskField.GetValue(_localAbility) : ~0;
            float groundCheckDistance = _groundCheckDistanceField != null ? (float)_groundCheckDistanceField.GetValue(_localAbility) : 100f;
            if (!Physics.Raycast(requestedLandPoint + Vector3.up * 5f, Vector3.down, groundCheckDistance + 10f, groundMask))
                return false;

            Vector3 aim = Vector3.ProjectOnPlane(requestedAim, Vector3.up);
            aim = aim.sqrMagnitude > 0.001f ? aim.normalized : flatOffset.normalized;
            Vector3 landPoint = requestedLandPoint;

            chicken.transform.SetPositionAndRotation(shootPoint.position,
                aim.sqrMagnitude > 0.001f ? Quaternion.LookRotation(aim) : Quaternion.identity);
            // Launched once the object is really spawned (OnStartServer). Started in here - before
            // the spawn - the server's jump never ran: the server chicken sat at Carme's gun for its
            // whole life while only her own screen saw it jump (ShootingProbe 2026-09-30).
            chicken.QueueServerLaunch(landPoint, aim);
            var abilityData = SingleplayerReflectionCache.BaseUniqueAbility_AbilityData?.GetValue(_localAbility) as AbilityData;
            float lifetime = abilityData != null && abilityData.Duration > 0f ? abilityData.Duration : 15f;
            StartCoroutine(DespawnAfter(chicken.gameObject, lifetime));
            return true;
        }

        private static bool IsFinite(Vector3 v) =>
            !float.IsNaN(v.x) && !float.IsInfinity(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.y) &&
            !float.IsNaN(v.z) && !float.IsInfinity(v.z);

        private IEnumerator DespawnAfter(GameObject chicken, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (chicken != null && base.IsServerInitialized)
                base.ServerManager.Despawn(chicken);
        }
    }
}
