using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;
using Utilities;
using Enums;
using TMPro;
using StateMachine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Wrapper for LevelController singleton that adds networking.
    /// USES the original LevelController, doesn't replace it.
    /// 
    /// SERVER: Gets singleton reference, calls Construct/Initialize, spawns characters.
    /// CLIENT: Does NOT eagerly access singleton. Waits for server to send game state via RPC.
    ///         The client's LevelController gets initialized only after receiving spawned characters.
    /// </summary>
    public class NetworkedLevelControllerWrapper : NetworkBehaviour
    {
        private LevelController _levelController;

        public LevelController LevelController => _levelController;

        // Resolve LC eagerly in Awake so it's never null when the manager's OnStartServer fires
        // (FishNet network callbacks can race relative to Awake order across separate
        // NetworkBehaviours on the same dynamically-spawned NetworkObject).
        private void Awake()
        {
            if (_levelController == null)
                _levelController = LevelController.HasInstance
                    ? LevelController.Instance
                    : FindFirstObjectByType<LevelController>(FindObjectsInactive.Include);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (_levelController == null) _levelController = LevelController.Instance;
            if (_levelController == null)
                Debug.LogError("[NetworkedLevelControllerWrapper] Failed to get LevelController.Instance!");
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (!IsServerInitialized)
            {
                // CLIENT: Do NOT call LevelController.Instance here!
                // Accessing .Instance can create a broken auxiliar singleton if the scene's
                // LevelController hasn't been initialized yet.
                // Instead, find it directly without triggering auxiliar creation.
                _levelController = FindFirstObjectByType<LevelController>(FindObjectsInactive.Include);
                
                if (_levelController == null)
                {
                    Debug.LogWarning("[NetworkedLevelControllerWrapper] Client: LevelController not found in scene. Will be set when server sends game state.");
                }
            }
        }
        
        /// <summary>
        /// The character name code the LOCAL player actually drove this match (e.g. "Pepe_0"),
        /// resolved at init from the authoritative spawn data. PlayerPrefs("Selected_Player")
        /// is NOT trustworthy for this in multiplayer: editor clones share the registry hive,
        /// so the last writer (any peer) wins. Read by NetworkedLevelControllerManager when
        /// resolving the win/lose screen animation.
        /// </summary>
        public string LocalPlayerNameCode { get; private set; }

        /// <summary>
        /// Server: Register player's character selection
        /// </summary>
        [Server]
        public void AddCharacterSelection(int clientId, string characterNameCode)
        {
            if (_levelController != null)
            {
                _levelController.AddCharacterToListOfSelectedCharacters(clientId, characterNameCode);
            }
        }

        /// <summary>
        /// Server: removes stale character selections from disconnected clientIds before spawning.
        /// </summary>
        [Server]
        public void PruneCharacterSelectionsToActiveClients(List<int> activeClientIds)
        {
            if (_levelController == null)
                return;

            _levelController.PruneSelectedCharactersToActiveClientIds(activeClientIds);
        }
        
        // Construct in debugMode resets PlayerPrefs("Player_Num") to defaultPlayerCount (1) and
        // the spawner reads MaxCharactersInGame (serialized=8) — both must be re-set BETWEEN
        // Construct and Initialize, hence this two-arg shape.
        [Server]
        public void InitializeGame(int gameSeed, int totalCharacterCount, int humanCount)
        {
            if (_levelController == null)
            {
                Debug.LogError("[NetworkedLevelControllerWrapper] InitializeGame: _levelController is null!");
                return;
            }

            DeterministicRandom.Initialize(gameSeed);
            EnsureTimerSystemRunner();
            EnsureCharactersDataAssigned();

            _levelController.PerformConstruct();

            if (totalCharacterCount > 0)
                _levelController.MaxCharactersInGame = totalCharacterCount;
            if (humanCount > 0)
                PlayerPrefs.SetInt("Player_Num", humanCount);

            var isInitField = SingleplayerReflectionCache.AbstractAutoInit_IsInitialized;
            bool wasInit = isInitField != null && (bool)isInitField.GetValue(_levelController);
            int beforeCount = _levelController.InGameCharacters?.Count ?? -1;
            Debug.Log($"[NetworkedLevelControllerWrapper] PerformInitialize about to run. " +
                      $"_isInitialized(before)={wasInit}, MaxCharactersInGame={_levelController.MaxCharactersInGame}, " +
                      $"Player_Num={PlayerPrefs.GetInt("Player_Num")}, InGameCharacters(before)={beforeCount}");

            _levelController.PerformInitialize();

            int afterCount = _levelController.InGameCharacters?.Count ?? -1;
            Debug.Log($"[NetworkedLevelControllerWrapper] PerformInitialize done. InGameCharacters(after)={afterCount}");
        }
        
        [Server]
        public List<GameObject> GetSpawnedCharacters()
        {
            return _levelController != null ? _levelController.InGameCharacters : new List<GameObject>();
        }
        
        [Server]
        public List<string> GetCharacterNameCodes()
        {
            return _levelController != null ? _levelController.InGameCharactersNameCodes : new List<string>();
        }
        
        [Server]
        public bool GetDebugMode()
        {
            return _levelController != null && _levelController.DebugMode;
        }
        
        /// <summary>
        /// Client: Initialize client-side singleton state with data received from server.
        /// This is the ONLY place the client's LevelController gets initialized.
        /// 
        /// Critical ordering:
        /// 1. Construct singletons in dependency order (HUDManager before IngameEventsManager)
        /// 2. InitializeAfterSpawn (CarInjector.Install, onLevelPreStart timer, etc.)
        /// 3. Re-apply input providers, destroy remote PlayerHUDs, bind local HUD
        /// </summary>
        [Client]
        public void InitializeClientState(List<GameObject> characters, List<string> characterNameCodes, List<int> orderedClientIds, int gameSeed)
        {
            if (IsServerInitialized)
                return;
            
            if (_levelController == null)
            {
                _levelController = FindFirstObjectByType<LevelController>(FindObjectsInactive.Include);
            }
            
            if (_levelController == null)
            {
                Debug.LogError("[NetworkedLevelControllerWrapper] Client: Cannot find LevelController for initialization!");
                return;
            }
            
            DeterministicRandom.Initialize(gameSeed);

            // Same reason as the server path: the client's LevelController is also runtime-created,
            // so its ScriptableObject roster reference is null and Construct() would throw.
            EnsureCharactersDataAssigned();

            // Construct LevelController first (other singletons need it)
            _levelController.PerformConstruct();
            
            // Fix PlayerPrefs for local player
            // LevelController.Construct() in debugMode overwrites PlayerPrefs("Selected_Player")
            // with its serialized playerCharacter field (the host's character).
            // HUDManager.Construct() reads this for portrait/icons, so we must fix it
            // BEFORE constructing HUDManager.
            // Use position in ordered client list (not raw clientId) because
            // InGameCharactersNameCodes is compact (empty entries skipped by PrepareSpawnList).
            int localClientId = LocalConnection.ClientId;
            int playerIndex = orderedClientIds != null ? orderedClientIds.IndexOf(localClientId) : localClientId;
            if (playerIndex >= 0 && playerIndex < characterNameCodes.Count)
            {
                string localCharacter = characterNameCodes[playerIndex];
                LocalPlayerNameCode = localCharacter;
                PlayerPrefs.SetString("Selected_Player", localCharacter);
            }
            else
            {
                Debug.LogWarning($"[NetworkedLevelControllerWrapper] Could not fix PlayerPrefs: clientId={localClientId}, playerIndex={playerIndex}, characterNameCodes.Count={characterNameCodes.Count}");
            }
            
            // Construct player singletons in dependency order
            // HUDManager.Construct() needs LevelController.Instance (for onLevelPreStart)
            // IngameEventsManager.Construct() needs HUDManager.Instance.GetUIElement(PLAYER_ICONS)
            // CameraBehaviours.Construct() needs LevelController.Instance  
            // GameReadyScript.Initialize() needs IngameEventsManager.Instance
            ConstructPlayerSingletonsInOrder();

            // Force HUD sprite refresh
            // HUDManager.PerformConstruct() may have been a no-op if _isConstructed was already
            // true from a prior auto-init, or SetPlayerStartingSprites() may have silently failed
            // if ResourcesManager wasn't ready yet. Set character directly on HUDManager,
            // bypassing PlayerPrefs (unreliable in multiplayer — ParrelSync clones share registry).
            {
                string localChar = null;
                int idx = orderedClientIds != null ? orderedClientIds.IndexOf(localClientId) : localClientId;
                if (idx >= 0 && idx < characterNameCodes.Count)
                    localChar = characterNameCodes[idx];
                ForceRefreshHUDSprites(localChar);
            }

            _levelController.InGameCharactersNameCodes = characterNameCodes;
            
            // Prepare HUD BEFORE InitializeAfterSpawn
            // InitializeAfterSpawn starts a 0.1s timer that fires onLevelPreStart.
            // PlayerHUD.Initialize() and IngameEventsManager.Initialize() register listeners on onLevelPreStart.
            // We MUST destroy remote PlayerHUDs and init local PlayerHUD BEFORE that timer starts.
            DisableRemoteCanvases();
            DisableRemotePlayerHUDs();
            DisableRemotePlayerCameraRigs();
            HideDebugCharacterSelectorDropdowns();
            InitializeLocalPlayerHUD();
            
            // Ensure TimerSystem has a runner
            // TimerSystem.Tick() must be called every frame for timers to work.
            // The singleplayer scene uses TimerSystemSinglePlayerRunner, but it may not exist
            // in the multiplayer scene. Without it, timers never update and onLevelPreStart never fires.
            EnsureTimerSystemRunner();
            
            // Initialize with already-networked characters
            // This calls CarInjector.Install(null, true) for ALL cars.
            // CarInjector is on the root (same GO as the vehicle's parent), so GetComponentInChildren works.
            // Also starts onLevelPreStart timer and game loop.
            _levelController.InitializeAfterSpawn(characters);
            
            // Post-init fixups
            // Re-apply NetworkInputProvider on all vehicles AFTER CarInjector overwrote them
            ReApplyNetworkInputProviders();
            
            // Guard: ActivateCharacterControls fires after delayUntilGameStarts and
            // RE-ENABLES all Controllers (including remote ones we just disabled).
            RegisterCharactersCustomStartGuard();
            
            // Bind HUD to local player's car (CarInjector.Install(null,true) skips speed binding)
            BindLocalPlayerHUD();

            // Resize the local player's icon in the top HUD strip (and hide the unused slots).
            // InitializeAfterSpawn above invokes onLevelPreStart synchronously, so a listener added
            // here would never run for this match: apply it now. The listener stays for the case
            // where the pre-start is still pending (it is idempotent).
            ResizeLocalPlayerIcon();
            _levelController.onLevelPreStart.AddListener(ResizeLocalPlayerIcon);

        }

        /// <summary>
        /// Construct player-spawned singletons in the correct dependency order.
        /// NetworkedLocalSingletonPreserver.OnStartClient only claims ownership (sets s_Instance).
        /// Construct/Initialize must happen here where we control the order.
        /// 
        /// Dependency chain:
        ///   LevelController (already constructed above)
        ///   ->HUDManager.Construct() needs LevelController.Instance.onLevelPreStart
        ///   ->IngameEventsManager.Construct() needs HUDManager.Instance.GetUIElement(PLAYER_ICONS)
        ///   ->CameraBehaviours needs LevelController.Instance.onLevelPreStart
        ///   ->GameReadyScript.Initialize() needs IngameEventsManager.Instance
        /// </summary>
        private void ConstructPlayerSingletonsInOrder()
        {
            var preservers = FindObjectsByType<NetworkedLocalSingletonPreserver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            
            AbstractAutoInitializableMonoBehaviour hudManager = null;
            AbstractAutoInitializableMonoBehaviour ingameEventsManager = null;
            var otherSingletons = new List<AbstractAutoInitializableMonoBehaviour>();
            
            foreach (var preserver in preservers)
            {
                var singleton = preserver.SingletonToPreserve;
                if (singleton == null || !singleton.enabled) continue;
                
                // Only construct singletons on OUR player (remote ones are disabled by preserver)
                var nob = preserver.GetComponentInParent<NetworkObject>();
                if (nob == null || !nob.IsOwner) continue;
                
                if (singleton is HUDManager)
                    hudManager = singleton;
                else if (singleton is IngameEventsManager)
                    ingameEventsManager = singleton;
                else
                    otherSingletons.Add(singleton);
            }
            
            if (hudManager != null)
            {
                hudManager.PerformConstruct();
            }
            if (ingameEventsManager != null)
            {
                ingameEventsManager.PerformConstruct();
            }
            foreach (var singleton in otherSingletons)
            {
                singleton.PerformConstruct();
            }
            
            if (hudManager != null)
                hudManager.PerformInitialize();
            if (ingameEventsManager != null)
                ingameEventsManager.PerformInitialize();
            foreach (var singleton in otherSingletons)
                singleton.PerformInitialize();
            
        }
        
        /// <summary>
        /// Host-side HUD initialization. Called from SentLevelControllerStartData when IsServerInitialized.
        /// The host doesn't run InitializeClientState, but still needs PlayerHUD disabled on non-owner
        /// and HUD speed binding for the local player.
        /// </summary>
        public void InitializeHostHUD(List<int> orderedClientIds = null)
        {
            // Fix PlayerPrefs for host and force HUD sprites directly.
            // LevelController.Construct() in debugMode overwrites Selected_Player with
            // the serialized default. We fix PlayerPrefs AND apply sprites directly to
            // HUDManager, bypassing PlayerPrefs for the actual sprite load.
            string hostCharacter = null;
            if (_levelController != null)
            {
                var nameCodes = _levelController.InGameCharactersNameCodes;
                int hostClientId = LocalConnection.ClientId;
                int playerIndex = orderedClientIds != null ? orderedClientIds.IndexOf(hostClientId) : hostClientId;
                if (playerIndex >= 0 && playerIndex < nameCodes.Count)
                {
                    hostCharacter = nameCodes[playerIndex];
                    LocalPlayerNameCode = hostCharacter;
                    PlayerPrefs.SetString("Selected_Player", hostCharacter);
                }
                Debug.Log($"[NetworkedLevelControllerWrapper] InitializeHostHUD: hostClientId={hostClientId}, " +
                          $"playerIndex={playerIndex}, nameCodes=[{string.Join(",", nameCodes)}], " +
                          $"hostCharacter={hostCharacter}, HUDManager.HasInstance={HUDManager.HasInstance}");
            }

            ForceRefreshHUDSprites(hostCharacter);

            DisableRemotePlayerHUDs();
            DisableRemotePlayerCameraRigs();

            // The host used to skip this entirely — it was only ever called from
            // InitializeClientState — which is why the HOST's HUD showed no overheat, no push
            // cooldown, no ultimate charge while the remote client's HUD worked. The preserver
            // suppresses PlayerHUD auto-init on every player root (NetworkedLocalSingletonPreserver
            // .Awake) so that a remote car's PlayerHUD cannot bind its cooldowns to our HUDManager;
            // restoring the OWNER's is this method's job, and nothing was doing it on the host.
            InitializeLocalPlayerHUD();

            BindLocalPlayerHUD();

            // ...and again once the level actually pre-starts. On the CLIENT this call sits safely
            // after InitializeAfterSpawn (step 5 of InitializeClientState), but the host reaches
            // InitializeHostHUD from WaitForClientsThenSpawn, which runs it right after
            // LoadResolvedScene() — an ASYNC load, so the level's own InitializeAfterSpawn (and the
            // CarInjector.Install(null, true) inside it) generally runs later. Install re-binds the
            // shared SPEED_INDICATOR text to the CarSpeedOverlay of EVERY car, so the remote car's
            // overlay then overwrites the host's speed reading every frame — the host's speed
            // indicator stops tracking their own car. onLevelPreStart is invoked immediately after
            // that Install (LevelController.cs:185-199), so re-binding there lands after it in
            // either ordering. BindLocalPlayerHUD is idempotent.
            _levelController.onLevelPreStart.AddListener(BindLocalPlayerHUD);

            // Resize local player's icon after SetCharactersTopElements populates the icons (now
            // if that already happened, else when the level pre-starts).
            ResizeLocalPlayerIcon();
            _levelController.onLevelPreStart.AddListener(ResizeLocalPlayerIcon);

            // Re-apply correct InputProviders on host.
            // CarInjector.Install(null, true) in InitializeAfterSpawn overwrites ALL vehicles'
            // InputProvider to PlayerInputProvider. This causes remote vehicles to read the host's
            // input (shoot, dash, ultimate all mirror). Must restore NetworkInputProvider on non-owner vehicles.
            ReApplyNetworkInputProviders();
            
            // Guard: ActivateCharacterControls fires after delayUntilGameStarts (3.5s) and
            // RE-ENABLES all Controllers (including remote ones we just disabled).
            // Also, Controller.CustomStart ->LoadFirsState is called by charactersCustomStart,
            // and disabled PlayerInputControllers get BindActions creating duplicate input listeners.
            // Register a post-guard that fixes both issues after the event fires.
            RegisterCharactersCustomStartGuard();

        }

        /// <summary>
        /// After InitializeAfterSpawn (which calls CarInjector.Install), re-apply the correct
        /// InputProvider on all NetworkedVehicle components. CarInjector.Install overwrites
        /// InputProvider to PlayerInputProvider, but non-owner cars need NetworkInputProvider.
        /// </summary>
        private void ReApplyNetworkInputProviders()
        {
            var vehicles = FindObjectsByType<NetworkedVehicle>(FindObjectsSortMode.None);
            foreach (var vehicle in vehicles)
            {
                vehicle.ApplyInputProviderLocally();
            }
            
            // Disable state machine Controllers on non-owner vehicles.
            // The singleplayer state machine reads from PlayerInputController.Instance (singleton),
            // so ALL vehicles would share the local player's input if left enabled.
            DisableRemoteStateMachines();
        }

        /// <summary>
        /// Disable Controller.Update on ALL networked vehicles (owner and non-owner). The state
        /// machine is now driven by NetworkedStateMachineDriver from NetworkedVehicle.Replicate
        /// at tick rate, eliminating the duplicate frame-rate driver. For owners this fixes the
        /// double ProcessMovementInput call (state machine + Replicate). For non-owners it
        /// eliminates the singleton-input bleed (their state machine read PlayerInputController.Instance).
        /// </summary>
        private void DisableRemoteStateMachines()
        {
            var allControllers = FindObjectsByType<Controller>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var ctrl in allControllers)
            {
                var nob = ctrl.GetComponentInParent<FishNet.Object.NetworkObject>(true);
                if (nob == null) continue;

                // The owner's CHILD state machines (the shooting Controller) must keep running from
                // Update at frame rate, exactly like singleplayer: they only translate local input
                // into TryPushShoot/TryMineShoot/ability calls, which already route through the
                // owner/server paths in NetworkedShoot. Nothing ticks them from the network loop —
                // NetworkedVehicle only ticks the MOVEMENT Controller (the one on the root NOB's own
                // GameObject) — so taking them over left ulti, push shoot, mine and the push aim
                // trace completely dead on any car where this ran. Non-owned cars keep everything
                // disabled: their state machines read the local PlayerInputController singleton and
                // would bleed this client's input into remote replicas.
                bool isMovementController = ctrl.gameObject == nob.gameObject;
                if (nob.IsOwner && !isMovementController)
                {
                    var existingDriver = ctrl.GetComponent<FastAndFractured.Multiplayer.NetworkedStateMachineDriver>();
                    if (existingDriver != null)
                        existingDriver.ReleaseToUpdate();
                    else if (!ctrl.enabled)
                        ctrl.enabled = true;
                    continue;
                }

                var driver = ctrl.GetComponent<FastAndFractured.Multiplayer.NetworkedStateMachineDriver>();
                if (driver == null)
                    driver = ctrl.gameObject.AddComponent<FastAndFractured.Multiplayer.NetworkedStateMachineDriver>();
                driver.TakeOverFromUpdate();
            }
        }

        /// <summary>
        /// Register a listener on charactersCustomStart that fires AFTER Controller.CustomStart 
        /// and PlayerInputController.BindActions. This fixes two issues:
        /// 
        /// 1. ActivateCharacterControls() (called by the game loop after delayUntilGameStarts)
        ///    RE-ENABLES ALL Controller components, including remote ones we disabled.
        ///    We re-disable them here.
        /// 
        /// 2. Controller.Awake() on non-owner vehicles registered CustomStart on charactersCustomStart.
        ///    PlayerInputController.Construct() on non-owner vehicles registered BindActions too.
        ///    When the event fires, disabled PlayerInputControllers create duplicate PlayerInputAction
        ///    instances (leaked input listeners). We disable those actions here.
        /// </summary>
        private bool _guardRegistered = false;
        private void RegisterCharactersCustomStartGuard()
        {
            if (_guardRegistered || _levelController == null) return;
            _guardRegistered = true;
            
            _levelController.charactersCustomStart.AddListener(OnCharactersCustomStartGuard);
        }

        private void OnCharactersCustomStartGuard()
        {
            // ActivateCharacterControls re-enabled every Controller, remote ones included.
            DisableRemoteStateMachines();
            
            // Non-owner PlayerInputControllers must not answer the local keyboard.
            var allInputControllers = FindObjectsByType<PlayerInputController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var pic in allInputControllers)
            {
                var nob = pic.GetComponentInParent<FishNet.Object.NetworkObject>(true);
                if (nob == null) continue;
                if (nob.IsOwner) continue;
                
                if (pic.InputActions != null)
                {
                    pic.InputActions.Disable();
                }
            }
            
            // BindActions() runs once per vehicle (CarInjector.Install -> PlayerInputProvider
            // .Initialize) plus once from charactersCustomStart, and each call leaks a new
            // PlayerInputAction onto the PlayerInputController singleton. ChangeShootMode()
            // toggles _isPushShootMode, so N leaked instances toggle it N+1 times per click and
            // an even count leaves push shoot permanently off. Drop every enabled action, then
            // rebind exactly one. Init-time only; PauseManager re-enables its own actions.
            UnityEngine.InputSystem.InputSystem.DisableAllEnabledActions();
            var ownerPic = PlayerInputController.Instance;
            if (ownerPic != null)
            {
                ownerPic.BindActions();
            }

            // LevelController adds its single-player ReportCharacterDeath listener immediately
            // after ActivateCharacterControls (and therefore after this UnityEvent) returns.
            // Remove only that listener on the next frame, leaving all other StatsController
            // listeners untouched and keeping the adaptation wholly inside Multiplayer.
            StartCoroutine(RemoveSingleplayerDeathListenersNextFrame());
            
        }

        private System.Collections.IEnumerator RemoveSingleplayerDeathListenersNextFrame()
        {
            yield return null;

            var networkedStats = FindObjectsByType<NetworkedStatsController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var stats in networkedStats)
            {
                if (stats != null)
                    stats.RemoveSingleplayerDeathListener(_levelController);
            }
        }

        /// <summary>
        /// Disable ALL Canvas components on non-owner player hierarchies.
        /// Ensures remote players' UI is fully invisible even if the preserver
        /// missed a canvas (e.g. "Canvas" not being a direct parent of a managed singleton).
        /// </summary>
        private void DisableRemoteCanvases()
        {
            var allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in allCanvases)
            {
                var nob = c.GetComponentInParent<FishNet.Object.NetworkObject>(true);
                if (nob == null) continue;                 // scene canvas, skip
                if (IsLocalPlayerObject(nob)) continue;    // our own canvas, skip (see IsLocalPlayerObject)
                
                // Only disable canvases named "Canvas" or "PlayerCanvas" (player HUD canvases)
                if (c.gameObject.name == "Canvas" || c.gameObject.name == "PlayerCanvas")
                {
                    c.enabled = false;
                }
            }
        }

        /// <summary>
        /// BUG-4 fix (2026-09-16): a stray "Josefino" TMP dropdown floats in the game view during
        /// every match. Root cause found live: DebugCharacterSelector's "Dropdown" GameObject ships
        /// active on the networked player prefab and is never hidden once character selection is
        /// done - confirmed by inspecting a live match, where the LOCAL player's own Dropdown was
        /// active=true under an ENABLED canvas (DisableRemoteCanvases correctly leaves the local
        /// player's own canvas alone, since that canvas is the real HUD - this dropdown just rides
        /// along inside it). Unlike the HUD/camera/canvas passes above, this has nothing to do with
        /// ownership: DebugCharacterSelector's job (send a character choice to the server) is done
        /// well before this init runs (character selection happens in the lobby, this runs after
        /// spawn), so it is safe to hide for every player, not just remote ones. Deactivating the
        /// GameObject only stops it from rendering/receiving UI input - AutoCharacterSelector or
        /// DistributedTestSystem calling its public methods directly (not via a click) still works,
        /// since those are plain C# calls, unaffected by the GameObject being inactive.
        /// </summary>
        private void HideDebugCharacterSelectorDropdowns()
        {
            var selectors = FindObjectsByType<DebugCharacterSelector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var selector in selectors)
            {
                if (selector != null && selector.gameObject.activeSelf)
                    selector.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Destroy PlayerHUD components on non-owner player hierarchies.
        /// PlayerHUD is on the player root (NetworkedPlayer), NOT on the vehicle child.
        /// 
        /// Must DESTROY (not just disable) because PlayerHUD.Initialize() adds
        /// InitUpdateHUDEvents as an onLevelPreStart listener. Dynamic UnityEvent listeners
        /// fire even on disabled MonoBehaviours. Only destroying the component makes Unity
        /// skip the listener when the event fires.
        /// 
        /// Without this, remote cars' cooldown events update OUR HUDManager (HUD overlap).
        /// </summary>
        /// <summary>
        /// Whether a NetworkObject belongs to the LOCAL player.
        ///
        /// <see cref="NetworkObject.IsOwner"/> is not usable from the host's init path.
        /// InitializeHostHUD runs synchronously right after SpawnCharactersOnClients, when the
        /// server has already assigned ownership but the host's own CLIENT half has not processed
        /// the spawn yet — so IsOwner is still false for the host's OWN objects, and every
        /// "is this remote?" test built on it answers yes for the local player.
        ///
        /// Measured live (2026-08-12, MountainVillage, same match, both peers):
        ///   client (initializes from an ObserversRpc that arrives after spawn processing):
        ///       PlayerHUD alive = 1, IsOwner = True, owned car speedText = SpeedIndicator_TMP
        ///   host  (initializes synchronously during spawn):
        ///       PlayerHUD alive = 0,                 owned car speedText = NULL
        /// One cause, both host symptoms: the HUD was destroyed and the speed overlay unbound as if
        /// the host's own player were a remote one. Driving still worked only because
        /// NetworkedVehicle.OnOwnershipClient re-applies the input provider once ownership lands —
        /// a Destroy has no such second chance.
        ///
        /// Comparing the owner CONNECTION is valid on the server as soon as Spawn(conn) returns,
        /// and is equivalent to IsOwner on a pure client.
        /// </summary>
        private bool IsLocalPlayerObject(NetworkObject nob)
        {
            if (nob == null) return false;
            if (nob.IsOwner) return true;

            FishNet.Connection.NetworkConnection local = base.LocalConnection;
            return local != null && nob.Owner != null && nob.Owner.ClientId == local.ClientId;
        }

        /// <summary>
        /// Deactivate the camera rig on every non-local player root.
        ///
        /// Each NetworkedPlayer carries its own FreeLookCameraLast rig (virtual camera, camera
        /// collider, impulse listeners). Nothing used to switch the remote ones off, so a two-player
        /// match ran two virtual cameras at equal priority against the single CinemachineBrain, plus
        /// a second CinemachineCollider raycasting against the map every frame. Which one wins is
        /// decided by activation order, which differs between host and client - that is why the
        /// tremble showed up on the client and not on the host.
        ///
        /// Deactivating the whole rig object also takes its CameraBehaviours out of
        /// AbstractSingleton's lookups, which exclude inactive objects, so CameraBehaviours.Instance
        /// resolves to the local player's rig.
        /// </summary>
        private void DisableRemotePlayerCameraRigs()
        {
            var allRigs = FindObjectsByType<CameraBehaviours>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var rig in allRigs)
            {
                // Same fail-safe ownership rule as the HUD pass: includeInactive because FishNet
                // deactivates a NetworkObject while spawning it, and a null NetworkObject means
                // "not provably remote", so the rig stays.
                var nob = rig.GetComponentInParent<NetworkObject>(true);
                if (nob == null) continue;
                if (IsLocalPlayerObject(nob)) continue;
                if (!rig.gameObject.activeSelf) continue;

                Debug.Log($"[NetworkedLevelControllerWrapper] Deactivating remote camera rig '{rig.gameObject.name}' " +
                          $"under '{nob.gameObject.name}' (OwnerId={(nob.Owner != null ? nob.Owner.ClientId : -1)}, " +
                          $"localClientId={(base.LocalConnection != null ? base.LocalConnection.ClientId : -1)}). " +
                          "If this names the LOCAL player's rig, the local peer loses its camera and the ownership test is wrong.");

                rig.gameObject.SetActive(false);
            }
        }

        private void DisableRemotePlayerHUDs()
        {
            var allHuds = FindObjectsByType<PlayerHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var hud in allHuds)
            {
                // includeInactive: FishNet deactivates a NetworkObject while spawning it, and the
                // host reaches this straight from WaitForClientsThenSpawn, so the active-only
                // overload returns null for the host's own still-spawning player root.
                // Then fail SAFE: a null NetworkObject means "not provably remote", so keep the HUD.
                var nob = hud.GetComponentInParent<NetworkObject>(true);
                if (nob == null) continue;
                if (IsLocalPlayerObject(nob)) continue;

                Debug.Log($"[NetworkedLevelControllerWrapper] Destroying remote PlayerHUD on '{nob.gameObject.name}' " +
                          $"(IsOwner={nob.IsOwner}, OwnerId={(nob.Owner != null ? nob.Owner.ClientId : -1)}, " +
                          $"localClientId={(base.LocalConnection != null ? base.LocalConnection.ClientId : -1)}). " +
                          "If this names the LOCAL player's root, the ownership test is wrong again.");

                hud.enabled = false;
                Destroy(hud);
            }
        }

        /// <summary>
        /// Initialize the local (owner) PlayerHUD. Auto-init was suppressed by the preserver
        /// in Awake to prevent the host's PlayerHUD from subscribing onLevelPreStart.
        /// Now that remote PlayerHUDs are destroyed, we can safely init the owner's.
        /// This calls PerformConstruct + PerformInitialize which adds InitUpdateHUDEvents
        /// as an onLevelPreStart listener (fires after the 0.1s delay from InitializeAfterSpawn).
        /// </summary>
        private void InitializeLocalPlayerHUD()
        {
            var allHuds = FindObjectsByType<PlayerHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var hud in allHuds)
            {
                // includeInactive for the same reason as DisableRemotePlayerHUDs: the host runs this
                // while its own player root can still be deactivated by FishNet's spawn, and the
                // active-only overload would return null and skip the very HUD we came to initialize.
                var nob = hud.GetComponentInParent<NetworkObject>(true);
                if (!IsLocalPlayerObject(nob)) continue;

                RestoreAutoInitOnPlayerHUD(hud);

                SingleplayerReflectionCache.AbstractAutoInit_IsConstructed?.SetValue(hud, false);
                SingleplayerReflectionCache.AbstractAutoInit_IsInitialized?.SetValue(hud, false);

                hud.PerformConstruct();
                hud.PerformInitialize();

                // PerformInitialize only ADDS InitUpdateHUDEvents as an onLevelPreStart listener.
                // That is enough on a remote client, where the car exists before the level starts,
                // but not on the host: LevelController.InitializeAfterSpawn() invokes
                // onLevelPreStart synchronously (LevelController.cs:199), and on the host that can
                // already have happened by the time we get here — the listener is then registered
                // for an event that will never fire again, so overheat, push cooldown, dash and
                // ultimate never update. Invoking it directly makes the wiring independent of that
                // ordering. Safe in the other order too: if onLevelPreStart has not fired yet it
                // will call the same method again, and every handler is an idempotent HUD setter.
                SingleplayerReflectionCache.PlayerHUD_InitUpdateHUDEvents?.Invoke(hud, null);

                break;
            }
        }

        /// <summary>
        /// Ensure TimerSystem has something calling Tick() every frame.
        /// In singleplayer, TimerSystemSinglePlayerRunner does this. In multiplayer scenes
        /// it may not exist, causing all timers (including onLevelPreStart delay) to never fire.
        /// </summary>
        private void EnsureTimerSystemRunner()
        {
            if (TimerSystem.Instance == null) return;
            
            var existingRunner = FindFirstObjectByType<TimerSystemSinglePlayerRunner>();
            if (existingRunner != null) return;
            
            TimerSystem.Instance.gameObject.AddComponent<TimerSystemSinglePlayerRunner>();
        }

        private const string CHARACTERS_DATA_RESOURCES_PATH = "CharactersSkins/ListOfCharactersData";

        /// <summary>
        /// Assigns LevelController.charactersDataScriptableObject before Construct() runs.
        ///
        /// The dev branch refactored the roster from a serialized List&lt;CharacterData&gt; to a
        /// ListOfCharactersData ScriptableObject, and Construct() now dereferences it with no null
        /// check. Singleplayer is fine because its LevelController is placed in a scene/prefab with
        /// the reference wired in the inspector — but NO scene serializes a LevelController for
        /// multiplayer: the singleton accessor creates one at runtime, so every serialized field is
        /// at its default (null) and Construct() throws a NullReferenceException, which aborted the
        /// whole match start ("Manager spawn / game start threw").
        ///
        /// The asset lives under a Resources folder precisely so it can be resolved without a scene
        /// reference, so load it and assign it via reflection (no singleplayer code is modified).
        /// </summary>
        private void EnsureCharactersDataAssigned()
        {
            var field = SingleplayerReflectionCache.LevelController_CharactersDataScriptableObject;
            if (field == null)
            {
                Debug.LogError("[NetworkedLevelControllerWrapper] Could not resolve " +
                               "LevelController.charactersDataScriptableObject via reflection — the " +
                               "singleplayer field was renamed. Match start will fail.");
                return;
            }

            if (field.GetValue(_levelController) != null)
                return; // already wired (scene-placed controller)

            var asset = Resources.Load<ListOfCharactersData>(CHARACTERS_DATA_RESOURCES_PATH);
            if (asset == null)
            {
                Debug.LogError($"[NetworkedLevelControllerWrapper] ListOfCharactersData not found at " +
                               $"Resources/{CHARACTERS_DATA_RESOURCES_PATH}. Characters cannot spawn.");
                return;
            }

            field.SetValue(_levelController, asset);
            Debug.Log($"[NetworkedLevelControllerWrapper] Assigned ListOfCharactersData " +
                      $"({asset.listOfCharactersData?.Count ?? 0} characters) to the runtime LevelController.");

            // NOTE — deliberately NOT binding LevelController._sandStormController here.
            // The multiplayer LevelController is the runtime singleton, so that reference is null
            // too and GameLoopManager logs "_sandStormController is null. Cannot set up storm."
            // (pre-existing: the sandstorm never runs in multiplayer). Binding the map's
            // SandstormController was tried and made things WORSE: the networked maps' controller
            // has its own unassigned `sphereCenter`, so SetupStorm then threw
            // UnassignedReferenceException and aborted match start entirely. Fixing the sandstorm
            // needs the networked scenes regenerated with that reference wired — a separate job.
        }
        
        /// <summary>
        /// Restore auto-init flags on a PlayerHUD that was suppressed by the preserver.
        /// </summary>
        private void RestoreAutoInitOnPlayerHUD(PlayerHUD hud)
        {
            SingleplayerReflectionCache.AbstractAutoInit_AutoConstructOnAwake?.SetValue(hud, true);
            SingleplayerReflectionCache.AbstractAutoInit_AutoInitializeOnStart?.SetValue(hud, true);
            SingleplayerReflectionCache.AbstractAutoInit_AutoInitializeOnEnable?.SetValue(hud, true);
        }

        /// <summary>
        /// Forces HUDManager to display sprites for the given character, bypassing PlayerPrefs.
        ///
        /// PlayerPrefs is unreliable in multiplayer: ParrelSync clones share the same Windows
        /// registry key, and the delayed onLevelPreStart re-read can pick up another clone's
        /// value. This method sets HUDManager's private fields directly and calls
        /// SetPlayerStartingSprites, then unregisters the onLevelPreStart listener to prevent
        /// any future PlayerPrefs re-read from overwriting the correct sprites.
        /// </summary>
        private void ForceRefreshHUDSprites(string characterCode)
        {
            if (!HUDManager.HasInstance || string.IsNullOrEmpty(characterCode)) return;

            var hud = HUDManager.Instance;
            var fSelectedPlayer = SingleplayerReflectionCache.HUDManager_SelectedPlayer;

            if (fSelectedPlayer == null)
            {
                Debug.LogWarning("[NetworkedLevelControllerWrapper] Could not find HUDManager.selectedPlayer field");
                return;
            }

            fSelectedPlayer.SetValue(hud, characterCode);
            SingleplayerReflectionCache.HUDManager_SelectedPlayerHalfBody?.SetValue(hud, characterCode + "_HALFBODY");
            string baseName = characterCode.Split('_')[0];
            SingleplayerReflectionCache.HUDManager_SelectedPlayerUA?.SetValue(hud, baseName + "_UA");
            SingleplayerReflectionCache.HUDManager_SelectedPlayerPush?.SetValue(hud, baseName + "_PUSH");
            SingleplayerReflectionCache.HUDManager_SelectedPlayerShoot?.SetValue(hud, baseName + "_SHOOT");

            SingleplayerReflectionCache.HUDManager_SetPlayerStartingSprites?.Invoke(hud, null);

            // Unregister RegisterSelectedPlayer from onLevelPreStart to prevent the delayed
            // re-read (0.1s timer) from overwriting our sprites with stale PlayerPrefs data.
            if (LevelController.HasInstance)
            {
                var registerMethod = SingleplayerReflectionCache.HUDManager_RegisterSelectedPlayer;
                if (registerMethod != null)
                {
                    var del = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(
                        typeof(UnityEngine.Events.UnityAction), hud, registerMethod);
                    LevelController.Instance.onLevelPreStart.RemoveListener(del);
                }
            }
        }

        /// <summary>
        /// Bind the HUD speed indicator to the local player's car, and UNBIND it from every
        /// other car. CarInjector.Install binds the shared HUD SPEED_INDICATOR text to a
        /// CarSpeedOverlay on EVERY player car, so remote cars' overlays write their own
        /// speed into the local HUD every frame (a stationary remote car pins it at "0,0").
        /// Clearing the reference on non-owned overlays stops the clobbering; the local
        /// overlay is (re)bound right after.
        /// </summary>
        private void BindLocalPlayerHUD()
        {
            if (!HUDManager.HasInstance) return;

            var vehicles = FindObjectsByType<NetworkedVehicle>(FindObjectsSortMode.None);
            foreach (var vehicle in vehicles)
            {
                // IsLocalPlayerObject, not IsOwner — on the host this runs before its own client-side
                // spawn is processed, and IsOwner would report the host's OWN car as remote, so the
                // loop below would unbind the speed text it is about to bind. See IsLocalPlayerObject.
                if (IsLocalPlayerObject(vehicle.NetworkObject)) continue;
                var remoteOverlay = vehicle.GetComponent<CarSpeedOverlay>();
                if (remoteOverlay != null)
                    remoteOverlay.speedOverlayText = null;
            }

            foreach (var vehicle in vehicles)
            {
                if (!IsLocalPlayerObject(vehicle.NetworkObject)) continue;

                var speedOverlay = vehicle.GetComponent<CarSpeedOverlay>();
                if (speedOverlay == null) continue;
                
                var speedElement = HUDManager.Instance.GetUIElement(UIDynamicElementType.SPEED_INDICATOR);
                if (speedElement == null)
                {
                    Debug.LogWarning("[NetworkedLevelControllerWrapper] SPEED_INDICATOR not found in HUD");
                    continue;
                }
                
                // Use textReference from UIDynamicElement, matching CarInjector.Install(prefab,name,provider) pattern
                var tmpText = speedElement.textReference ?? speedElement.GetComponent<TextMeshProUGUI>();
                if (tmpText != null)
                {
                    speedOverlay.speedOverlayText = tmpText;
                }
                break;
            }
        }

        /// <summary>
        /// Resize the local player's character icon in the HUD top strip.
        /// In singleplayer, Image_Player(0) is always the local player and is 65x65.
        /// In multiplayer the local player may not be index 0, so we find their icon
        /// and enlarge it, resetting the others to the default 50x50 size.
        /// Called on onLevelPreStart, AFTER IngameEventsManager.SetCharactersTopElements populates the icons.
        /// </summary>
        private void ResizeLocalPlayerIcon()
        {
            if (_levelController == null || _levelController.characterIcons == null)
                return;

            const float LOCAL_PLAYER_ICON_SIZE = 65f;
            const float OTHER_PLAYER_ICON_SIZE = 50f;

            // The player canvas has a stray "SpeedOverlay" text that nothing writes to (the speed
            // goes to the SPEED_INDICATOR element), so it showed its "New Text" placeholder under
            // the icons. Empty it.
            foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                if (text.name == "SpeedOverlay" && text.text == "New Text") text.text = string.Empty;

            foreach (var icon in _levelController.characterIcons)
            {
                if (icon == null) continue;
                // The networked HUD has 12 slots, all active; a slot with no character used to
                // stay on screen as an empty square. Show only the characters in the match.
                if (icon.Character == null)
                {
                    icon.gameObject.SetActive(false);
                    continue;
                }

                var nob = icon.Character.GetComponentInParent<NetworkObject>(true);
                if (nob == null)
                    nob = icon.Character.GetComponentInChildren<NetworkObject>(true);

                var rt = icon.GetComponent<RectTransform>();
                if (rt == null) continue;

                if (IsLocalPlayerObject(nob))
                {
                    rt.sizeDelta = new Vector2(LOCAL_PLAYER_ICON_SIZE, LOCAL_PLAYER_ICON_SIZE);
                }
                else
                {
                    rt.sizeDelta = new Vector2(OTHER_PLAYER_ICON_SIZE, OTHER_PLAYER_ICON_SIZE);
                }
            }

        }

        #region HOST_MIGRATION
        private const System.Reflection.BindingFlags INSTANCE_FLAGS =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;

        /// <summary>
        /// The host's own car, once its NetworkObject is started on the host's client side.
        /// </summary>
        public GameObject FindLocalPlayerCar()
        {
            foreach (var vehicle in FindObjectsByType<NetworkedVehicle>(FindObjectsSortMode.None))
            {
                var nob = vehicle.GetComponent<NetworkObject>();
                if (nob != null && nob.IsOwner) return vehicle.gameObject;
            }
            return null;
        }

        /// <summary>
        /// Host of a migrated match. The new host replays the original start, so the singleplayer
        /// level makes the FIRST human - the host that left - its local player: LocalPlayer and
        /// the player-root singletons (HUD, top icons, events, camera, input, pause menu) are that
        /// car's, and they go when its root is removed. The new host's own root never got them,
        /// so its HUD showed the empty icon strip (playtest 2026-10-04). Hand them to this
        /// peer's car and replay the level-start steps that already ran for the departed one.
        /// </summary>
        public void RebindLocalPlayerAfterMigration(GameObject localCar)
        {
            if (_levelController == null || localCar == null) return;

            typeof(LevelController).GetField("_localPlayer", INSTANCE_FLAGS)?.SetValue(_levelController, localCar);

            var root = localCar.transform.parent != null ? localCar.transform.parent : localCar.transform;
            AbstractAutoInitializableMonoBehaviour hudManager = null, ingameEvents = null;
            var others = new List<AbstractAutoInitializableMonoBehaviour>();
            foreach (var preserver in root.GetComponentsInChildren<NetworkedLocalSingletonPreserver>(true))
            {
                var singleton = preserver.SingletonToPreserve;
                if (singleton == null) continue;
                singleton.gameObject.SetActive(true);
                singleton.enabled = true;
                TakeSingletonInstance(singleton);
                if (singleton is HUDManager) hudManager = singleton;
                else if (singleton is IngameEventsManager) ingameEvents = singleton;
                else others.Add(singleton);
            }

            // Construct/Initialize them as a fresh client does (ConstructPlayerSingletonsInOrder).
            // GameReadyScript is left alone: its Initialize schedules the 3-2-1, which the
            // manager already drives from the server tick. MainMenuManager.Construct shows the
            // cursor (menu default); keep the in-match cursor state.
            var ordered = new List<AbstractAutoInitializableMonoBehaviour>();
            if (hudManager != null) ordered.Add(hudManager);
            if (ingameEvents != null) ordered.Add(ingameEvents);
            foreach (var s in others) if (!(s is GameReadyScript)) ordered.Add(s);
            var cursorLock = Cursor.lockState;
            bool cursorVisible = Cursor.visible;
            // IngameEventsManager.Construct appends the HUD's icons to its list: start it empty.
            if (ingameEvents != null &&
                typeof(IngameEventsManager).GetField("inGameCharactersTopIcons", INSTANCE_FLAGS)?.GetValue(ingameEvents) is List<CharacterIcon> icons)
                icons.Clear();
            foreach (var s in ordered)
            {
                SingleplayerReflectionCache.AbstractAutoInit_IsConstructed?.SetValue(s, false);
                SingleplayerReflectionCache.AbstractAutoInit_IsInitialized?.SetValue(s, false);
                s.PerformConstruct();
            }
            foreach (var s in ordered) s.PerformInitialize();
            Cursor.lockState = cursorLock;
            Cursor.visible = cursorVisible;

            // onLevelPreStart already fired, for the departed car: redo what it did for this one.
            if (ingameEvents is IngameEventsManager events) events.SetCharactersTopElements();
            foreach (var camera in root.GetComponentsInChildren<CameraBehaviours>(true))
                typeof(CameraBehaviours).GetField("_statsController", INSTANCE_FLAGS)?.SetValue(camera, localCar.GetComponent<StatsController>());
            foreach (var minimap in root.GetComponentsInChildren<MinimapFollowPlayer>(true))
            {
                typeof(MinimapFollowPlayer).GetField("_isPlayerReceived", INSTANCE_FLAGS)?.SetValue(minimap, false);
                typeof(MinimapFollowPlayer).GetMethod("OnCharactersCustomStart", INSTANCE_FLAGS)?.Invoke(minimap, null);
            }
            if (PlayerInputController.HasInstance) PlayerInputController.Instance.BindActions();

            InitializeLocalPlayerHUD();
            ForceRefreshHUDSprites(LocalPlayerNameCode);
            BindLocalPlayerHUD();
            ResizeLocalPlayerIcon();
            MpCanary.Mark($"HostMigration: local player is now '{localCar.name}' ({ordered.Count} HUD singleton(s) rebound)");
        }

        /// <summary>Point AbstractSingleton&lt;T&gt;.s_Instance at this component, dropping a stand-in.</summary>
        private static void TakeSingletonInstance(Component singleton)
        {
            for (var type = singleton.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
            {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(AbstractSingleton<>)) continue;
                var field = type.GetField("s_Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                if (field == null) return;
                var current = field.GetValue(null) as Component;
                // A singleton read after the departed root went away is recreated as a bare
                // "auxiliar" object; remove it so it does not linger next to the real one.
                if (current != null && current != singleton && current.GetComponentInParent<NetworkObject>(true) == null)
                    Destroy(current.gameObject);
                field.SetValue(null, singleton);
                return;
            }
        }
        #endregion
    }
}
