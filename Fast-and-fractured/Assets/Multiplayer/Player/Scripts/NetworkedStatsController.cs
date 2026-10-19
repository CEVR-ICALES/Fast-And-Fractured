using Assets.SimpleLocalization.Scripts;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using UnityEngine;
using FastAndFractured;
using Enums;
using System.Reflection;
using Utilities;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Networks a car's StatsController: endurance, invulnerability, skin and death.</summary>
    [RequireComponent(typeof(StatsController))]
    public class NetworkedStatsController : NetworkBehaviour
    {
        #region REFERENCES
        private StatsController _coreStats;
        private static NetworkedLevelControllerManager _cachedLevelControllerManager;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;
        
        [Header("VFX")]
        [SerializeField] private GameObject dieExplosionVfx;
        #endregion
        
        #region STATS_TRACKING
        public readonly SyncVar<float> TotalDamageDealt = new();
        public readonly SyncVar<float> TotalDamageTaken = new();
        public readonly SyncVar<float> TotalDistanceTraveled = new();
        
        private Vector3 _lastPosition;
        #endregion

        #region SYNCED_STATE
        private readonly SyncVar<float> _syncedEndurance = new();
        
        private readonly SyncVar<bool> _syncedInvulnerable = new();

        /// <summary>Spawn character code (e.g. "Pepe_2"): the skin is a material swap only the server applies, so every peer re-applies it.</summary>
        private readonly SyncVar<string> _skinNameCode = new();
        
        /// <summary>Stops the OnChange -> SyncVar write loop.</summary>
        private bool _isUpdatingFromNetwork = false;

        /// <summary>Each death is reported once: the damage and environment paths both reach HandleServerSideDeath.</summary>
        private bool _serverDeathReported = false;
        
        // From SingleplayerReflectionCache, so a singleplayer rename fails loudly in one place.
        private static FieldInfo EnduranceField => SingleplayerReflectionCache.StatsController_CurrentEndurance;
        private static FieldInfo IsPlayerField => SingleplayerReflectionCache.StatsController_IsPlayer;
        private static FieldInfo DeadTimerField => SingleplayerReflectionCache.StatsController_DeadTimer;
        private static FieldInfo CurrentKillerField => SingleplayerReflectionCache.StatsController_CurrentKiller;
        #endregion

        #region LIFECYCLE
        private void Awake()
        {
            _coreStats = GetComponent<StatsController>();

            if (_coreStats == null)
            {
                Debug.LogError("[NetworkedStatsController] StatsController component not found!");
                enabled = false;
            }

            _syncedEndurance.OnChange += OnEnduranceChanged;
            _syncedInvulnerable.OnChange += OnInvulnerabilityChanged;
            _skinNameCode.OnChange += OnSkinNameCodeChanged;
        }

        /// <summary>Server: records the car's character code so clients reproduce its skin; call after Spawn.</summary>
        [Server]
        public void SetSkinNameCode(string nameCode)
        {
            if (string.IsNullOrEmpty(nameCode)) return;
            _skinNameCode.Value = nameCode;
        }

        private void OnSkinNameCodeChanged(string prev, string next, bool asServer)
        {
            ApplySkinFromNameCode(next);
        }

        /// <summary>Re-runs the singleplayer skin swap locally; a throwaway provider is enough.</summary>
        private void ApplySkinFromNameCode(string nameCode)
        {
            if (string.IsNullOrEmpty(nameCode)) return;
            try
            {
                new CharacterDataProvider(null).SetCharacterSkin(nameCode, gameObject);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[NetworkedStatsController] Failed to apply skin '{nameCode}' on " +
                                 $"'{gameObject.name}': {ex.Message}");
            }
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();

            _syncedEndurance.Value = _coreStats.Endurance;
            _syncedInvulnerable.Value = _coreStats.IsInvulnerable;

            _lastPosition = transform.position;

            InstallNetworkDeathListener();

            if (verboseLogging)
                Debug.Log($"<color=cyan>[NetworkedStatsController] Server initialized. Endurance: {_syncedEndurance.Value}</color>");
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (_coreStats != null && _coreStats.onDead != null)
                _coreStats.onDead.RemoveListener(OnLocalStatsDeath);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            CancelPendingSingleplayerDeath();
        }

        /// <summary>Client mirror of the server-side strip: a local dead-wall death must not load the main menu.</summary>
        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!IsServerInitialized)
            {
                OnEnduranceChanged(0f, _syncedEndurance.Value, false);
                OnInvulnerabilityChanged(false, _syncedInvulnerable.Value, false);

                // OnChange does not fire for the buffered initial value: apply it now.
                ApplySkinFromNameCode(_skinNameCode.Value);

                InstallNetworkDeathListener();
            }

            if (verboseLogging)
                Debug.Log($"<color=cyan>[NetworkedStatsController] Client initialized. Endurance: {_syncedEndurance.Value}</color>");
        }

        /// <summary>Installs the multiplayer death listener; the wrapper removes LevelController's own a frame later.</summary>
        private void InstallNetworkDeathListener()
        {
            if (_coreStats == null || _coreStats.onDead == null) return;

            _coreStats.onDead.RemoveListener(OnLocalStatsDeath);
            if (IsServerInitialized)
                _coreStats.onDead.AddListener(OnLocalStatsDeath);

            if (verboseLogging)
                Debug.Log($"[NetworkedStatsController] Network death listener installed on " +
                          $"'{gameObject.name}' server={IsServerInitialized}.");
        }

        internal void RemoveSingleplayerDeathListener(LevelController levelController)
        {
            if (_coreStats == null || _coreStats.onDead == null)
                return;

            if (levelController != null)
                _coreStats.onDead.RemoveListener(levelController.ReportCharacterDeath);

            InstallNetworkDeathListener();
        }

        /// <summary>Server: routes environment deaths (StatsController.onDead) into the networked death flow.</summary>
        private void OnLocalStatsDeath(float delay, GameObject character, bool isPlayer)
        {
            if (!IsServerInitialized) return;
            if (_serverDeathReported) return;

            if (verboseLogging)
                Debug.Log($"<color=red>[NetworkedStatsController] {gameObject.name} died via local source " +
                          "(environment / DeadWall / hole). Forwarding to server-side death handler.</color>");

            // null killer = environment kill (no attacking connection to credit).
            HandleServerSideDeath(null);
        }
        
        private void FixedUpdate()
        {
            if (IsServerInitialized)
            {
                float dist = Vector3.Distance(transform.position, _lastPosition);
                if (dist > MIN_DISTANCE_STEP)
                {
                    TotalDistanceTraveled.Value += dist;
                    _lastPosition = transform.position;
                }
            }
        }

        #endregion
        
        #region SYNCVAR_CALLBACKS
        private void OnEnduranceChanged(float prev, float next, bool asServer)
        {
            if (_coreStats == null) return;

            // The server already applied the damage: only refresh the VFX of remote cars.
            if (IsServerInitialized)
            {
                if (!IsOwner)
                {
                    var vfxController = _coreStats.GetComponentInChildren<VehicleVfxController>();
                    if (vfxController != null && _coreStats.MaxEndurance > 0)
                    {
                        vfxController.HandleOnEnduranceChanged(next / _coreStats.MaxEndurance);
                    }
                }
                else
                {
                    // Server-side writes (an environment kill sets 0) skip TakeEndurance: mirror the synced value to the host's HUD.
                    if (HUDManager.HasInstance && _coreStats.MaxEndurance > 0)
                    {
                        HUDManager.Instance.UpdateUIElement(UIDynamicElementType.HEALTH_BAR, next, _coreStats.MaxEndurance);
                    }

                    var hostVfx = _coreStats.GetComponentInChildren<VehicleVfxController>();
                    if (hostVfx != null && _coreStats.MaxEndurance > 0)
                    {
                        hostVfx.HandleOnEnduranceChanged(next / _coreStats.MaxEndurance);
                    }
                }
                return;
            }

            _isUpdatingFromNetwork = true;

            if (IsOwner)
            {
                if (EnduranceField != null)
                {
                    EnduranceField.SetValue(_coreStats, next);
                }

                if (HUDManager.HasInstance && _coreStats.MaxEndurance > 0)
                {
                    HUDManager.Instance.UpdateUIElement(UIDynamicElementType.HEALTH_BAR, next, _coreStats.MaxEndurance);
                }

                var ownerVfx = _coreStats.GetComponentInChildren<VehicleVfxController>();
                if (ownerVfx != null && _coreStats.MaxEndurance > 0)
                {
                    ownerVfx.HandleOnEnduranceChanged(next / _coreStats.MaxEndurance);
                }
            }
            else
            {
                if (EnduranceField != null)
                {
                    EnduranceField.SetValue(_coreStats, next);
                }
                var vfxController = _coreStats.GetComponentInChildren<VehicleVfxController>();
                if (vfxController != null && _coreStats.MaxEndurance > 0)
                {
                    vfxController.HandleOnEnduranceChanged(next / _coreStats.MaxEndurance);
                }
            }

            _isUpdatingFromNetwork = false;

            if (verboseLogging)
                Debug.Log($"<color=yellow>[NetworkedStatsController] Endurance changed: {prev:F1} -> {next:F1} (asServer: {asServer}, IsOwner: {IsOwner})</color>");
        }
        
        private void OnInvulnerabilityChanged(bool prev, bool next, bool asServer)
        {
            if (_coreStats == null) return;
            
            _isUpdatingFromNetwork = true;
            
            if (next && !prev)
            {
                _coreStats.ActivateInvulnerability();
            }
            else if (!next && prev)
            {
                _coreStats.LoseInvulnerability();
            }
            
            _isUpdatingFromNetwork = false;
            
            if (verboseLogging)
                Debug.Log($"<color=yellow>[NetworkedStatsController] Invulnerability: {prev} -> {next}</color>");
        }
        #endregion
        
        #region DAMAGE_SYSTEM
        // Anti-cheat caps for the client-callable RPCs (real bullets do a few hundred at most).
        private const float MAX_DAMAGE_PER_CALL = 500f;
        private const float MAX_HEAL_PER_CALL = 500f;
        private const float MIN_DISTANCE_STEP = 0.01f;
        private const float DEATH_VFX_LIFETIME_SECONDS = 5f;
        private const float DEBUG_AMOUNT = 50f;
        // Skips DeathCaller and HandleServerSideDeath.
        private const int DEATH_CALLER_SKIPPED_FRAMES = 2;
        private const int DEATH_CALLER_FRAMES = 4;
        
        /// <summary>Any client may call it; the server validates, applies and syncs the damage.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void DealDamageServerRpc(float damage, NetworkConnection attacker)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[NetworkedStatsController] DealDamageServerRpc called before server initialized!");
                return;
            }

            if (damage <= 0f)
            {
                Debug.LogWarning($"[NetworkedStatsController] Invalid damage value: {damage}");
                return;
            }

            // Anti-cheat sanity cap (silently clamp instead of reject so legitimate edge cases still apply).
            if (damage > MAX_DAMAGE_PER_CALL)
            {
                Debug.LogWarning($"[NetworkedStatsController] Damage {damage:F1} exceeds cap {MAX_DAMAGE_PER_CALL}, clamping (possible cheat attempt).");
                damage = MAX_DAMAGE_PER_CALL;
            }

            ApplyDamageOnServer(damage, attacker);
        }

        /// <summary>Server: map damage (TNT, explosions); not client-callable, so uncapped.</summary>
        [Server]
        public void DealEnvironmentDamageServer(float damage, NetworkConnection attacker = null)
        {
            if (damage <= 0f) return;
            ApplyDamageOnServer(damage, attacker);
        }

        /// <summary>Server: damage from a car that may have no connection (AI bullets), credited to that car.</summary>
        [Server]
        public void DealDamageFromCarServer(float damage, NetworkObject attackerCar)
        {
            if (damage <= 0f) return;
            var owner = attackerCar != null && attackerCar.Owner != null && attackerCar.Owner.IsValid ? attackerCar.Owner : null;
            ApplyDamageOnServer(damage, owner, attackerCar);
        }

        // Kill credit: a car pushed into a pit or the storm shortly after a hit counts as killed by the hitter.
        private NetworkObject _lastAttacker;
        private float _lastAttackerTime;
        private const float LAST_HIT_CREDIT_SECONDS = 8f;

        /// <summary>Server: remember the car that just hit or pushed this one.</summary>
        [Server]
        public void ServerNoteAttacker(NetworkObject attackerCar)
        {
            if (attackerCar == null) return;
            var self = GetComponentInParent<NetworkObject>();
            if (self != null && (attackerCar == self || attackerCar.transform.IsChildOf(self.transform) ||
                                 self.transform.IsChildOf(attackerCar.transform)))
                return;
            _lastAttacker = attackerCar;
            _lastAttackerTime = Time.time;
        }

        private NetworkObject RecentAttacker() =>
            _lastAttacker != null && Time.time - _lastAttackerTime <= LAST_HIT_CREDIT_SECONDS ? _lastAttacker : null;

        [Server]
        private void ApplyDamageOnServer(float damage, NetworkConnection attacker, NetworkObject attackerCar = null)
        {
            if (attackerCar == null && attacker != null && attacker.FirstObject != null)
                attackerCar = attacker.FirstObject;
            ServerNoteAttacker(attackerCar);
            
            GameObject attackerObject = attackerCar != null ? attackerCar.gameObject : null;
            
            if (verboseLogging)
            {
                string attackerName = attackerObject != null ? attackerObject.name : "Unknown";
                Debug.Log($"<color=red>[NetworkedStatsController] {gameObject.name} taking {damage:F1} damage from {attackerName}</color>");
            }
            
            // Josefino's shield (active until it is broken, not only during the 5 s of damage
            // invulnerability) absorbs this hit and breaks (see NetworkedUniqueAbility).
            var ownerRoot = GetComponentInParent<NetworkObject>();
            var ultimate = ownerRoot != null ? ownerRoot.GetComponentInChildren<NetworkedUniqueAbility>(true) : null;
            if (ultimate != null && ultimate.ServerConsumeJosefinoShield())
                return;

            // Non-owner cars must not update the local HUD singleton.
            ApplyEnduranceDamage(damage, attackerObject ?? gameObject);
            
            _syncedEndurance.Value = _coreStats.Endurance;
            
            TotalDamageTaken.Value += damage;
            
            if (attackerCar != null)
            {
                var attackerStats = attackerCar.GetComponentInChildren<NetworkedStatsController>();
                if (attackerStats == null) attackerStats = attackerCar.GetComponentInParent<NetworkedStatsController>();
                if (attackerStats != null && attackerStats != this)
                    attackerStats.TotalDamageDealt.Value += damage;
            }

            if (_coreStats.Endurance <= 0f && IsServerInitialized)
            {
                HandleServerSideDeath(attacker);
            }
        }
        
        /// <summary>TakeEndurance with HUD updates suppressed for non-owner cars (HUDManager is a singleton).</summary>
        private void ApplyEnduranceDamage(float damage, GameObject attacker)
        {
            bool needsSuppress = !IsOwner && IsPlayerField != null;
            if (needsSuppress)
            {
                IsPlayerField.SetValue(_coreStats, false);
            }
            
            _coreStats.TakeEndurance(damage, false, attacker);
            
            if (needsSuppress)
            {
                IsPlayerField.SetValue(_coreStats, true);
            }
        }
        
        [ServerRpc]
        public void HealServerRpc(float amount)
        {
            if (!IsServerInitialized) return;

            if (amount <= 0f)
            {
                Debug.LogWarning($"[NetworkedStatsController] Invalid heal amount: {amount}");
                return;
            }

            // Anti-cheat sanity cap.
            if (amount > MAX_HEAL_PER_CALL)
            {
                Debug.LogWarning($"[NetworkedStatsController] Heal {amount:F1} exceeds cap {MAX_HEAL_PER_CALL}, clamping (possible cheat attempt).");
                amount = MAX_HEAL_PER_CALL;
            }

            // Same HUD suppression as ApplyEnduranceDamage for non-owner vehicles
            bool needsSuppress = !IsOwner && IsPlayerField != null;
            if (needsSuppress) IsPlayerField.SetValue(_coreStats, false);
            _coreStats.RecoverEndurance(amount, false);
            if (needsSuppress) IsPlayerField.SetValue(_coreStats, true);
            
            _syncedEndurance.Value = _coreStats.Endurance;
            
            if (verboseLogging)
                Debug.Log($"<color=green>[NetworkedStatsController] {gameObject.name} healed {amount:F1}. New endurance: {_syncedEndurance.Value:F1}</color>");
        }
        
        [ServerRpc]
        public void SetInvulnerabilityServerRpc(bool invulnerable, float duration = 0f)
        {
            SetInvulnerabilityServer(invulnerable, duration);
        }

        public void SetInvulnerabilityServer(bool invulnerable, float duration = 0f)
        {
            if (!IsServerInitialized) return;

            CancelInvoke(nameof(RemoveInvulnerabilityInternal));
            _syncedInvulnerable.Value = invulnerable;

            if (invulnerable && duration > 0f)
            {
                Invoke(nameof(RemoveInvulnerabilityInternal), duration);
            }

            if (verboseLogging)
                Debug.Log($"<color=cyan>[NetworkedStatsController] Invulnerability set to {invulnerable} for {duration}s</color>");
        }
        
        private void RemoveInvulnerabilityInternal()
        {
            if (IsServerInitialized)
            {
                _syncedInvulnerable.Value = false;
            }
        }
        #endregion
        
        #region DEATH_HANDLING
        // Caller frames for the death canary mark.
        private static string DeathCaller()
        {
            var frames = new System.Diagnostics.StackTrace(DEATH_CALLER_SKIPPED_FRAMES, false).GetFrames();
            if (frames == null) return "?";
            var names = new System.Collections.Generic.List<string>();
            foreach (var f in frames)
            {
                var m = f.GetMethod();
                if (m?.DeclaringType == null) continue;
                names.Add(m.DeclaringType.Name + "." + m.Name);
                if (names.Count == DEATH_CALLER_FRAMES) break;
            }
            return string.Join(" < ", names);
        }

        /// <summary>Server: reports a death once, with its killer.</summary>
        [Server]
        private void HandleServerSideDeath(NetworkConnection killer)
        {
            if (!IsServerInitialized) return;

            // Stop a pending singleplayer hazard countdown so it cannot report a second death.
            CancelPendingSingleplayerDeath();

            if (_serverDeathReported) return;
            _serverDeathReported = true;

            // Environment kills do not drain endurance: force 0 so clients see a dead car.
            _syncedEndurance.Value = 0f;

            // Killer: the connection's car, else whoever last hit or pushed this one.
            NetworkObject killerNob = (killer != null && killer.FirstObject != null)
                ? killer.FirstObject.GetComponent<NetworkObject>()
                : null;
            if (killerNob == null) killerNob = RecentAttacker();
            
            string killerName = killerNob != null ? killerNob.gameObject.name : "Unknown";
            MpCanary.Mark($"Death '{transform.root.name}' at {transform.position:F0} killer={killerName} via {DeathCaller()}");

            if (verboseLogging)
                Debug.Log($"<color=red>[NetworkedStatsController] {gameObject.name} killed by {killerName}</color>");
            
            OnPlayerDeathRpc(base.NetworkObject, killerNob);
            
            if (_cachedLevelControllerManager == null)
                _cachedLevelControllerManager = FindFirstObjectByType<NetworkedLevelControllerManager>();
            if (_cachedLevelControllerManager != null)
            {
                _cachedLevelControllerManager.ReportDeathOnServer(base.NetworkObject, killerNob);
            }
            else
            {
                Debug.LogWarning("[NetworkedStatsController] NetworkedLevelControllerManager not found! Cannot report death.");
            }
        }

        /// <summary>
        /// Server-authoritative environmental kill which bypasses the client-RPC damage cap.
        /// Used by dead walls, pits, and boss kill volumes.
        /// </summary>
        [Server]
        public void KillFromEnvironmentServer()
        {
            if (!IsServerInitialized) return;
            HandleServerSideDeath(null);
        }
        
        [ObserversRpc]
        private void OnPlayerDeathRpc(NetworkObject victim, NetworkObject killer)
        {
            // A client may have entered a local hazard before the report arrived: stop its countdown.
            CancelPendingSingleplayerDeath();

            string victimName = victim != null ? MatchNames.Of(victim.gameObject) : null;
            string killerName = killer != null ? MatchNames.Of(killer.gameObject) : null;
                        
            if (dieExplosionVfx != null)
            {
                var vfx = Instantiate(dieExplosionVfx, transform.position, Quaternion.identity);
                Destroy(vfx, DEATH_VFX_LIFETIME_SECONDS);
            }

            if (HUDManager.HasInstance)
            {
                HUDManager.Instance.UpdateUIElement(UIDynamicElementType.EVENT_TEXT, string.IsNullOrEmpty(killerName)
                    ? LocalizationManager.Localize("MP.Feed.Eliminated", victimName ?? LocalizationManager.Localize("MP.Feed.APlayer"))
                    : LocalizationManager.Localize("MP.Feed.Kill", killerName, victimName));
            }

            // The singleplayer path that greys the icon is detached in multiplayer.
            MarkCharacterIconDead(victim);

            if (IsOwner)
            {
                if (HUDManager.HasInstance)
                {
                    HUDManager.Instance.UpdateUIElement(UIDynamicElementType.EVENT_TEXT, LocalizationManager.Localize("MP.YouDied"));
                }

                DisableLocalInputsAndOffensiveActions();

                EnsureSpectatorInstance();
                NetworkedSpectatorController.Instance?.ActivateSpectator();
            }
            else
            {
                // Someone else died: if I'm currently spectating them, the spectator will cycle.
                NetworkedSpectatorController.Instance?.OnAnyCharacterDied(victim);
            }
        }

        /// <summary>Greys the victim's icon (icons reference the car: compare NetworkObject ids).</summary>
        private static void MarkCharacterIconDead(NetworkObject victim)
        {
            if (victim == null) return;

            var levelController = LevelController.ExistingInstance;
            if (levelController == null || levelController.characterIcons == null) return;

            int victimObjectId = victim.ObjectId;
            foreach (var icon in levelController.characterIcons)
            {
                if (icon == null || icon.Character == null) continue;
                var iconNob = icon.Character.GetComponentInParent<NetworkObject>(true);
                if (iconNob == null) iconNob = icon.Character.GetComponentInChildren<NetworkObject>(true);
                if (iconNob != null && iconNob.ObjectId == victimObjectId)
                {
                    icon.SetPlayerDeadIconIsActive(true);
                    break;
                }
            }
        }

        private void CancelPendingSingleplayerDeath()
        {
            if (_coreStats == null)
                return;

            var timerField = DeadTimerField;
            if (timerField?.GetValue(_coreStats) is ITimer deadTimer)
                deadTimer.StopTimer();

            timerField?.SetValue(_coreStats, null);
            CurrentKillerField?.SetValue(_coreStats, null);
        }

        private void DisableLocalInputsAndOffensiveActions()
        {
            // Walk up to the NetworkedPlayer root so we catch components on the shell + the car.
            var rootNob = GetComponentInParent<NetworkObject>(true);
            Transform root = rootNob != null ? rootNob.transform : transform;

            foreach (var pic in root.GetComponentsInChildren<PlayerInputController>(true))
                if (pic != null) pic.enabled = false;

            foreach (var pip in root.GetComponentsInChildren<PlayerInputProvider>(true))
                if (pip != null) pip.enabled = false;

            foreach (var shoot in root.GetComponentsInChildren<NetworkedShoot>(true))
                if (shoot != null) shoot.enabled = false;

            foreach (var ult in root.GetComponentsInChildren<NetworkedUniqueAbility>(true))
                if (ult != null) ult.enabled = false;

            // Also the singleplayer abilities and shoot handles.
            foreach (var ab in root.GetComponentsInChildren<BaseUniqueAbility>(true))
                if (ab != null) ab.enabled = false;
            foreach (var sh in root.GetComponentsInChildren<NormalShootHandle>(true))
                if (sh != null) sh.enabled = false;
            foreach (var ph in root.GetComponentsInChildren<PushShootHandle>(true))
                if (ph != null) ph.enabled = false;

            if (verboseLogging)
                Debug.Log($"[NetworkedStatsController] Disabled inputs + offensive actions on '{gameObject.name}' (dead).");
        }

        private static void EnsureSpectatorInstance()
        {
            if (NetworkedSpectatorController.Instance != null) return;
            var go = new GameObject("[NetworkedSpectatorController]");
            go.AddComponent<NetworkedSpectatorController>();
        }
        #endregion
        
        #region PUBLIC_API
        public float GetSyncedEndurance() => _syncedEndurance.Value;
        
        public float GetMaxEndurance() => _coreStats != null ? _coreStats.MaxEndurance : 0f;
        
        public bool IsInvulnerable() => _syncedInvulnerable.Value;
        
        public bool IsAlive() => _syncedEndurance.Value > 0f;
        
        /// <summary>Re-reads the endurance into the SyncVar after a direct server-side heal.</summary>
        public void ForceEnduranceSync()
        {
            if (!IsServerInitialized) return;
            _syncedEndurance.Value = _coreStats.Endurance;
        }
        #endregion
        
        #region DEBUG
        [ContextMenu("Debug - Take 50 Damage")]
        private void DebugTakeDamage()
        {
            if (IsServerInitialized)
            {
                DealDamageServerRpc(DEBUG_AMOUNT, null);
            }
        }
        
        [ContextMenu("Debug - Heal 50")]
        private void DebugHeal()
        {
            if (IsOwner)
            {
                HealServerRpc(DEBUG_AMOUNT);
            }
        }
        
        [ContextMenu("Debug - Toggle Invulnerability")]
        private void DebugToggleInvulnerability()
        {
            if (IsOwner)
            {
                SetInvulnerabilityServerRpc(!_syncedInvulnerable.Value);
            }
        }
        #endregion
    }
}
