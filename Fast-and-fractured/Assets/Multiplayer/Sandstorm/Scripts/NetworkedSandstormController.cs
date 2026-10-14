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
    ///
    /// The server owns WHEN the storm comes: it schedules it (LevelController's time-to-call-the-storm,
    /// brought forward by every death as in singleplayer), publishes the start tick, starts it through
    /// ServerSetSpawnPoints / ServerSpawnFogs / ServerStartMovement, and every peer's HUD counts down
    /// to that tick (TIMER_TEXT). Until 2026-09-30 nothing called those server methods: the storm
    /// could only start through the singleplayer GameLoopManager timer, which never ran in
    /// multiplayer because the map tool's prefab swap had dropped the level's reference to the
    /// storm (and the storm's sphereCenter). No peer saw the storm, its damage hitbox never switched
    /// on, and the HUD storm timer stayed at 00:00. Now that the tool restores that wiring, the
    /// singleplayer timer does start on the host; the server takes it over (see
    /// TakeOverSingleplayerStormTimer) so the storm keeps exactly one clock.
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

        #region STORM_SCHEDULE
        /// <summary>
        /// Server tick at which the storm starts (0 until the level has scheduled it). Synced, so every
        /// peer counts down to the same moment on its own estimate of the server tick.
        /// </summary>
        public readonly SyncVar<uint> SyncedStormStartTick = new SyncVar<uint>();

        internal static NetworkedSandstormController ServerInstance { get; private set; }

        private const float DEFAULT_TIME_TO_CALL_THE_STORM = 190f;
        private const float DEFAULT_DEATH_REDUCTION = 20f;
        private static readonly System.Reflection.FieldInfo LevelTimeToCallTheStorm =
            typeof(LevelController).GetField("_timeToCallTheStorm", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        private static readonly System.Reflection.FieldInfo LevelPlayerDeadReduction =
            typeof(LevelController).GetField("_playerDeadReductionTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        private static readonly System.Reflection.FieldInfo LevelGameLoopManager =
            typeof(LevelController).GetField("_gameLoopManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        private static readonly System.Reflection.FieldInfo GameLoopStormTimer =
            typeof(GameLoopManager).GetField("_callStormTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        private const System.Reflection.BindingFlags PrivateInstance = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        private static readonly System.Reflection.FieldInfo StormMinimapArrow = typeof(SandstormController).GetField("minimapSandstormDirection", PrivateInstance);
        private static readonly System.Reflection.FieldInfo StormDirection = typeof(SandstormController).GetField("_direction", PrivateInstance);
        private static readonly System.Reflection.FieldInfo ArrowDirection = typeof(SandstormDirectionMinimap).GetField("_sandstormDirection", PrivateInstance);
        private static readonly System.Reflection.FieldInfo ArrowCamera = typeof(SandstormDirectionMinimap).GetField("_cameraReference", PrivateInstance);
        private static readonly System.Reflection.FieldInfo ArrowDataReceived = typeof(SandstormDirectionMinimap).GetField("_isDataReceived", PrivateInstance);
        private static readonly System.Reflection.FieldInfo ArrowIcon = typeof(SandstormDirectionMinimap).GetField("arrowIcon", PrivateInstance);

        private bool _stormStarted;
        private bool _clientFogsSpawned;
        private float _deathReductionSeconds = DEFAULT_DEATH_REDUCTION;
        private string _shownTimerText;
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
            ServerInstance = this;
            _stormStarted = false;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            StormCountdownHud.DrivenByNetwork = true;   // the synced countdown below owns the HUD timer
            
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
            StormCountdownHud.DrivenByNetwork = false;
            
            SyncedSpawnPoint.OnChange -= OnSpawnPointChanged;
            SyncedMirrorPoint.OnChange -= OnMirrorPointChanged;
            SyncedCurrentGrowth.OnChange -= OnCurrentGrowthChanged;
            SyncedIsMoving.OnChange -= OnIsMovingChanged;
            SyncedFogPosition.OnChange -= OnFogPositionChanged;
        }

        public override void OnStopServer()
        {
            if (ServerInstance == this) ServerInstance = null;
            SetServerHitboxActive(false);
            ClearServerOverlapState();
            base.OnStopServer();
        }

        private void Update()
        {
            IsolateLegacyCollider();
            UpdateStormCountdownHud();
            if (!IsServerInitialized) return;

            UpdateStormSchedule();

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

        #region STORM_SCHEDULE_METHODS
        [Server]
        private void UpdateStormSchedule()
        {
            if (_stormStarted) return;

            // The singleplayer GameLoopManager may still start the storm itself on the host (a level
            // not in debug mode). Adopt that start instead of racing it.
            if (_sandstormController.MoveSandStorm)
            {
                AdoptStormStartedBySingleplayer();
                return;
            }

            TakeOverSingleplayerStormTimer();

            if (SyncedStormStartTick.Value == 0)
            {
                LevelController level = LevelController.ExistingInstance;
                if (level == null) return;
                float seconds = ReadLevelSeconds(level, LevelTimeToCallTheStorm, DEFAULT_TIME_TO_CALL_THE_STORM);
                _deathReductionSeconds = ReadLevelSeconds(level, LevelPlayerDeadReduction, DEFAULT_DEATH_REDUCTION);
                SyncedStormStartTick.Value = base.TimeManager.Tick + base.TimeManager.TimeToTicks(seconds);
                MpCanary.Mark($"Sandstorm scheduled in {seconds:F0}s (tick {SyncedStormStartTick.Value})");
                return;
            }

            if (base.TimeManager.Tick >= SyncedStormStartTick.Value)
                StartStormOnServer();
        }

        /// <summary>
        /// The level's GameLoopManager starts its own storm timer on the host (LevelController
        /// SetupStorm). That timer knows nothing about multiplayer deaths and would start the storm a
        /// second time - spawning the fogs again - whenever it ran out after ours. Stop it and carry
        /// its remaining time over as the synced start tick: one clock, the singleplayer duration.
        /// </summary>
        [Server]
        private void TakeOverSingleplayerStormTimer()
        {
            LevelController level = LevelController.ExistingInstance;
            if (level == null || LevelGameLoopManager == null || GameLoopStormTimer == null) return;
            object gameLoop = LevelGameLoopManager.GetValue(level);
            if (gameLoop == null || !(GameLoopStormTimer.GetValue(gameLoop) is global::Utilities.ITimer timer)) return;

            GameLoopStormTimer.SetValue(gameLoop, null);
            if (_restoredFromMigration)
            {
                // Keep the migrated schedule; only silence the singleplayer timer.
                var ownTimers = global::Utilities.TimerSystem.ExistingInstance;
                if (ownTimers != null && ownTimers.HasTimer(timer)) ownTimers.StopTimer(timer.GetData().ID);
                return;
            }
            global::Utilities.TimerData data = timer.GetData();
            float remaining = Mathf.Max(0f, data.CurrentTime);
            var timers = global::Utilities.TimerSystem.ExistingInstance;
            if (timers != null && timers.HasTimer(timer)) timers.StopTimer(data.ID);
            else timer.StopTimer();

            SyncedStormStartTick.Value = base.TimeManager.Tick + base.TimeManager.TimeToTicks(remaining);
            MpCanary.Mark($"Sandstorm: took over the level's storm timer, {remaining:F0}s left (tick {SyncedStormStartTick.Value})");
        }

        private static float ReadLevelSeconds(LevelController level, System.Reflection.FieldInfo field, float fallback)
        {
            if (field == null) return fallback;
            float value = (float)field.GetValue(level);
            return value > 0f ? value : fallback;
        }

        [Server]
        private void StartStormOnServer()
        {
            _stormStarted = true;
            try
            {
                // As GameLoopManager.HandleStormCall: keep the points the level already chose.
                if (_sandstormController.StormSpawnPointsSetted) SyncSpawnPointsToClients(false);
                else ServerSetSpawnPoints(false);
                ServerSpawnFogs();
                ServerStartMovement();
                MpCanary.Mark("Sandstorm started");
            }
            catch (System.Exception e)
            {
                MpCanary.Mark($"Sandstorm start FAILED: {e.GetType().Name}: {e.Message}");
                throw;
            }
        }

        [Server]
        private void AdoptStormStartedBySingleplayer()
        {
            _stormStarted = true;
            if (_spawnPointField != null && _mirrorPointField != null)
            {
                SyncedSpawnPoint.Value = (Vector3)_spawnPointField.GetValue(_sandstormController);
                SyncedMirrorPoint.Value = (Vector3)_mirrorPointField.GetValue(_sandstormController);
                RpcSpawnPointsSet(SyncedSpawnPoint.Value, SyncedMirrorPoint.Value, false);
            }
            CopyLegacyColliderGeometry();
            SetServerHitboxActive(true);
            SyncedIsMoving.Value = true;
            if (_fogParentTransform != null)
                SyncedFogPosition.Value = _fogParentTransform.position;
            if (SyncedStormStartTick.Value == 0 || SyncedStormStartTick.Value > base.TimeManager.Tick)
                SyncedStormStartTick.Value = base.TimeManager.Tick;
            RpcSpawnFogs();
            MpCanary.Mark("Sandstorm started by the singleplayer timer; adopted");
        }

        /// <summary>
        /// A character died: the storm comes sooner, like GameLoopManager.OnCharacterDied does to its
        /// singleplayer timer.
        /// </summary>
        [Server]
        public void ServerAdvanceStormForDeath()
        {
            if (_stormStarted || SyncedStormStartTick.Value == 0) return;
            SyncedStormStartTick.Value = AdvancedStormStart(SyncedStormStartTick.Value, base.TimeManager.Tick,
                base.TimeManager.TimeToTicks(_deathReductionSeconds));
        }

        /// <summary>The start tick brought forward by <paramref name="reductionTicks"/>, never into the past.</summary>
        internal static uint AdvancedStormStart(uint startTick, uint nowTick, uint reductionTicks) =>
            startTick > nowTick + reductionTicks ? startTick - reductionTicks : nowTick;

        /// <summary>
        /// Every peer: the HUD storm timer (TIMER_TEXT, "Tmp_StormTimer") shows the time left until
        /// the synced start tick, then 00:00. Written only when the shown second changes; re-shown
        /// each time because the match-start countdown hides the same element when it finishes.
        /// </summary>
        private void UpdateStormCountdownHud()
        {
            uint start = SyncedStormStartTick.Value;
            if (start == 0 || base.TimeManager == null) return;
            HUDManager hud = HUDManager.ExistingInstance;
            if (hud == null) return;

            uint now = base.TimeManager.Tick;
            float remaining = SyncedIsMoving.Value || now >= start ? 0f : (start - now) * (float)base.TimeManager.TickDelta;
            string text = FormatStormCountdown(remaining);
            if (text == _shownTimerText) return;
            _shownTimerText = text;
            hud.UpdateUIElement(Enums.UIDynamicElementType.TIMER_TEXT, true);
            hud.UpdateUIElement(Enums.UIDynamicElementType.TIMER_TEXT, text);
        }

        // ── Host migration ─────────────────────────────────────────────────────────────
        private bool _restoredFromMigration;

        /// <summary>Server: the storm's state for a host migration snapshot.</summary>
        [Server]
        internal void ServerDescribeForMigration(out bool started, out float secondsUntilStorm, out float growth,
            out Vector3 spawnPoint, out Vector3 mirrorPoint)
        {
            started = _stormStarted;
            uint now = base.TimeManager.Tick, start = SyncedStormStartTick.Value;
            secondsUntilStorm = start == 0 ? -1f : start > now ? (start - now) * (float)base.TimeManager.TickDelta : 0f;
            growth = _currentGrowthField != null ? (float)_currentGrowthField.GetValue(_sandstormController) : 0f;
            spawnPoint = SyncedSpawnPoint.Value;
            mirrorPoint = SyncedMirrorPoint.Value;
        }

        /// <summary>
        /// Server, migrated match: continue the storm where the lost host left it - the same
        /// countdown, or the same moving storm from the same side at the same size.
        /// </summary>
        [Server]
        internal void ServerRestoreFromMigration(bool started, float secondsUntilStorm, float growth,
            Vector3 spawnPoint, Vector3 mirrorPoint)
        {
            _restoredFromMigration = true;
            if (!started)
            {
                if (secondsUntilStorm >= 0f)
                    SyncedStormStartTick.Value = base.TimeManager.Tick + base.TimeManager.TimeToTicks(secondsUntilStorm);
                return;
            }

            _stormStarted = true;
            SyncedStormStartTick.Value = base.TimeManager.Tick;
            ApplySpawnPointsOnClient(spawnPoint, mirrorPoint, false);   // sets the singleplayer points on this peer
            SyncSpawnPointsToClients(false);
            ServerSpawnFogs();
            ServerStartMovement();
            if (_currentGrowthField != null) _currentGrowthField.SetValue(_sandstormController, growth);
            MpCanary.Mark($"Sandstorm restored after host migration (growth {growth:F0})");
        }

        internal static string FormatStormCountdown(float remainingSeconds)
        {
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, remainingSeconds));
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }
        #endregion

        #region MINIMAP_ARROW
        /// <summary>
        /// SandstormController.SpawnFogs on this peer, then this peer's minimap storm arrow.
        /// SpawnFogs aims the arrow it finds through LevelController.LocalPlayer, which on the host
        /// is not necessarily the host's car and on a remote client may be unset, so the field is
        /// bound to this peer's own HUD arrow first and the arrow is aimed with this peer's camera.
        /// </summary>
        private void SpawnFogsOnThisPeer()
        {
            NetworkedVehicle localCar = FindLocalCar();
            var arrow = localCar != null ? localCar.transform.root.GetComponentInChildren<SandstormDirectionMinimap>(true) : null;
            StormMinimapArrow?.SetValue(_sandstormController, arrow != null ? arrow.gameObject : null);
            _sandstormController.SpawnFogs();
            AimLocalMinimapArrow();
        }

        private void AimLocalMinimapArrow()
        {
            if (ArrowDirection == null || ArrowCamera == null || ArrowDataReceived == null || StormDirection == null) return;
            NetworkedVehicle localCar = FindLocalCar();
            if (localCar == null) return;   // dedicated server, or this peer's car is gone
            var arrow = localCar.transform.root.GetComponentInChildren<SandstormDirectionMinimap>(true);
            if (arrow == null) return;

            var holder = localCar.transform.parent != null ? localCar.transform.parent.GetComponent<global::Utilities.CameraHolder>() : null;
            Camera camera = holder != null && holder.CameraToHold != null ? holder.CameraToHold : Camera.main;
            ArrowDirection.SetValue(arrow, (Vector3)StormDirection.GetValue(_sandstormController));
            ArrowCamera.SetValue(arrow, camera);
            ArrowDataReceived.SetValue(arrow, true);
            (ArrowIcon?.GetValue(arrow) as GameObject)?.SetActive(true);
        }

        private static NetworkedVehicle FindLocalCar()
        {
            foreach (var car in FindObjectsByType<NetworkedVehicle>(FindObjectsSortMode.None))
                if (car.IsOwner) return car;
            return null;
        }
        #endregion

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
            SyncSpawnPointsToClients(debug);
        }

        [Server]
        private void SyncSpawnPointsToClients(bool debug)
        {
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
            SpawnFogsOnThisPeer();
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
            SpawnFogsOnClientOnce();
        }

        /// <summary>
        /// The RPC and the IsMoving SyncVar can arrive in either order; the fog must spawn exactly
        /// once (SpawnFogs also resets the kill time and raises the "Events.Sandstorm" banner).
        /// </summary>
        private void SpawnFogsOnClientOnce()
        {
            if (_clientFogsSpawned) return;
            _clientFogsSpawned = true;
            if (!_sandstormController.StormSpawnPointsSetted)
                ApplySpawnPointsOnClient(SyncedSpawnPoint.Value, SyncedMirrorPoint.Value, false);
            SpawnFogsOnThisPeer();
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
        // These replicate the server's storm onto a remote client. FishNet also calls them on the
        // host's own client side (asServer == false), where the storm IS the server's: there they
        // started the fogs a second time and resized the collider from an initial size SpawnFogs
        // had not captured yet - leaving the damage box and the fog volume 0 m wide and 0 m tall
        // on every peer (measured 2026-09-30: size (0, 0, 1400)). They only run on remote clients.
        private bool IsRemoteClientCallback(bool asServer) => !asServer && !base.IsServerInitialized;

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
            if (!IsRemoteClientCallback(asServer)) return;

            // Apply growth state on client (cached reflection)
            if (_currentGrowthField != null)
            {
                _currentGrowthField.SetValue(_sandstormController, next);
            }

            // Also update collider size to match - once SpawnFogs has captured its initial size.
            if (_clientFogsSpawned) ApplyColliderState(next);
        }

        private void OnIsMovingChanged(bool prev, bool next, bool asServer)
        {
            if (!IsRemoteClientCallback(asServer)) return;
            if (next) SpawnFogsOnClientOnce();
            _sandstormController.MoveSandStorm = next;
        }

        private void OnFogPositionChanged(Vector3 prev, Vector3 next, bool asServer)
        {
            if (!IsRemoteClientCallback(asServer)) return;
            
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
