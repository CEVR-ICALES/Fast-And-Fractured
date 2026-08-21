using FishNet.Object;
using FishNet.Connection;
using UnityEngine;
using UnityEngine.Events;
using FastAndFractured;
using Utilities;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Networked wrapper for unique abilities. Subscribes to the singleplayer
    /// BaseUniqueAbility events and broadcasts activation/deactivation to all clients.
    /// 
    /// The singleplayer ability runs normally on the owner for instant feedback.
    /// This wrapper ensures:
    /// 1. VFX/sound are played on ALL clients (not just owner)
    /// 2. Healing goes through NetworkedStatsController (server-authoritative)
    /// 3. Cooldown state is consistent
    /// 
    /// Spawned ability objects (Pepe's tomato, Carme's chicken, Maria's croquettes)
    /// remain local to the owner. Their damage goes through the local collision system
    /// which calls NetworkedStatsController.DealDamageServerRpc. For full network
    /// visibility of ability objects, create NetworkObject variants using the
    /// Networked Bullet Converter tool.
    /// </summary>
    public class NetworkedUniqueAbility : NetworkBehaviour
    {
        [Tooltip("Optional: Assign a networked variant of the ability prefab here. If null, it tries to use the one from AbilityData.")]
        [SerializeField] private GameObject _networkedAbilityPrefab;

        private BaseUniqueAbility _ability;
        private NetworkedStatsController _networkedStats;
        private StatsController _statsController;
        private bool _subscribedToEvents;
        private bool _isJosefinoAbility;
        private bool _abilityActiveOnServer;
        private bool _predictedObjectSpawnPermit;
        private bool _suppressLocalEventForwarding;

        private bool _isCarmeAbility;
        private bool _sceneTeardownPrepared;
        private bool _mariaDestroyScheduled;

        private void Awake()
        {
            _ability = GetComponentInChildren<BaseUniqueAbility>();
            _networkedStats = GetComponentInChildren<NetworkedStatsController>();
            _statsController = GetComponentInChildren<StatsController>();
            _isJosefinoAbility = _ability is JosefinoUniqueAbility;
            _isCarmeAbility = _ability is CarmeUniqueAbility;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // BaseUniqueAbility's UniqueUIPrefab is a singleplayer-only full-screen overlay. In a
            // multiplayer car it can recursively create player/UI singletons and lock the editor
            // before onAbilityActivated is reached (reproduced with Pepe). Multiplayer already
            // exposes the ultimate through PlayerHUD, so suppress that SP overlay on owners and
            // replicas. Particle/VFX and the actual ability implementation still run normally.
            if (_ability != null)
                SingleplayerReflectionCache.BaseUniqueAbility_IsAI?.SetValue(_ability, true);
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _sceneTeardownPrepared = false;
            _mariaDestroyScheduled = false;

            if (_ability == null)
            {
                Debug.LogWarning($"[NetworkedUniqueAbility] No BaseUniqueAbility found on {gameObject.name}. Disabling.");
                enabled = false;
                return;
            }

            SubscribeToEvents();
        }

        public override void OnStopNetwork()
        {
            // A lost connection can despawn the vehicle before HostDisconnectWatchdog gets its
            // client-stopped callback. Prepare here as well so Maria is quiesced while map
            // services (especially SoundManager) are still alive. The method is idempotent and
            // unsubscribes before invoking any singleplayer completion delegate, so it cannot
            // send through FishNet while the object is stopping.
            PrepareForSceneTeardown(destroyMariaComponent: false);
            base.OnStopNetwork();
        }

        /// <summary>
        /// Quiesces every networked unique ability before a multiplayer map is unloaded.
        /// Maria needs an explicit early destroy: her singleplayer OnDestroy accesses
        /// SoundManager.Instance, which would create an invalid auxiliary singleton if Unity
        /// happened to destroy SoundManager first during unordered scene teardown.
        /// </summary>
        public static void PrepareAllForSceneTeardown()
        {
            var wrappers = FindObjectsByType<NetworkedUniqueAbility>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var wrapper in wrappers)
            {
                if (wrapper != null)
                    wrapper.PrepareForSceneTeardown();
            }
        }

        private void PrepareForSceneTeardown(bool destroyMariaComponent = true)
        {
            if (_ability == null)
                _ability = GetComponentInChildren<BaseUniqueAbility>(true);
            if (!(_ability is MariaAntoniaUniqueAbility mariaAbility))
            {
                if (!_sceneTeardownPrepared)
                {
                    _sceneTeardownPrepared = true;
                    UnsubscribeFromEvents();
                }
                return;
            }

            if (!_sceneTeardownPrepared)
            {
                _sceneTeardownPrepared = true;

                // No ability-end callback may try to forward an RPC once teardown has begun.
                UnsubscribeFromEvents();

                StopTimer(SingleplayerReflectionCache.BaseUniqueAbility_CooldownTimer, mariaAbility,
                    invokeDecreaseCompletion: false);
                StopTimer(SingleplayerReflectionCache.MariaAntoniaUniqueAbility_DurationTimer, mariaAbility,
                    invokeDecreaseCompletion: true);

                _abilityActiveOnServer = false;
                SetAbilityActiveStateOnServer(false);
                SingleplayerReflectionCache.BaseUniqueAbility_IsOnCooldown?.SetValue(mariaAbility, false);
            }

            // OnStopNetwork may represent a pooled despawn. Quiesce there, but let FishNet own
            // the component lifetime. The explicit scene-transition path escalates the same
            // idempotent preparation and schedules the early destroy below.
            if (!destroyMariaComponent || _mariaDestroyScheduled) return;
            _mariaDestroyScheduled = true;

            // Destroy is intentionally deferred to the end of this frame. All MainMenu entry
            // points wait one frame through MultiplayerSceneTeardown, so Maria.OnDestroy runs
            // while the map's real SoundManager is still alive.
            mariaAbility.enabled = false;
            Destroy(mariaAbility);
            _ability = null;
        }

        private static void StopTimer(System.Reflection.FieldInfo timerField, object owner,
            bool invokeDecreaseCompletion)
        {
            if (timerField == null || owner == null) return;

            var timer = timerField.GetValue(owner) as ITimer;
            if (timer == null) return;

            var data = timer.GetData();
            bool wasRunning = data != null && data.IsRunning;
            var completion = invokeDecreaseCompletion && wasRunning
                ? data.OnTimerDecreaseComplete
                : null;
            timer.StopTimer();

            if (data != null)
            {
                data.OnTimerDecreaseComplete = null;
                data.OnTimerIncreaseComplete = null;
                data.OnTimerDecreaseUpdate = null;
                data.OnTimerIncreaseUpdate = null;
            }

            // Maria's duration completion is her canonical cleanup path: it stops VFX/audio,
            // restores the shared hair material and clears spawned croquettes. Invoke it once
            // while all scene services still exist, after unsubscribing the network wrapper.
            if (completion != null)
            {
                try
                {
                    completion.Invoke();
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, owner as UnityEngine.Object);
                }
            }
            timerField.SetValue(owner, null);
        }

        private void SubscribeToEvents()
        {
            if (_subscribedToEvents || _ability == null) return;
            _subscribedToEvents = true;

            _ability.onAbilityActivated.AddListener(OnLocalAbilityActivated);
            _ability.onAbilityEnded.AddListener(OnLocalAbilityEnded);
        }

        private void UnsubscribeFromEvents()
        {
            if (!_subscribedToEvents || _ability == null) return;
            _subscribedToEvents = false;

            _ability.onAbilityActivated.RemoveListener(OnLocalAbilityActivated);
            _ability.onAbilityEnded.RemoveListener(OnLocalAbilityEnded);
        }

        #region LOCAL_EVENT_HANDLERS

        /// <summary>
        /// Called when the singleplayer ability activates on the owner.
        /// Sends activation to the server for validation and broadcast.
        /// </summary>
        private void OnLocalAbilityActivated()
        {
            if (_suppressLocalEventForwarding) return;
            if (!base.IsOwner) return;

            // Capture state for server
            float currentEndurance = _statsController != null ? _statsController.Endurance : 0f;

            // HOST: calling a [ServerRpc] from a server context silently never delivers its
            // logic (live-debugged 2026-07-11 on Pepe's tomato: RpcWriter ran, RpcLogic never
            // executed). The ability-END path below already used this direct-call pattern —
            // the ACTIVATE path lacked it, which killed the host's entire ultimate pipeline
            // (heal, invulnerability, networked spawn, replica broadcast). Same fix as
            // StartGame / HandleServerSideDeath.
            if (base.IsServerInitialized)
                HandleAbilityActivatedOnServer(currentEndurance);
            else
                ActivateAbilityServerRpc(currentEndurance);
        }

        private void OnLocalAbilityEnded()
        {
            if (_suppressLocalEventForwarding) return;
            if (base.IsServerInitialized)
            {
                HandleAbilityEndedOnServer();
                return;
            }

            if (!base.IsOwner) return;
            EndAbilityServerRpc();
        }

        #endregion

        #region SERVER_RPCS

        [ServerRpc]
        private void ActivateAbilityServerRpc(float ownerEnduranceAfterAbility)
        {
            HandleAbilityActivatedOnServer(ownerEnduranceAfterAbility);
        }

        [Server]
        private void HandleAbilityActivatedOnServer(float ownerEnduranceAfterAbility)
        {
            if (!base.IsServerInitialized) return;
            MpCanary.Mark($"HandleAbilityActivatedOnServer ENTER ({gameObject.name})");
            _abilityActiveOnServer = true;
            _predictedObjectSpawnPermit = true;
            SetAbilityActiveStateOnServer(true);

            // If ability heals (Josefino), sync the heal through NetworkedStatsController
            if (_networkedStats != null && _statsController != null)
            {
                float serverEndurance = _statsController.Endurance;
                float healAmount = ownerEnduranceAfterAbility - serverEndurance;

                if (healAmount > 0f)
                {
                    // Suppress _isPlayer for non-owner so HUD updates from singleplayer code
                    // don't bleed into the local player's HUD.
                    if (!base.IsOwner)
                    {
                        SingleplayerReflectionCache.RunWithoutPlayerFlag(_statsController,
                            () => _statsController.RecoverEndurance(healAmount, false));
                    }
                    else
                    {
                        _statsController.RecoverEndurance(healAmount, false);
                    }

                    _networkedStats.ForceEnduranceSync();
                }

                if (_isJosefinoAbility)
                {
                    // Josefino's ultimate grants temporary invulnerability. Sync it server-authoritatively.
                    var josefinoAbilityData = GetAbilityData();
                    float duration = josefinoAbilityData != null ? josefinoAbilityData.Duration : 0f;
                    _networkedStats.SetInvulnerabilityServer(true, duration);
                }

                // Host path: heal may already be applied on server, resulting in healAmount == 0.
                // Force sync to propagate current endurance to remote observers in all cases.
                _networkedStats.ForceEnduranceSync();
            }

            // Check if the ability effect is a NetworkObject (needs server spawn)
            var abilityData = GetAbilityData();
            GameObject prefabToSpawn = _networkedAbilityPrefab;
            
            // If no override, try the one from AbilityData
            if (prefabToSpawn == null && abilityData != null)
            {
                prefabToSpawn = abilityData.ParticleEffectPrefab;
            }

            if (prefabToSpawn != null)
            {
                if (prefabToSpawn.GetComponent<NetworkObject>() != null)
                {
                    // Spawn networked ability object
                    var go = Instantiate(prefabToSpawn, 
                        _ability.transform.position, _ability.transform.rotation);
                    
                    base.ServerManager.Spawn(go, base.Owner);
                    return; // Skip RPC, FishNet handles the spawn
                }
            }

            // Broadcast VFX activation to all clients (for non-networked visual-only effects)
            OnAbilityActivatedRpc();
        }

        internal bool TryConsumePredictedObjectSpawnPermit(NetworkConnection spawner)
        {
            // One-shot abilities may raise their end event in the same frame as activation.
            // The permit itself is the server-issued, single-use proof of a valid activation;
            // tying consumption to the transient active flag races the reliable spawn packet.
            if (!base.IsServerInitialized || spawner == null || base.Owner != spawner ||
                !_predictedObjectSpawnPermit)
                return false;
            _predictedObjectSpawnPermit = false;
            return true;
        }

        [ServerRpc]
        private void EndAbilityServerRpc()
        {
            HandleAbilityEndedOnServer();
        }

        #endregion

        #region OBSERVERS_RPCS

        /// <summary>
        /// Broadcasts ability activation VFX to all clients.
        /// Owner already played effects locally; this handles non-owner clients.
        /// </summary>
        [ObserversRpc(ExcludeOwner = true)]
        private void OnAbilityActivatedRpc()
        {
            if (_ability == null) return;

            // Run the FULL singleplayer ability on this replica instead of only replaying the
            // configured UnityEvents. Per-character effects live inside ActivateAbility overrides
            // that the events don't cover: MariaAntonia's temporal stat buffs (which the host's
            // authoritative StatsController must also carry or her damage/speed desyncs) and her
            // orbiting croquettes, sounds, hair emission, ParticleEffectPrefab, etc.
            //
            // Exceptions that keep the legacy events+VFX-only path:
            // - Carme: her override Instantiates the local McChicken with an aim direction only
            //   the owner knows — running it on replicas would spawn junk chickens.
            // - Josefino: his heal is already applied server-side by ActivateAbilityServerRpc
            //   (endurance delta + ForceEnduranceSync); a full replica activation on the host
            //   would heal him twice. His shield visuals are covered by the UnityEvents.
            if (_isCarmeAbility || _isJosefinoAbility)
            {
                SetAbilityActiveStateOnServer(true);
                InvokeAbilityEventSafely(_ability.onAbilityActivated);

                var carmeData = GetAbilityData();
                if (carmeData != null && carmeData.ParticleEffectPrefab != null)
                {
                    var vfx = Instantiate(carmeData.ParticleEffectPrefab,
                        _ability.transform.position, _ability.transform.rotation, _ability.transform);
                    if (carmeData.Duration > 0f)
                        Destroy(vfx, carmeData.Duration + 1f);
                }
                return;
            }

            // Clear replica-local guards first: the replica keeps its own cooldown timer, and the
            // owner's cooldown can run faster (COOLDOWN_SPEED buffs), so the replica could still
            // be "on cooldown" and silently reject an activation the server already validated.
            SingleplayerReflectionCache.BaseUniqueAbility_IsAbilityActivePrivate?.SetValue(_ability, false);
            SingleplayerReflectionCache.BaseUniqueAbility_IsAbilityActivePublic?.SetValue(_ability, false);
            SingleplayerReflectionCache.BaseUniqueAbility_IsOnCooldown?.SetValue(_ability, false);

            _suppressLocalEventForwarding = true;
            try
            {
                _ability.ActivateAbility();
            }
            finally
            {
                _suppressLocalEventForwarding = false;
            }
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void OnAbilityEndedRpc()
        {
            // Mirror inactive state and replay configured end events (eg. disable Josefino shield object).
            SetAbilityActiveStateOnServer(false);
            InvokeAbilityEventSafely(_ability.onAbilityEnded);

            // Non-owner cleanup of spawned particle VFX is handled by the auto-destroy timer.
        }

        #endregion

        #region HELPERS

        private void HandleAbilityEndedOnServer()
        {
            if (!base.IsServerInitialized) return;
            if (!_abilityActiveOnServer) return;

            _abilityActiveOnServer = false;
            SetAbilityActiveStateOnServer(false);

            if (_isJosefinoAbility && _networkedStats != null)
            {
                // Ensure remote clients lose invulnerability if the ability ends early (eg. first hit consumed shield).
                _networkedStats.SetInvulnerabilityServer(false, 0f);
            }

            OnAbilityEndedRpc();
        }

        /// <summary>
        /// Gets the AbilityData ScriptableObject via reflection (field is protected in BaseUniqueAbility).
        /// </summary>
        private AbilityData GetAbilityData()
        {
            if (_ability == null) return null;
            return SingleplayerReflectionCache.BaseUniqueAbility_AbilityData?.GetValue(_ability) as AbilityData;
        }

        /// <summary>
        /// Invokes an ability UnityEvent on remote replicas without re-entering local->server forwarding paths.
        /// </summary>
        private void InvokeAbilityEventSafely(UnityEvent abilityEvent)
        {
            if (abilityEvent == null) return;

            _suppressLocalEventForwarding = true;
            try
            {
                abilityEvent.Invoke();
            }
            finally
            {
                _suppressLocalEventForwarding = false;
            }
        }

        /// <summary>
        /// Mirrors ability active state on server without re-running the full ability activation flow.
        /// Needed so JosefinoCarImpactHandler server-side checks remain consistent for remote owners.
        /// </summary>
        private void SetAbilityActiveStateOnServer(bool isActive)
        {
            if (_ability == null) return;
            SingleplayerReflectionCache.BaseUniqueAbility_IsAbilityActivePrivate?.SetValue(_ability, isActive);
            SingleplayerReflectionCache.BaseUniqueAbility_IsAbilityActivePublic?.SetValue(_ability, isActive);
        }

        #endregion
    }
}
