using System.Collections;
using System.Reflection;
using FishNet.Object;
using UnityEngine;
using FishNet;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Networks Carme's ultimate: the owner's local chicken is replaced by a server-spawned NetworkedMcChicken at
    /// the landing point the owner computed, despawned after AbilityData.Duration.
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

        /// <summary>One frame after activation, once the SP flow has run its raycasts and maybe spawned its chicken.</summary>
        private IEnumerator InterceptLocalChicken()
        {
            yield return null;

            Transform shootPoint = _localAbility.uniqueAbilityShootPoint != null
                ? _localAbility.uniqueAbilityShootPoint
                : _localAbility.transform;

            // The networked chicken replaces the owner's local one.
            bool localChickenSpawned = false;
            foreach (var chicken in FindObjectsByType<McChicken>(FindObjectsSortMode.None))
            {
                if (chicken.GetComponent<NetworkObject>() != null) continue;
                if ((chicken.transform.position - shootPoint.position).sqrMagnitude > MAX_LOCAL_CHICKEN_SQR_DISTANCE) continue;
                localChickenSpawned = true;
                Destroy(chicken.gameObject);
            }

            // No local chicken: the SP flow found no landing point and refunded the cooldown; do not launch a free one.
            if (!localChickenSpawned)
                yield break;

            // As CarmeUniqueAbility.CalculateLandingPoint.
            if (!TryComputeAim(out Vector3 aimDirection, out Vector3 landPoint, shootPoint))
                yield break;

            // A [ServerRpc] called from the host never runs its logic.
            if (base.IsServerInitialized)
                SpawnChickenOnServer(shootPoint.position, landPoint, aimDirection);
            else
                SpawnChickenPredicted(shootPoint.position, landPoint, aimDirection);
        }

        private void SpawnChickenPredicted(Vector3 spawnPosition, Vector3 landPoint, Vector3 aimDirection)
        {
            if (_networkedChickenPrefab == null) return;
            var go = Instantiate(_networkedChickenPrefab, spawnPosition,
                aimDirection.sqrMagnitude > MIN_AIM_SQR_MAGNITUDE ? Quaternion.LookRotation(aimDirection) : Quaternion.identity);
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

            float rayDistance = _rayCastDistanceField != null ? (float)_rayCastDistanceField.GetValue(_localAbility) : DEFAULT_RAYCAST_DISTANCE;
            LayerMask obstacleMask = _obstacleMaskField != null ? (LayerMask)_obstacleMaskField.GetValue(_localAbility) : default;
            LayerMask groundMask = _groundMaskField != null ? (LayerMask)_groundMaskField.GetValue(_localAbility) : default;
            float groundCheckDistance = _groundCheckDistanceField != null ? (float)_groundCheckDistanceField.GetValue(_localAbility) : DEFAULT_GROUND_CHECK_DISTANCE;
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
                aimDirection.sqrMagnitude > MIN_AIM_SQR_MAGNITUDE ? Quaternion.LookRotation(aimDirection) : Quaternion.identity);
            base.ServerManager.Spawn(go, base.Owner);

            var networkedChicken = go.GetComponent<NetworkedMcChickenBehaviour>();
            if (networkedChicken != null)
                networkedChicken.ServerInitializeChicken(landPoint, aimDirection);

            var abilityData = _localAbility != null
                ? SingleplayerReflectionCache.BaseUniqueAbility_AbilityData?.GetValue(_localAbility) as AbilityData
                : null;
            float lifetime = abilityData != null && abilityData.Duration > 0f ? abilityData.Duration : DEFAULT_LIFETIME_SECONDS;
            StartCoroutine(DespawnAfter(go, lifetime));
        }

        // The owner's car keeps moving between aiming and the server receiving the spawn.
        private const float LANDING_RANGE_SLACK = 20f;
        private const float MAX_LOCAL_CHICKEN_SQR_DISTANCE = 400f;
        private const float MIN_AIM_SQR_MAGNITUDE = 0.001f;
        private const float DEFAULT_RAYCAST_DISTANCE = 50f;
        private const float DEFAULT_GROUND_CHECK_DISTANCE = 100f;
        private const float GROUND_CHECK_HEIGHT = 5f;
        private const float GROUND_CHECK_SLACK = 10f;
        private const float DEFAULT_LIFETIME_SECONDS = 15f;

        /// <summary>
        /// Accepts the owner's landing point if this car could have produced it: within reach, with ground under it.
        /// The server cannot recompute the aim (the owner's camera and velocity).
        /// </summary>
        internal bool InitializeValidatedPredictedChicken(NetworkedMcChickenBehaviour chicken,
            Vector3 requestedLandPoint, Vector3 requestedAim)
        {
            if (!base.IsServerInitialized || chicken == null || _localAbility == null) return false;
            Transform shootPoint = _localAbility.uniqueAbilityShootPoint != null
                ? _localAbility.uniqueAbilityShootPoint : _localAbility.transform;

            if (!IsFinite(requestedLandPoint) || !IsFinite(requestedAim)) return false;

            float rayDistance = _rayCastDistanceField != null ? (float)_rayCastDistanceField.GetValue(_localAbility) : DEFAULT_RAYCAST_DISTANCE;
            Vector3 flatOffset = Vector3.ProjectOnPlane(requestedLandPoint - shootPoint.position, Vector3.up);
            if (flatOffset.magnitude > rayDistance + LANDING_RANGE_SLACK) return false;

            LayerMask groundMask = _groundMaskField != null ? (LayerMask)_groundMaskField.GetValue(_localAbility) : ~0;
            float groundCheckDistance = _groundCheckDistanceField != null ? (float)_groundCheckDistanceField.GetValue(_localAbility) : DEFAULT_GROUND_CHECK_DISTANCE;
            if (!Physics.Raycast(requestedLandPoint + Vector3.up * GROUND_CHECK_HEIGHT, Vector3.down, groundCheckDistance + GROUND_CHECK_SLACK, groundMask))
                return false;

            Vector3 aim = Vector3.ProjectOnPlane(requestedAim, Vector3.up);
            aim = aim.sqrMagnitude > MIN_AIM_SQR_MAGNITUDE ? aim.normalized : flatOffset.normalized;
            Vector3 landPoint = requestedLandPoint;

            chicken.transform.SetPositionAndRotation(shootPoint.position,
                aim.sqrMagnitude > MIN_AIM_SQR_MAGNITUDE ? Quaternion.LookRotation(aim) : Quaternion.identity);
            // Launched in OnStartServer: started before the spawn, the server's jump never ran.
            chicken.QueueServerLaunch(landPoint, aim);
            var abilityData = SingleplayerReflectionCache.BaseUniqueAbility_AbilityData?.GetValue(_localAbility) as AbilityData;
            float lifetime = abilityData != null && abilityData.Duration > 0f ? abilityData.Duration : DEFAULT_LIFETIME_SECONDS;
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
