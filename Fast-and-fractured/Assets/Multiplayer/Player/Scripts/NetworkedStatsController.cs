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
    /// <summary>
    /// Wrapper para StatsController que sincroniza vida, daño y muerte por red.
    /// NO modifica StatsController original - Solo envuelve funcionalidad crítica.
    /// </summary>
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
        /// <summary>
        /// SyncVar para vida actual (FishNet 4.x syntax).
        /// Se sincroniza automáticamente a todos los clientes.
        /// </summary>
        private readonly SyncVar<float> _syncedEndurance = new();
        
        /// <summary>
        /// SyncVar para invulnerabilidad (FishNet 4.x syntax).
        /// Usado para power-ups temporales.
        /// </summary>
        private readonly SyncVar<bool> _syncedInvulnerable = new();

        /// <summary>
        /// The character name code (e.g. "Pepe_2") this car was spawned with. The visible skin is a
        /// runtime material swap (CharacterDataProvider.SetCharacterSkin) that the singleplayer
        /// spawner only ever applies to the SERVER's instance — FishNet re-instantiates the prefab
        /// on remote clients with its DEFAULT materials and nothing re-applies the skin there. That
        /// is why the host and a remote client saw the same AI with different skins. Replicating the
        /// name code lets every peer apply the same skin locally.
        /// </summary>
        private readonly SyncVar<string> _skinNameCode = new();
        
        /// <summary>
        /// Flag para prevenir loops infinitos: Server cambia SyncVar -> OnChange -> No volver a cambiar SyncVar
        /// </summary>
        private bool _isUpdatingFromNetwork = false;

        /// <summary>
        /// Guard: only fire HandleServerSideDeath once per character per match. Damage-based
        /// (DealDamageServerRpc) and environment-based (StatsController.onDead from DeadWall,
        /// sandstorm, etc.) paths both flow through HandleServerSideDeath; without this they
        /// could double-count or double-fire OnPlayerDeathRpc.
        /// </summary>
        private bool _serverDeathReported = false;
        
        // Reflection accessors for private singleplayer fields. Sourced from
        // SingleplayerReflectionCache so any rename on the singleplayer side fails here loudly
        // (and centralized) instead of silently across the codebase.
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

            // Setup SyncVar callbacks (FishNet 4.x)
            _syncedEndurance.OnChange += OnEnduranceChanged;
            _syncedInvulnerable.OnChange += OnInvulnerabilityChanged;
            _skinNameCode.OnChange += OnSkinNameCodeChanged;
        }

        /// <summary>
        /// Server: records the character name code this car uses so every client can reproduce its
        /// skin locally. Must be called AFTER the object is network-spawned (SyncVars only replicate
        /// once the NetworkObject is initialized on the server).
        /// </summary>
        [Server]
        public void SetSkinNameCode(string nameCode)
        {
            if (string.IsNullOrEmpty(nameCode)) return;
            _skinNameCode.Value = nameCode;
        }

        private void OnSkinNameCodeChanged(string prev, string next, bool asServer)
        {
            // The host already carries the singleplayer-applied skin on its own instance, but
            // re-applying the same code is idempotent, so we do not special-case it. Applying on
            // every peer (including late joiners, via the buffered SyncVar) guarantees consistency.
            ApplySkinFromNameCode(next);
        }

        /// <summary>
        /// Re-runs the singleplayer material-swap skin logic locally for this car's name code.
        /// CharacterDataProvider.SetCharacterSkin only reads the name code and this GameObject's
        /// Visuals hierarchy (never the provider's data list), so a throwaway provider is fine.
        /// </summary>
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

            // Inicializar SyncVars con valores del StatsController
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

        /// <summary>
        /// Mirror of the server-side strip on the client. Without this, when a local client's car
        /// dies via local DeadWall trigger, the singleplayer LC flow runs locally and calls
        /// LoadScene(MAIN_MENU), yanking the client out of the map scene before the server's
        /// authoritative OnPlayerDeathRpc can show the spectator UI.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();

            // Cliente sincroniza estado inicial
            if (!IsServerInitialized)
            {
                OnEnduranceChanged(0f, _syncedEndurance.Value, false);
                OnInvulnerabilityChanged(false, _syncedInvulnerable.Value, false);

                // Apply the replicated skin. If the car spawned before the name code was set,
                // OnSkinNameCodeChanged handles it when it arrives; if it was already buffered,
                // apply it now (OnChange does not fire for the initial buffered value here).
                ApplySkinFromNameCode(_skinNameCode.Value);

                InstallNetworkDeathListener();
            }

            if (verboseLogging)
                Debug.Log($"<color=cyan>[NetworkedStatsController] Client initialized. Endurance: {_syncedEndurance.Value}</color>");
        }

        /// <summary>
        /// Installs the multiplayer death listener without disturbing unrelated listeners.
        /// NetworkedLevelControllerWrapper removes LevelController.ReportCharacterDeath one frame
        /// after the single-player startup event adds it.
        /// </summary>
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

        /// <summary>
        /// Server-side listener on StatsController.onDead. Forwards environment / local-source
        /// deaths into the same flow as bullet-damage deaths so the match tracking and clients
        /// learn about them. Idempotent via _serverDeathReported.
        /// </summary>
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
                // Filter out small movements/jitter
                if (dist > 0.01f)
                {
                    TotalDistanceTraveled.Value += dist;
                    _lastPosition = transform.position;
                }
            }
        }

        #endregion
        
        #region SYNCVAR_CALLBACKS
        /// <summary>
        /// Callback cuando la vida cambia en el servidor. Actualiza el StatsController local.
        /// </summary>
        /// <param name="prev">Valor anterior</param>
        /// <param name="next">Nuevo valor</param>
        /// <param name="asServer">True si se ejecuta en el servidor</param>
        private void OnEnduranceChanged(float prev, float next, bool asServer)
        {
            if (_coreStats == null) return;

            // On server (including host-as-server), DealDamageServerRpc already applied damage
            // to StatsController and updated the SyncVar. Skip the endurance field write to
            // avoid double-processing, but still update VFX on non-owner vehicles so that
            // host sees damage visuals on remote cars.
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
                    // Host's own car: bullet damage flows through SP TakeEndurance which updates
                    // the HUD itself, but server-side writes that set the SyncVar directly —
                    // HandleServerSideDeath forcing 0 on environment kills (map holes, dead walls)
                    // — bypass it, leaving the host's health bar full while he's dead. Mirror the
                    // synced value to the HUD (idempotent when TakeEndurance already did it).
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
                // Owner (client-only, NOT host): set endurance via reflection and update HUD directly.
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
                // Non-owner: set currentEndurance directly via reflection.
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
        /// <summary>
        /// ServerRpc para aplicar daño. Cualquier cliente puede llamarlo (RequireOwnership = false).
        /// El servidor valida y aplica el daño, luego actualiza el SyncVar.
        /// </summary>
        /// <param name="damage">Cantidad de daño</param>
        /// <param name="attacker">NetworkConnection del atacante (para stats y kill tracking)</param>
        // Sanity cap on damage per RPC call. Anti-cheat: a malicious client could call
        // DealDamageServerRpc directly with absurd values since RequireOwnership=false.
        // Real bullets do at most a few hundred damage, so 500 is a generous upper bound.
        private const float MAX_DAMAGE_PER_CALL = 500f;
        // Same idea for healing.
        private const float MAX_HEAL_PER_CALL = 500f;

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
            
            // Obtener GameObject del atacante para el sistema de stats
            GameObject attackerObject = attacker != null && attacker.FirstObject != null 
                ? attacker.FirstObject.gameObject 
                : null;
            
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

            // Apply damage. StatsController.TakeEndurance() calls HUDManager.Instance.UpdateUIElement()
            // when _isPlayer is true. Since HUDManager.Instance is a singleton, non-owner vehicles
            // would incorrectly update the local player's HUD. Temporarily suppress _isPlayer for non-owner.
            ApplyEnduranceDamage(damage, attackerObject ?? gameObject);
            
            // Actualizar SyncVar (esto propagará a todos los clientes via OnEnduranceChanged)
            _syncedEndurance.Value = _coreStats.Endurance;
            
            // Track stats
            TotalDamageTaken.Value += damage;
            
            // Track attacker stats
            if (attacker != null)
            {
                foreach (var nob in attacker.Objects)
                {
                    var attackerStats = nob.GetComponentInChildren<NetworkedStatsController>();
                    if (attackerStats != null)
                    {
                        attackerStats.TotalDamageDealt.Value += damage;
                        break;
                    }
                }
            }

            // Verificar muerte
            if (_coreStats.Endurance <= 0f && IsServerInitialized)
            {
                HandleServerSideDeath(attacker);
            }
        }
        
        /// <summary>
        /// Apply endurance damage to StatsController, suppressing HUD updates for non-owner vehicles.
        /// StatsController.TakeEndurance() calls HUDManager.Instance.UpdateUIElement() when _isPlayer=true,
        /// but HUDManager is a singleton - all vehicles would update the SAME (local player's) HUD.
        /// For non-owner vehicles, temporarily set _isPlayer=false to skip HUD update, then restore.
        /// </summary>
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
        
        /// <summary>
        /// ServerRpc para curación. Solo el owner o server pueden curarse.
        /// </summary>
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
        
        /// <summary>
        /// ServerRpc para activar invulnerabilidad temporal (power-ups, abilities).
        /// </summary>
        [ServerRpc]
        public void SetInvulnerabilityServerRpc(bool invulnerable, float duration = 0f)
        {
            SetInvulnerabilityServer(invulnerable, duration);
        }

        /// <summary>
        /// Server-only helper for invulnerability sync. Use this when already executing on server.
        /// </summary>
        public void SetInvulnerabilityServer(bool invulnerable, float duration = 0f)
        {
            if (!IsServerInitialized) return;

            CancelInvoke(nameof(RemoveInvulnerabilityInternal));
            _syncedInvulnerable.Value = invulnerable;

            if (invulnerable && duration > 0f)
            {
                // Programar desactivación automática
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
        /// <summary>
        /// Server-side death handler. Called from DealDamageServerRpc when endurance reaches 0.
        /// Was previously a [ServerRpc] but it's invoked from code already running on the server,
        /// so the RPC roundtrip was redundant and added a frame of latency.
        /// </summary>
        /// <param name="killer">The NetworkConnection that dealt the final blow.</param>
        [Server]
        private void HandleServerSideDeath(NetworkConnection killer)
        {
            if (!IsServerInitialized) return;

            // A map hazard may still own StatsController's singleplayer countdown when another
            // authoritative source reports the death. Stop that countdown before notifying peers
            // so its completion callback cannot emit a second local death later.
            CancelPendingSingleplayerDeath();

            // Guard: a single victim may be reported via both damage-overflow AND the
            // StatsController.onDead listener. Only count the first one.
            if (_serverDeathReported) return;
            _serverDeathReported = true;

            // Force endurance to 0 in the synced state. Bullet damage already does this via
            // DealDamageServerRpc, but environment kills (DeadWall / hole) don't drain endurance
            // — the corpse would otherwise still be `Endurance > 0` on clients and our spectator
            // would let you cycle to a "dead" target.
            _syncedEndurance.Value = 0f;

            // Find killer's NetworkObject for more robust identification (works for Players and AI)
            NetworkObject killerNob = (killer != null && killer.FirstObject != null) 
                ? killer.FirstObject.GetComponent<NetworkObject>() 
                : null;
            
            string killerName = killerNob != null ? killerNob.gameObject.name : "Unknown";
            
            if (verboseLogging)
                Debug.Log($"<color=red>[NetworkedStatsController] {gameObject.name} killed by {killerName}</color>");
            
            // Notify all clients about the death
            OnPlayerDeathRpc(base.NetworkObject, killerNob);
            
            // Notify the manager for game loop tracking
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
        
        /// <summary>
        /// ObserversRpc notifying all clients about a death.
        /// </summary>
        [ObserversRpc]
        private void OnPlayerDeathRpc(NetworkObject victim, NetworkObject killer)
        {
            // Pure clients can also have entered a local hazard before the authoritative report
            // arrived. Quiesce their singleplayer countdown as soon as the death is replicated.
            CancelPendingSingleplayerDeath();

            string victimName = victim != null ? victim.gameObject.name : "Unknown";
            string killerName = killer != null ? killer.gameObject.name : "Unknown";
            
            // Instantiate death VFX with auto-destroy to prevent memory leak
            if (dieExplosionVfx != null)
            {
                var vfx = Instantiate(dieExplosionVfx, transform.position, Quaternion.identity);
                Destroy(vfx, 5f);
            }

            // Show kill feed message
            if (HUDManager.HasInstance)
            {
                HUDManager.Instance.UpdateUIElement(UIDynamicElementType.EVENT_TEXT, $"{killerName} eliminated {victimName}");
            }

            // Gray out the victim's icon in the top HUD strip. The singleplayer path that does this
            // (GameLoopManager.ProcessCharacterDeathAfterDelay) is detached in multiplayer — the
            // wrapper removes LevelController's death listener because the authoritative death flow
            // lives here — so this RPC is the only client-side place that knows a character died.
            MarkCharacterIconDead(victim);

            // If I am the victim, show death feedback and activate spectator mode
            if (IsOwner)
            {
                if (HUDManager.HasInstance)
                {
                    HUDManager.Instance.UpdateUIElement(UIDynamicElementType.EVENT_TEXT, "YOU DIED");
                }

                // Cut off input on the dead car: PlayerInputController, NetworkedShoot,
                // NetworkedUniqueAbility, and the local PlayerInputProvider. Without this the
                // ghost car keeps responding to mouse clicks and Q/E ability presses, shooting
                // bullets and triggering abilities while the player is supposed to be spectating.
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

        /// <summary>
        /// Activates the "dead" overlay on the victim's CharacterIcon in the top HUD strip.
        /// Mirrors the icon-matching logic of NetworkedLevelControllerManager.DisconnectDeathRpc:
        /// icons reference the car GameObject, so resolve each icon's NetworkObject and compare ids.
        /// </summary>
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

        /// <summary>
        /// Owner-side: silence everything that could let a "dead" car still attack or accept
        /// movement input. Components are disabled (not destroyed) — defensive in case the
        /// match has any continuation path that re-spawns the player.
        /// </summary>
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

            // Belt-and-braces: also disable the singleplayer abilities (BaseUniqueAbility) and
            // shoot handles so any local-only firing path is also blocked.
            foreach (var ab in root.GetComponentsInChildren<BaseUniqueAbility>(true))
                if (ab != null) ab.enabled = false;
            foreach (var sh in root.GetComponentsInChildren<NormalShootHandle>(true))
                if (sh != null) sh.enabled = false;
            foreach (var ph in root.GetComponentsInChildren<PushShootHandle>(true))
                if (ph != null) ph.enabled = false;

            if (verboseLogging)
                Debug.Log($"[NetworkedStatsController] Disabled inputs + offensive actions on '{gameObject.name}' (dead).");
        }

        /// <summary>
        /// Lazily creates the local spectator controller singleton on first death event.
        /// Lives on its own GameObject so it survives between scene reloads if needed.
        /// </summary>
        private static void EnsureSpectatorInstance()
        {
            if (NetworkedSpectatorController.Instance != null) return;
            var go = new GameObject("[NetworkedSpectatorController]");
            go.AddComponent<NetworkedSpectatorController>();
        }
        #endregion
        
        #region PUBLIC_API
        /// <summary>
        /// Obtiene la vida actual sincronizada. Seguro de usar desde cualquier cliente.
        /// </summary>
        public float GetSyncedEndurance() => _syncedEndurance.Value;
        
        /// <summary>
        /// Obtiene la vida máxima del StatsController base.
        /// </summary>
        public float GetMaxEndurance() => _coreStats != null ? _coreStats.MaxEndurance : 0f;
        
        /// <summary>
        /// Verifica si el jugador está invulnerable.
        /// </summary>
        public bool IsInvulnerable() => _syncedInvulnerable.Value;
        
        /// <summary>
        /// Verifica si el jugador está vivo.
        /// </summary>
        public bool IsAlive() => _syncedEndurance.Value > 0f;
        
        /// <summary>
        /// Forces the SyncVar endurance to re-read from StatsController. Called from 
        /// NetworkedUniqueAbility after server-side heal is applied directly to StatsController.
        /// </summary>
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
                DealDamageServerRpc(50f, null);
            }
        }
        
        [ContextMenu("Debug - Heal 50")]
        private void DebugHeal()
        {
            if (IsOwner)
            {
                HealServerRpc(50f);
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
