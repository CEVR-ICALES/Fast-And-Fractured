using FishNet.Object;
using FishNet.Connection;
using UnityEngine;
using UnityEngine.Events;
using FastAndFractured;
using Utilities;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Networks a unique ability: the owner runs the singleplayer ability, the server validates it (heal,
    /// invulnerability, networked spawns) and every replica plays it.
    /// </summary>
    public class NetworkedUniqueAbility : NetworkBehaviour
    {
        private const float VFX_EXTRA_LIFETIME_SECONDS = 1f;

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
        private bool _isPepeAbility;
        private bool _sceneTeardownPrepared;
        private bool _mariaDestroyScheduled;

        private void Awake()
        {
            _ability = GetComponentInChildren<BaseUniqueAbility>();
            _networkedStats = GetComponentInChildren<NetworkedStatsController>();
            _statsController = GetComponentInChildren<StatsController>();
            _isJosefinoAbility = _ability is JosefinoUniqueAbility;
            _isCarmeAbility = _ability is CarmeUniqueAbility;
            _isPepeAbility = _ability is PepeUniqueAbility;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // The singleplayer UniqueUIPrefab overlay can recreate player/UI singletons and hang the editor; PlayerHUD shows the ultimate.
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
            // A lost connection can despawn the car before HostDisconnectWatchdog runs: quiesce while SoundManager is alive.
            PrepareForSceneTeardown(destroyMariaComponent: false);
            base.OnStopNetwork();
        }

        /// <summary>Quiesces every networked ability before a map unloads; Maria's OnDestroy needs SoundManager alive.</summary>
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

            // OnStopNetwork may be a pooled despawn: quiesce, but leave the component's lifetime to FishNet.
            if (!destroyMariaComponent || _mariaDestroyScheduled) return;
            _mariaDestroyScheduled = true;

            // End of frame: MultiplayerSceneTeardown waits a frame, so Maria.OnDestroy still finds the map's SoundManager.
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

            // Maria's duration completion is her cleanup path (VFX, audio, hair material, croquettes).
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

        private void OnLocalAbilityActivated()
        {
            if (_suppressLocalEventForwarding) return;
            if (!base.IsOwner) return;

            float currentEndurance = _statsController != null ? _statsController.Endurance : 0f;

            // A [ServerRpc] called from the host never runs its logic.
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

        private static readonly System.Reflection.FieldInfo JosefinoRecoveryAmount =
            typeof(JosefinoUniqueAbility).GetField("enduranceRecoveryAmount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        private System.Collections.IEnumerator SyncEnduranceAfterAbilityEffect()
        {
            yield return null;
            if (_networkedStats != null) _networkedStats.ForceEnduranceSync();
        }

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

            // onAbilityActivated fires before the effect, so the owner reports pre-heal endurance: the server applies the
            // recovery itself for a remote owner; on the host the singleplayer heal lands a moment later, then syncs.
            if (_networkedStats != null && _statsController != null)
            {
                if (_isJosefinoAbility && JosefinoRecoveryAmount != null)
                {
                    if (base.IsOwner)
                    {
                        StartCoroutine(SyncEnduranceAfterAbilityEffect());
                    }
                    else
                    {
                        float healAmount = (float)JosefinoRecoveryAmount.GetValue(_ability);
                        if (healAmount > 0f)
                            SingleplayerReflectionCache.RunWithoutPlayerFlag(_statsController,
                                () => _statsController.RecoverEndurance(healAmount, false));
                    }
                }

                if (_isJosefinoAbility)
                {
                    var josefinoAbilityData = GetAbilityData();
                    float duration = josefinoAbilityData != null ? josefinoAbilityData.Duration : 0f;
                    _networkedStats.SetInvulnerabilityServer(true, duration);
                }

                // On the host the heal may already be applied (healAmount 0): sync anyway.
                _networkedStats.ForceEnduranceSync();
            }

            var abilityData = GetAbilityData();
            GameObject prefabToSpawn = _networkedAbilityPrefab;
            
            if (prefabToSpawn == null && abilityData != null)
            {
                prefabToSpawn = abilityData.ParticleEffectPrefab;
            }

            if (prefabToSpawn != null)
            {
                if (prefabToSpawn.GetComponent<NetworkObject>() != null)
                {
                    var go = Instantiate(prefabToSpawn, 
                        _ability.transform.position, _ability.transform.rotation);
                    
                    base.ServerManager.Spawn(go, base.Owner);
                    return; // Skip RPC, FishNet handles the spawn
                }
            }

            OnAbilityActivatedRpc();
        }

        internal bool TryConsumePredictedObjectSpawnPermit(NetworkConnection spawner)
        {
            // One-shot abilities can end in the activation frame: the single-use permit, not the active flag, proves activation.
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

        [ObserversRpc(ExcludeOwner = true)]
        private void OnAbilityActivatedRpc()
        {
            if (_ability == null) return;

            // Replicas run the full singleplayer ability (Maria's stat buffs and croquettes live in ActivateAbility), except:
            // - Carme: only the owner knows her chicken's aim.
            // - Josefino: the server already healed him; a replica activation on the host would heal twice.
            // - Pepe: his singleplayer tomato would drop a second, local set of tomatoes.
            if (_isCarmeAbility || _isJosefinoAbility || _isPepeAbility)
            {
                SetAbilityActiveStateOnServer(true);
                InvokeAbilityEventSafely(_ability.onAbilityActivated);

                var carmeData = GetAbilityData();
                if (carmeData != null && carmeData.ParticleEffectPrefab != null)
                {
                    var vfx = Instantiate(carmeData.ParticleEffectPrefab,
                        _ability.transform.position, _ability.transform.rotation, _ability.transform);
                    if (carmeData.Duration > 0f)
                        Destroy(vfx, carmeData.Duration + VFX_EXTRA_LIFETIME_SECONDS);
                }
                return;
            }

            // The owner's cooldown can run faster, so the replica would reject an activation the server validated.
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
                _networkedStats.SetInvulnerabilityServer(false, 0f);
            }

            OnAbilityEndedRpc();

            // Josefino's shield ends on a hit, resolved here; OnAbilityEndedRpc skips the owner, so the owner is told too.
            if (_isJosefinoAbility && base.Owner != null && base.Owner.IsValid && !base.Owner.IsLocalClient)
                TargetEndJosefinoShield(base.Owner);
        }

        [TargetRpc]
        private void TargetEndJosefinoShield(FishNet.Connection.NetworkConnection owner)
        {
            if (!(_ability is JosefinoUniqueAbility josefino) || !josefino.IsAbilityActive) return;
            _suppressLocalEventForwarding = true;
            try { josefino.OnInvulnerabilityLost(); }
            finally { _suppressLocalEventForwarding = false; }
        }

        /// <summary>Server: a hit broke Josefino's shield, as a push does in singleplayer.</summary>
        [Server]
        internal bool ServerConsumeJosefinoShield()
        {
            if (!_isJosefinoAbility || !_abilityActiveOnServer) return false;
            if (_ability is JosefinoUniqueAbility josefino && josefino.IsAbilityActive)
                josefino.OnInvulnerabilityLost();   // ends the ability -> OnLocalAbilityEnded -> HandleAbilityEndedOnServer
            HandleAbilityEndedOnServer();
            MpCanary.Mark($"Josefino shield broken by a hit ({gameObject.name})");
            return true;
        }

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

        /// <summary>Server: mirrors the active state so JosefinoCarImpactHandler checks hold for remote owners.</summary>
        private void SetAbilityActiveStateOnServer(bool isActive)
        {
            if (_ability == null) return;
            SingleplayerReflectionCache.BaseUniqueAbility_IsAbilityActivePrivate?.SetValue(_ability, isActive);
            SingleplayerReflectionCache.BaseUniqueAbility_IsAbilityActivePublic?.SetValue(_ability, isActive);
        }

        #endregion
    }
}
