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

            // Reproduce CarmeUniqueAbility.CalculateLandingPoint with the same serialized config.
            if (!TryComputeAim(out Vector3 aimDirection, out Vector3 landPoint, shootPoint))
                yield break; // SP flow also refunded the ability (no valid landing point)

            // Destroy the owner's local cosmetic chicken: the networked one replaces it for
            // everyone. (Replicas never spawn one — Carme keeps the events-only RPC path.)
            foreach (var chicken in FindObjectsByType<McChicken>(FindObjectsSortMode.None))
            {
                if (chicken.GetComponent<NetworkObject>() != null) continue;
                if ((chicken.transform.position - shootPoint.position).sqrMagnitude > 400f) continue;
                Destroy(chicken.gameObject);
            }

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

        internal bool InitializeValidatedPredictedChicken(NetworkedMcChickenBehaviour chicken,
            Vector3 requestedLandPoint, Vector3 requestedAim)
        {
            if (!base.IsServerInitialized || chicken == null) return false;
            Transform shootPoint = _localAbility.uniqueAbilityShootPoint != null
                ? _localAbility.uniqueAbilityShootPoint : _localAbility.transform;
            if (!TryComputeAim(out Vector3 aim, out Vector3 landPoint, shootPoint)) return false;
            if ((landPoint - requestedLandPoint).sqrMagnitude > 100f || Vector3.Dot(aim, requestedAim.normalized) < 0.5f)
                return false;
            chicken.transform.SetPositionAndRotation(shootPoint.position,
                aim.sqrMagnitude > 0.001f ? Quaternion.LookRotation(aim) : Quaternion.identity);
            chicken.ServerInitializeChicken(landPoint, aim);
            var abilityData = SingleplayerReflectionCache.BaseUniqueAbility_AbilityData?.GetValue(_localAbility) as AbilityData;
            float lifetime = abilityData != null && abilityData.Duration > 0f ? abilityData.Duration : 15f;
            StartCoroutine(DespawnAfter(chicken.gameObject, lifetime));
            return true;
        }

        private IEnumerator DespawnAfter(GameObject chicken, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (chicken != null && base.IsServerInitialized)
                base.ServerManager.Despawn(chicken);
        }
    }
}
