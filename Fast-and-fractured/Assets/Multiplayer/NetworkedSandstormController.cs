using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Networked wrapper for SandstormController. Syncs zone shrinking state server-authoritatively.
    /// 
    /// The sandstorm is critical for battle royale - all clients must see the same zone boundaries
    /// and take damage at the same time.
    /// </summary>
    [RequireComponent(typeof(SandstormController))]
    public class NetworkedSandstormController : NetworkBehaviour
    {
        // Cached reflection field handles (looked up once per session, not per Update tick).
        private static System.Reflection.FieldInfo _spawnPointField;
        private static System.Reflection.FieldInfo _mirrorPointField;
        private static System.Reflection.FieldInfo _currentGrowthField;
        private static System.Reflection.FieldInfo _maxGrowthField;
        private static System.Reflection.FieldInfo _spawnPointsSetField;
        private static System.Reflection.FieldInfo _initialColliderSizeField;
        private static bool _reflectionCached;

        private SandstormController _sandstormController;
        private BoxCollider _stormCollider;
        private BoxCollider _networkHitbox;
        private NetworkedSandstormHitboxRelay _hitboxRelay;
        private Transform _fogParentTransform;
        private bool _stormHitboxActive;

        // Server-only overlap state. Ref-counts make multi-collider vehicles and props leave the
        // storm only after their final collider exits the isolated hitbox.
        private readonly Dictionary<NetworkedStatsController, int> _victimOverlapCounts = new();
        private readonly Dictionary<NetworkedStatsController, float> _victimDamagePerSecond = new();
        private readonly Dictionary<NetworkedStatsController, GameObject> _victimObjects = new();
        private readonly Dictionary<GameObject, int> _itemOverlapCounts = new();
        private readonly Dictionary<GameObject, int> _safeZoneOverlapCounts = new();
        private readonly List<NetworkedStatsController> _victimRemovalBuffer = new();

        private static void EnsureReflectionCached()
        {
            if (_reflectionCached) return;
            const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            _spawnPointField = typeof(SandstormController).GetField("_spawnPoint", Flags);
            _mirrorPointField = typeof(SandstormController).GetField("_mirrorPoint", Flags);
            _currentGrowthField = typeof(SandstormController).GetField("_currentGrowth", Flags);
            _maxGrowthField = typeof(SandstormController).GetField("_maxGrowth", Flags);
            _spawnPointsSetField = typeof(SandstormController).GetField("_spawnPointsSet", Flags);
            _initialColliderSizeField = typeof(SandstormController).GetField("_initialColliderSize", Flags);
            _reflectionCached = true;
        }

        #region SYNCED_STATE
        /// <summary>
        /// Spawn point determined by server. Synced to clients on game start.
        /// </summary>
        public readonly SyncVar<Vector3> SyncedSpawnPoint = new SyncVar<Vector3>();

        /// <summary>
        /// Mirror point determined by server. Synced to clients on game start.
        /// </summary>
        public readonly SyncVar<Vector3> SyncedMirrorPoint = new SyncVar<Vector3>();

        /// <summary>
        /// Current growth progress (0 to maxGrowth). Synced periodically.
        /// </summary>
        public readonly SyncVar<float> SyncedCurrentGrowth = new SyncVar<float>();

        /// <summary>
        /// Whether the sandstorm is actively moving/expanding.
        /// </summary>
        public readonly SyncVar<bool> SyncedIsMoving = new SyncVar<bool>();

        /// <summary>
        /// Current kill time (reduces as storm expands).
        /// </summary>
        public readonly SyncVar<float> SyncedKillTime = new SyncVar<float>();

        /// <summary>
        /// Fog parent position for visual sync.
        /// </summary>
        public readonly SyncVar<Vector3> SyncedFogPosition = new SyncVar<Vector3>();
        #endregion

        #region SERVER_STATE
        private float _syncTimer = 0f;
        private float _damageTickTimer;
        private const float SYNC_INTERVAL = 0.1f; // Sync 10 times per second
        private const float DAMAGE_TICK_INTERVAL = 0.1f;
        #endregion

        private void Awake()
        {
            _sandstormController = GetComponent<SandstormController>();
            _stormCollider = GetComponent<BoxCollider>();

            if (_sandstormController.fogParent != null)
            {
                _fogParentTransform = _sandstormController.fogParent.transform;
            }

            EnsureReflectionCached();
            InstallIsolatedHitbox();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Initialize SyncVars from controller state
            SyncedIsMoving.Value = _sandstormController.MoveSandStorm;
            SyncedKillTime.Value = _sandstormController.KillTime;
            SetServerHitboxActive(false);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Subscribe to SyncVar changes for client-side state application
            SyncedSpawnPoint.OnChange += OnSpawnPointChanged;
            SyncedMirrorPoint.OnChange += OnMirrorPointChanged;
            SyncedCurrentGrowth.OnChange += OnCurrentGrowthChanged;
            SyncedIsMoving.OnChange += OnIsMovingChanged;
            SyncedFogPosition.OnChange += OnFogPositionChanged;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            
            SyncedSpawnPoint.OnChange -= OnSpawnPointChanged;
            SyncedMirrorPoint.OnChange -= OnMirrorPointChanged;
            SyncedCurrentGrowth.OnChange -= OnCurrentGrowthChanged;
            SyncedIsMoving.OnChange -= OnIsMovingChanged;
            SyncedFogPosition.OnChange -= OnFogPositionChanged;
        }

        public override void OnStopServer()
        {
            SetServerHitboxActive(false);
            ClearServerOverlapState();
            base.OnStopServer();
        }

        private void Update()
        {
            IsolateLegacyCollider();
            if (!IsServerInitialized) return;

            // Periodically sync state to clients
            _syncTimer += Time.deltaTime;
            if (_syncTimer >= SYNC_INTERVAL)
            {
                _syncTimer = 0f;
                SyncStateToClients();
            }

            _damageTickTimer += Time.deltaTime;
            if (_damageTickTimer >= DAMAGE_TICK_INTERVAL)
            {
                float elapsed = _damageTickTimer;
                _damageTickTimer = 0f;
                ApplySandstormDamageTick(elapsed);
            }
        }

        private void LateUpdate()
        {
            // SandstormController grows its cached collider in Update. Mirror that final geometry
            // to the dedicated server hitbox, then keep the legacy collider physically inert.
            CopyLegacyColliderGeometry();
            IsolateLegacyCollider();
        }

        private void InstallIsolatedHitbox()
        {
            if (_stormCollider == null)
            {
                Debug.LogError("[NetworkedSandstormController] Sandstorm BoxCollider not found.", this);
                return;
            }

            // Disable the source collider immediately. Unlike disabling SandstormController, this
            // prevents the legacy OnTriggerEnter/Exit messages at the physics source itself.
            _stormCollider.enabled = false;

            const string HitboxName = "[Multiplayer] Sandstorm Hitbox";
            Transform existing = transform.Find(HitboxName);
            GameObject hitboxObject;
            if (existing != null)
            {
                hitboxObject = existing.gameObject;
            }
            else
            {
                hitboxObject = new GameObject(HitboxName);
                hitboxObject.transform.SetParent(transform, false);
            }

            hitboxObject.layer = gameObject.layer;

            _networkHitbox = hitboxObject.GetComponent<BoxCollider>();
            if (_networkHitbox == null)
                _networkHitbox = hitboxObject.AddComponent<BoxCollider>();

            _networkHitbox.isTrigger = true;
            _networkHitbox.enabled = false;

            // A local kinematic body anchors trigger delivery to this child. Even if a map variant
            // later adds a Rigidbody to the parent, callbacks remain on the relay, not legacy code.
            Rigidbody relayBody = hitboxObject.GetComponent<Rigidbody>();
            if (relayBody == null)
                relayBody = hitboxObject.AddComponent<Rigidbody>();
            relayBody.isKinematic = true;
            relayBody.useGravity = false;
            relayBody.constraints = RigidbodyConstraints.FreezeAll;

            _hitboxRelay = hitboxObject.GetComponent<NetworkedSandstormHitboxRelay>();
            if (_hitboxRelay == null)
                _hitboxRelay = hitboxObject.AddComponent<NetworkedSandstormHitboxRelay>();
            _hitboxRelay.Configure(this);

            CopyLegacyColliderGeometry();
        }

        private void CopyLegacyColliderGeometry()
        {
            if (_stormCollider == null || _networkHitbox == null) return;

            _networkHitbox.center = _stormCollider.center;
            _networkHitbox.size = _stormCollider.size;
            _networkHitbox.sharedMaterial = _stormCollider.sharedMaterial;
            _networkHitbox.contactOffset = _stormCollider.contactOffset;
        }

        private void IsolateLegacyCollider()
        {
            if (_stormCollider != null && _stormCollider.enabled)
                _stormCollider.enabled = false;

            if (_networkHitbox != null)
            {
                bool shouldBeEnabled = IsServerInitialized && _stormHitboxActive;
                if (_networkHitbox.enabled != shouldBeEnabled)
                    _networkHitbox.enabled = shouldBeEnabled;
            }
        }

        private void SetServerHitboxActive(bool active)
        {
            _stormHitboxActive = active;
            IsolateLegacyCollider();
        }

        #region SERVER_METHODS
        /// <summary>
        /// Called by NetworkedLevelControllerManager or game loop to initialize sandstorm spawn points.
        /// Server-only - determines the random spawn angle and syncs to all clients.
        /// </summary>
        [Server]
        public void ServerSetSpawnPoints(bool debug)
        {
            // Let the original controller set spawn points (uses DeterministicRandom)
            _sandstormController.SetSpawnPoints(debug);

            // Extract and sync the spawn/mirror points via cached reflection (no public accessor exists).
            if (_spawnPointField != null && _mirrorPointField != null)
            {
                SyncedSpawnPoint.Value = (Vector3)_spawnPointField.GetValue(_sandstormController);
                SyncedMirrorPoint.Value = (Vector3)_mirrorPointField.GetValue(_sandstormController);
            }

            // Notify all clients
            RpcSpawnPointsSet(SyncedSpawnPoint.Value, SyncedMirrorPoint.Value, debug);
        }

        /// <summary>
        /// Called to start the sandstorm movement/expansion. Server-authoritative.
        /// </summary>
        [Server]
        public void ServerSpawnFogs()
        {
            _sandstormController.SpawnFogs();
            CopyLegacyColliderGeometry();
            SetServerHitboxActive(true);
            SyncedIsMoving.Value = true;
            
            // Sync initial fog position
            if (_fogParentTransform != null)
            {
                SyncedFogPosition.Value = _fogParentTransform.position;
            }

            RpcSpawnFogs();
        }

        /// <summary>
        /// Called to start sandstorm movement. Server-authoritative.
        /// </summary>
        [Server]
        public void ServerStartMovement()
        {
            _sandstormController.MoveSandStorm = true;
            SyncedIsMoving.Value = true;
            RpcSetMoving(true);
        }

        /// <summary>
        /// Called to stop sandstorm movement. Server-authoritative.
        /// </summary>
        [Server]
        public void ServerStopMovement()
        {
            _sandstormController.MoveSandStorm = false;
            SyncedIsMoving.Value = false;
            RpcSetMoving(false);
        }

        [Server]
        private void SyncStateToClients()
        {
            // Sync current growth (cached reflection, no public accessor)
            if (_currentGrowthField != null)
            {
                SyncedCurrentGrowth.Value = (float)_currentGrowthField.GetValue(_sandstormController);
            }

            // Sync kill time
            SyncedKillTime.Value = _sandstormController.KillTime;

            // Sync fog position
            if (_fogParentTransform != null)
            {
                SyncedFogPosition.Value = _fogParentTransform.position;
            }
        }
        #endregion

        #region OBSERVER_RPCS
        [ObserversRpc]
        private void RpcSpawnPointsSet(Vector3 spawnPoint, Vector3 mirrorPoint, bool debug)
        {
            if (IsServerInitialized) return;

            // Apply spawn points on clients
            ApplySpawnPointsOnClient(spawnPoint, mirrorPoint, debug);
        }

        [ObserversRpc]
        private void RpcSpawnFogs()
        {
            if (IsServerInitialized) return;

            // Trigger fog spawn visuals on clients
            _sandstormController.SpawnFogs();
            CopyLegacyColliderGeometry();
            IsolateLegacyCollider();
        }

        [ObserversRpc]
        private void RpcSetMoving(bool isMoving)
        {
            if (IsServerInitialized) return;

            _sandstormController.MoveSandStorm = isMoving;
        }
        #endregion

        #region CLIENT_SYNCVAR_HANDLERS
        private void OnSpawnPointChanged(Vector3 prev, Vector3 next, bool asServer)
        {
            if (asServer) return;
            // Spawn point will be applied via RpcSpawnPointsSet
        }

        private void OnMirrorPointChanged(Vector3 prev, Vector3 next, bool asServer)
        {
            if (asServer) return;
            // Mirror point will be applied via RpcSpawnPointsSet
        }

        private void OnCurrentGrowthChanged(float prev, float next, bool asServer)
        {
            if (asServer) return;

            // Apply growth state on client (cached reflection)
            if (_currentGrowthField != null)
            {
                _currentGrowthField.SetValue(_sandstormController, next);
            }

            // Also update collider size to match
            ApplyColliderState(next);
        }

        private void OnIsMovingChanged(bool prev, bool next, bool asServer)
        {
            if (asServer) return;
            _sandstormController.MoveSandStorm = next;
        }

        private void OnFogPositionChanged(Vector3 prev, Vector3 next, bool asServer)
        {
            if (asServer) return;
            
            if (_fogParentTransform != null)
            {
                _fogParentTransform.position = next;
            }
        }
        #endregion

        #region CLIENT_HELPERS
        private void ApplySpawnPointsOnClient(Vector3 spawnPoint, Vector3 mirrorPoint, bool debug)
        {
            // _spawnPointsSet is only ever read on this path so we look it up lazily once.
            if (_spawnPointsSetField == null)
            {
                _spawnPointsSetField = typeof(SandstormController).GetField("_spawnPointsSet",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            }

            if (_spawnPointField != null) _spawnPointField.SetValue(_sandstormController, spawnPoint);
            if (_mirrorPointField != null) _mirrorPointField.SetValue(_sandstormController, mirrorPoint);
            if (_spawnPointsSetField != null) _spawnPointsSetField.SetValue(_sandstormController, true);

            // Calculate maxGrowth
            float maxGrowth = (mirrorPoint - spawnPoint).magnitude;
            if (_maxGrowthField != null) _maxGrowthField.SetValue(_sandstormController, maxGrowth);
        }

        private void ApplyColliderState(float currentGrowth)
        {
            if (_stormCollider == null) return;

            if (_initialColliderSizeField != null)
            {
                Vector3 initialSize = (Vector3)_initialColliderSizeField.GetValue(_sandstormController);
                float newZSize = initialSize.z + currentGrowth;
                _stormCollider.size = new Vector3(initialSize.x, initialSize.y, newZSize);
            }
        }
        #endregion

        #region ISOLATED_SERVER_HITBOX
        internal void HandleStormTriggerEnter(Collider other)
        {
            if (!IsServerInitialized || !_stormHitboxActive || other == null) return;

            StatsController coreStats = other.GetComponentInParent<StatsController>();
            if (coreStats != null)
            {
                Rigidbody body = other.attachedRigidbody;
                if (body != null && body.isKinematic) return;

                NetworkedStatsController networkedStats =
                    coreStats.GetComponent<NetworkedStatsController>() ??
                    coreStats.GetComponentInParent<NetworkedStatsController>();
                if (networkedStats == null) return;

                RegisterStormVictim(networkedStats, coreStats.gameObject);
                return;
            }

            StatsBoostInteractable boost = other.GetComponentInParent<StatsBoostInteractable>();
            if (boost != null)
            {
                RegisterListOverlap(_itemOverlapCounts,
                    _sandstormController.ItemsInsideSandstorm, boost.gameObject);
            }
            else
            {
                RegisterListOverlap(_safeZoneOverlapCounts,
                    _sandstormController.SafeZonesInsideSandstorm, other.gameObject);
            }
        }

        internal void HandleStormTriggerExit(Collider other)
        {
            if (!IsServerInitialized || other == null) return;

            StatsController coreStats = other.GetComponentInParent<StatsController>();
            if (coreStats != null)
            {
                NetworkedStatsController networkedStats =
                    coreStats.GetComponent<NetworkedStatsController>() ??
                    coreStats.GetComponentInParent<NetworkedStatsController>();
                if (networkedStats != null)
                    UnregisterStormVictim(networkedStats);
                return;
            }

            StatsBoostInteractable boost = other.GetComponentInParent<StatsBoostInteractable>();
            if (boost != null)
            {
                UnregisterListOverlap(_itemOverlapCounts,
                    _sandstormController.ItemsInsideSandstorm, boost.gameObject);
            }
            else
            {
                UnregisterListOverlap(_safeZoneOverlapCounts,
                    _sandstormController.SafeZonesInsideSandstorm, other.gameObject);
            }
        }

        private void RegisterStormVictim(NetworkedStatsController stats, GameObject characterObject)
        {
            if (_victimOverlapCounts.TryGetValue(stats, out int overlapCount))
            {
                _victimOverlapCounts[stats] = overlapCount + 1;
                return;
            }

            _victimOverlapCounts.Add(stats, 1);
            _victimObjects.Add(stats, characterObject);

            float killTime = Mathf.Max(0.01f, _sandstormController.KillTime);
            float startingEndurance = Mathf.Max(0f, stats.GetSyncedEndurance());
            _victimDamagePerSecond.Add(stats, startingEndurance / killTime);

            List<GameObject> characters = _sandstormController.CharactersInsideSandstorm;
            if (characters != null && characterObject != null && !characters.Contains(characterObject))
                characters.Add(characterObject);
        }

        private void UnregisterStormVictim(NetworkedStatsController stats)
        {
            if (!_victimOverlapCounts.TryGetValue(stats, out int overlapCount)) return;

            if (overlapCount > 1)
            {
                _victimOverlapCounts[stats] = overlapCount - 1;
                return;
            }

            RemoveStormVictim(stats);
        }

        private void RemoveStormVictim(NetworkedStatsController stats)
        {
            if (_victimObjects.TryGetValue(stats, out GameObject characterObject))
            {
                List<GameObject> characters = _sandstormController.CharactersInsideSandstorm;
                if (characters != null)
                    characters.Remove(characterObject);
            }

            _victimOverlapCounts.Remove(stats);
            _victimDamagePerSecond.Remove(stats);
            _victimObjects.Remove(stats);
        }

        private void ApplySandstormDamageTick(float elapsed)
        {
            if (!_stormHitboxActive || elapsed <= 0f || _victimDamagePerSecond.Count == 0)
                return;

            _victimRemovalBuffer.Clear();
            foreach (KeyValuePair<NetworkedStatsController, float> entry in _victimDamagePerSecond)
            {
                NetworkedStatsController stats = entry.Key;
                if (stats == null || !stats.IsAlive())
                {
                    _victimRemovalBuffer.Add(stats);
                    continue;
                }

                float damage = entry.Value * elapsed;
                if (damage > 0f)
                    stats.DealDamageServerRpc(damage, null);
            }

            for (int i = 0; i < _victimRemovalBuffer.Count; i++)
                RemoveStormVictim(_victimRemovalBuffer[i]);
        }

        private static void RegisterListOverlap(
            Dictionary<GameObject, int> overlapCounts,
            List<GameObject> trackedObjects,
            GameObject target)
        {
            if (target == null) return;

            if (overlapCounts.TryGetValue(target, out int overlapCount))
            {
                overlapCounts[target] = overlapCount + 1;
                return;
            }

            overlapCounts.Add(target, 1);
            if (trackedObjects != null && !trackedObjects.Contains(target))
                trackedObjects.Add(target);
        }

        private static void UnregisterListOverlap(
            Dictionary<GameObject, int> overlapCounts,
            List<GameObject> trackedObjects,
            GameObject target)
        {
            if (target == null || !overlapCounts.TryGetValue(target, out int overlapCount)) return;

            if (overlapCount > 1)
            {
                overlapCounts[target] = overlapCount - 1;
                return;
            }

            overlapCounts.Remove(target);
            if (trackedObjects != null)
                trackedObjects.Remove(target);
        }

        private void ClearServerOverlapState()
        {
            List<GameObject> characters = _sandstormController != null
                ? _sandstormController.CharactersInsideSandstorm
                : null;
            if (characters != null)
            {
                foreach (GameObject characterObject in _victimObjects.Values)
                    characters.Remove(characterObject);
            }

            RemoveTrackedObjects(_itemOverlapCounts,
                _sandstormController != null ? _sandstormController.ItemsInsideSandstorm : null);
            RemoveTrackedObjects(_safeZoneOverlapCounts,
                _sandstormController != null ? _sandstormController.SafeZonesInsideSandstorm : null);

            _victimOverlapCounts.Clear();
            _victimDamagePerSecond.Clear();
            _victimObjects.Clear();
            _victimRemovalBuffer.Clear();
            _damageTickTimer = 0f;
        }

        private static void RemoveTrackedObjects(
            Dictionary<GameObject, int> overlapCounts,
            List<GameObject> trackedObjects)
        {
            if (trackedObjects != null)
            {
                foreach (GameObject target in overlapCounts.Keys)
                    trackedObjects.Remove(target);
            }

            overlapCounts.Clear();
        }
        #endregion

        #region SERVER_RPC_DAMAGE
        /// <summary>
        /// Server-authoritative damage from sandstorm. Called when character enters storm zone.
        /// Ensures all clients see the same damage timing.
        /// </summary>
        [Server]
        public void ServerApplySandstormDamage(NetworkObject victim, float damagePerFrame)
        {
            if (!IsServerInitialized) return;

            var stats = victim.GetComponentInChildren<NetworkedStatsController>();
            if (stats != null)
            {
                // Apply damage through networked stats controller (null attacker = environmental damage)
                stats.DealDamageServerRpc(damagePerFrame, null);
            }
        }
        #endregion
    }
}
