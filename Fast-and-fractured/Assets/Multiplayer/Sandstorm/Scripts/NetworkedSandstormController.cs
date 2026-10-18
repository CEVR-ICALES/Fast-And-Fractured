using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Server-authoritative sandstorm: the server schedules it (the level's storm time, brought forward by every
    /// death), publishes the start tick every HUD counts down to, and starts it; the singleplayer timer is taken over.
    /// </summary>
    [RequireComponent(typeof(SandstormController))]
    public class NetworkedSandstormController : NetworkBehaviour
    {
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

        // Server-only overlap ref-counts: a multi-collider car leaves the storm when its last collider exits.
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
        public readonly SyncVar<Vector3> SyncedSpawnPoint = new SyncVar<Vector3>();

        public readonly SyncVar<Vector3> SyncedMirrorPoint = new SyncVar<Vector3>();

        /// <summary>Growth progress (0 to maxGrowth).</summary>
        public readonly SyncVar<float> SyncedCurrentGrowth = new SyncVar<float>();

        public readonly SyncVar<bool> SyncedIsMoving = new SyncVar<bool>();

        /// <summary>Kill time; shrinks as the storm grows.</summary>
        public readonly SyncVar<float> SyncedKillTime = new SyncVar<float>();

        public readonly SyncVar<Vector3> SyncedFogPosition = new SyncVar<Vector3>();
        #endregion

        #region STORM_SCHEDULE
        /// <summary>Server tick the storm starts at (0 until scheduled); every peer counts down to it.</summary>
        public readonly SyncVar<uint> SyncedStormStartTick = new SyncVar<uint>();

        internal static NetworkedSandstormController ServerInstance { get; private set; }

        private const float DEFAULT_TIME_TO_CALL_THE_STORM = 190f;
        private const float DEFAULT_DEATH_REDUCTION = 20f;
        private const float MIN_KILL_TIME = 0.01f;
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
            // Mirror the controller's grown collider to the server hitbox; the original collider stays inert.
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

            // Disabling the collider (not the controller) stops the legacy trigger messages at the source.
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

            // A kinematic body keeps trigger callbacks on this relay even if the parent gets a Rigidbody.
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

            // The singleplayer GameLoopManager may start the storm itself on the host: adopt that start.
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

        /// <summary>Stops the host's singleplayer storm timer (it ignores multiplayer deaths) and carries its remaining time over as the start tick.</summary>
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

        /// <summary>A character died: the storm comes sooner, as in singleplayer.</summary>
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

        /// <summary>The HUD storm timer counts down to the synced start tick; re-shown each time (the 3-2-1 hides the same element).</summary>
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

        /// <summary>Server, migrated match: the same countdown, or the same moving storm at the same size.</summary>
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
        /// <summary>SpawnFogs, with this peer's own HUD arrow bound first (LevelController.LocalPlayer is not reliable here).</summary>
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
        /// <summary>Server: picks the spawn points (DeterministicRandom) and syncs them.</summary>
        [Server]
        public void ServerSetSpawnPoints(bool debug)
        {
            _sandstormController.SetSpawnPoints(debug);
            SyncSpawnPointsToClients(debug);
        }

        [Server]
        private void SyncSpawnPointsToClients(bool debug)
        {
            if (_spawnPointField != null && _mirrorPointField != null)
            {
                SyncedSpawnPoint.Value = (Vector3)_spawnPointField.GetValue(_sandstormController);
                SyncedMirrorPoint.Value = (Vector3)_mirrorPointField.GetValue(_sandstormController);
            }

            RpcSpawnPointsSet(SyncedSpawnPoint.Value, SyncedMirrorPoint.Value, debug);
        }

        [Server]
        public void ServerSpawnFogs()
        {
            SpawnFogsOnThisPeer();
            CopyLegacyColliderGeometry();
            SetServerHitboxActive(true);
            SyncedIsMoving.Value = true;
            
            if (_fogParentTransform != null)
            {
                SyncedFogPosition.Value = _fogParentTransform.position;
            }

            RpcSpawnFogs();
        }

        [Server]
        public void ServerStartMovement()
        {
            _sandstormController.MoveSandStorm = true;
            SyncedIsMoving.Value = true;
            RpcSetMoving(true);
        }

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
            if (_currentGrowthField != null)
            {
                SyncedCurrentGrowth.Value = (float)_currentGrowthField.GetValue(_sandstormController);
            }

            SyncedKillTime.Value = _sandstormController.KillTime;

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

            ApplySpawnPointsOnClient(spawnPoint, mirrorPoint, debug);
        }

        [ObserversRpc]
        private void RpcSpawnFogs()
        {
            if (IsServerInitialized) return;
            SpawnFogsOnClientOnce();
        }

        /// <summary>The RPC and the IsMoving SyncVar arrive in either order; the fog must spawn once.</summary>
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
        // Remote clients only: on the host's client side the storm is the server's own.
        private bool IsRemoteClientCallback(bool asServer) => !asServer && !base.IsServerInitialized;

        private void OnSpawnPointChanged(Vector3 prev, Vector3 next, bool asServer)
        {
            if (asServer) return;
        }

        private void OnMirrorPointChanged(Vector3 prev, Vector3 next, bool asServer)
        {
            if (asServer) return;
        }

        private void OnCurrentGrowthChanged(float prev, float next, bool asServer)
        {
            if (!IsRemoteClientCallback(asServer)) return;

            if (_currentGrowthField != null)
            {
                _currentGrowthField.SetValue(_sandstormController, next);
            }

            // Collider size too, once SpawnFogs has captured its initial size.
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
            if (_spawnPointsSetField == null)
            {
                _spawnPointsSetField = typeof(SandstormController).GetField("_spawnPointsSet",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            }

            if (_spawnPointField != null) _spawnPointField.SetValue(_sandstormController, spawnPoint);
            if (_mirrorPointField != null) _mirrorPointField.SetValue(_sandstormController, mirrorPoint);
            if (_spawnPointsSetField != null) _spawnPointsSetField.SetValue(_sandstormController, true);

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

            float killTime = Mathf.Max(MIN_KILL_TIME, _sandstormController.KillTime);
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
        /// <summary>Server: storm damage, so every client sees the same timing.</summary>
        [Server]
        public void ServerApplySandstormDamage(NetworkObject victim, float damagePerFrame)
        {
            if (!IsServerInitialized) return;

            var stats = victim.GetComponentInChildren<NetworkedStatsController>();
            if (stats != null)
            {
                stats.DealDamageServerRpc(damagePerFrame, null);
            }
        }
        #endregion
    }
}
