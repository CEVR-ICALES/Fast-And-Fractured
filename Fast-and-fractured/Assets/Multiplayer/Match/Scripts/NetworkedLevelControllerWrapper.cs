using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;
using Utilities;
using Enums;
using TMPro;
using StateMachine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Networking around the singleplayer LevelController: the server builds the level and spawns the cars; clients initialize from the server's start data.</summary>
    public class NetworkedLevelControllerWrapper : NetworkBehaviour
    {
        private LevelController _levelController;

        public LevelController LevelController => _levelController;

        // Resolved in Awake: FishNet callbacks on this NetworkObject can run before other components' Awake.
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
                // Not LevelController.Instance: before the scene's one initializes it would create an auxiliar singleton.
                _levelController = FindFirstObjectByType<LevelController>(FindObjectsInactive.Include);
                
                if (_levelController == null)
                {
                    Debug.LogWarning("[NetworkedLevelControllerWrapper] Client: LevelController not found in scene. Will be set when server sends game state.");
                }
            }
        }
        
        /// <summary>Character code the local player drove (e.g. "Pepe_0"), from the spawn data; PlayerPrefs is shared between editor clones.</summary>
        public string LocalPlayerNameCode { get; private set; }

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
        
        // Construct in debugMode resets Player_Num and MaxCharactersInGame: set both between Construct and Initialize.
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
            // Only read during Initialize; left behind, the next singleplayer match expects that many humans.
            PlayerPrefs.DeleteKey("Player_Num");

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
        
        /// <summary>Mid-match spectator: the client set-up with every car remote (no HUD, input or camera of its own).</summary>
        [Client]
        public void InitializeSpectatorState(List<GameObject> characters, List<string> characterNameCodes, int gameSeed)
        {
            InitializeClientState(characters, characterNameCodes, new List<int>(), gameSeed);
        }

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

            // Runtime-created LevelController: its roster reference is null and Construct() would throw.
            EnsureCharactersDataAssigned();

            _levelController.PerformConstruct();
            
            // Construct() in debugMode overwrites Selected_Player and HUDManager reads it, so fix it first. Index in the
            // ordered client list: the name codes are compact.
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
            
            // Dependency order: LevelController -> HUDManager -> IngameEventsManager -> CameraBehaviours -> GameReadyScript.
            ConstructPlayerSingletonsInOrder();

            // Set the HUD character directly (PlayerPrefs is shared between editor clones).
            {
                string localChar = null;
                int idx = orderedClientIds != null ? orderedClientIds.IndexOf(localClientId) : localClientId;
                if (idx >= 0 && idx < characterNameCodes.Count)
                    localChar = characterNameCodes[idx];
                ForceRefreshHUDSprites(localChar);
            }

            _levelController.InGameCharactersNameCodes = characterNameCodes;
            
            // Before InitializeAfterSpawn: its onLevelPreStart timer must find remote PlayerHUDs gone and ours initialized.
            DisableRemoteCanvases();
            DisableRemotePlayerHUDs();
            DisableRemotePlayerCameraRigs();
            InitializeLocalPlayerHUD();
            
            // Timers (onLevelPreStart included) never fire without a TimerSystem runner.
            EnsureTimerSystemRunner();
            
            _levelController.InitializeAfterSpawn(characters);
            
            ReApplyNetworkInputProviders();
            
            // ActivateCharacterControls re-enables every Controller after delayUntilGameStarts.
            RegisterCharactersCustomStartGuard();
            
            // Bind HUD to local player's car (CarInjector.Install(null,true) skips speed binding)
            BindLocalPlayerHUD();

            // onLevelPreStart already ran synchronously above: resize now; the listener covers a pending pre-start.
            ResizeLocalPlayerIcon();
            _levelController.onLevelPreStart.AddListener(ResizeLocalPlayerIcon);
            AssignScreenShakeChannels(characters);
            CarNameplate.AttachAll(characters);
            ApplyLocalAccessories(characters);

        }

        /// <summary>Constructs the player singletons in dependency order (the preserver only claims s_Instance).</summary>
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
        
        /// <summary>Host-side HUD set-up (the host never runs InitializeClientState).</summary>
        public void InitializeHostHUD(List<int> orderedClientIds = null)
        {
            // Fix Selected_Player and set the HUD sprites directly (Construct() in debugMode overwrote it).
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

            // The preserver suppresses PlayerHUD auto-init on every root; restore the owner's.
            InitializeLocalPlayerHUD();

            BindLocalPlayerHUD();

            // ...and again at pre-start: the host's level loads async and Install then rebinds the speed text to every car.
            _levelController.onLevelPreStart.AddListener(BindLocalPlayerHUD);

            // Resize the local icon once SetCharactersTopElements has filled the strip.
            ResizeLocalPlayerIcon();
            _levelController.onLevelPreStart.AddListener(ResizeLocalPlayerIcon);

            ReApplyNetworkInputProviders();
            
            // ActivateCharacterControls re-enables every Controller and charactersCustomStart rebinds input: fix both after it.
            RegisterCharactersCustomStartGuard();
            AssignScreenShakeChannels(_levelController.InGameCharacters);
            CarNameplate.AttachAll(_levelController.InGameCharacters);
            ApplyLocalAccessories(_levelController.InGameCharacters);
        }

        /// <summary>Re-applies the right InputProvider after CarInjector.Install overwrote it.</summary>
        private void ReApplyNetworkInputProviders()
        {
            var vehicles = FindObjectsByType<NetworkedVehicle>(FindObjectsSortMode.None);
            foreach (var vehicle in vehicles)
            {
                vehicle.ApplyInputProviderLocally();
            }
            
            // Remote cars' state machines would read the local PlayerInputController singleton.
            DisableRemoteStateMachines();
        }

        /// <summary>Disables Controller.Update on every vehicle: NetworkedStateMachineDriver ticks the state machine from Replicate.</summary>
        private void DisableRemoteStateMachines()
        {
            var allControllers = FindObjectsByType<Controller>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var ctrl in allControllers)
            {
                var nob = ctrl.GetComponentInParent<FishNet.Object.NetworkObject>(true);
                if (nob == null) continue;

                // The owner's child state machines (shooting) keep running from Update as in singleplayer; remote ones stay off.
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

        /// <summary>After charactersCustomStart: re-disable the Controllers ActivateCharacterControls re-enabled and drop leaked input actions.</summary>
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
            
            // Every BindActions call leaks a PlayerInputAction and ShootingMode toggles once per instance: keep exactly one.
            UnityEngine.InputSystem.InputSystem.DisableAllEnabledActions();
            var ownerPic = PlayerInputController.Instance;
            if (ownerPic != null)
            {
                ownerPic.BindActions();
            }

            // LevelController adds its ReportCharacterDeath listener right after this event: remove it next frame.
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

        private void DisableRemoteCanvases()
        {
            var allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in allCanvases)
            {
                var nob = c.GetComponentInParent<FishNet.Object.NetworkObject>(true);
                if (nob == null) continue;                 // scene canvas, skip
                if (IsLocalPlayerObject(nob)) continue;    // our own canvas, skip (see IsLocalPlayerObject)
                
                if (c.gameObject.name == "Canvas" || c.gameObject.name == "PlayerCanvas")
                {
                    c.enabled = false;
                }
            }
        }

        /// <summary>
        /// The local player's equipped accessories on its own car (Progression). Other players'
        /// choices are not sent over the network yet, so their cars show none.
        /// </summary>
        private void ApplyLocalAccessories(List<GameObject> characters)
        {
            if (characters == null || string.IsNullOrEmpty(LocalPlayerNameCode)) return;
            string character = LocalPlayerNameCode.Split('_')[0];
            foreach (var car in characters)
            {
                if (car == null) continue;
                var nob = car.GetComponentInParent<NetworkObject>(true) ?? car.GetComponentInChildren<NetworkObject>(true);
                if (nob == null || !IsLocalPlayerObject(nob)) continue;
                try { Progression.AccessoryApplier.ApplyLocalLoadout(car, character); }
                catch (System.Exception e) { Debug.LogException(e); }
                break;
            }
        }

        /// <summary>IsOwner is still false for the host's own objects during its init: compare the owner connection.</summary>
        private bool IsLocalPlayerObject(NetworkObject nob)
        {
            if (nob == null) return false;
            if (nob.IsOwner) return true;

            FishNet.Connection.NetworkConnection local = base.LocalConnection;
            return local != null && nob.Owner != null && nob.Owner.ClientId == local.ClientId;
        }

        /// <summary>Deactivates the camera rig on every remote player root (two virtual cameras fought over the brain).</summary>
        private void DisableRemotePlayerCameraRigs()
        {
            var allRigs = FindObjectsByType<CameraBehaviours>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var rig in allRigs)
            {
                // includeInactive (FishNet deactivates objects while spawning); a null NetworkObject keeps the rig.
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
                // includeInactive: FishNet deactivates the host's own root while spawning it. A null NetworkObject keeps the HUD.
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

        /// <summary>Initializes the owner's PlayerHUD, whose auto-init the preserver suppressed.</summary>
        private void InitializeLocalPlayerHUD()
        {
            var allHuds = FindObjectsByType<PlayerHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var hud in allHuds)
            {
                // includeInactive, as in DisableRemotePlayerHUDs.
                var nob = hud.GetComponentInParent<NetworkObject>(true);
                if (!IsLocalPlayerObject(nob)) continue;

                RestoreAutoInitOnPlayerHUD(hud);

                SingleplayerReflectionCache.AbstractAutoInit_IsConstructed?.SetValue(hud, false);
                SingleplayerReflectionCache.AbstractAutoInit_IsInitialized?.SetValue(hud, false);

                hud.PerformConstruct();
                hud.PerformInitialize();

                // On the host onLevelPreStart may have fired already: run the HUD hookup now too (idempotent).
                SingleplayerReflectionCache.PlayerHUD_InitUpdateHUDEvents?.Invoke(hud, null);

                break;
            }
        }

        /// <summary>Timers need something calling TimerSystem.Tick() every frame.</summary>
        private void EnsureTimerSystemRunner()
        {
            if (TimerSystem.Instance == null) return;
            
            var existingRunner = FindFirstObjectByType<TimerSystemSinglePlayerRunner>();
            if (existingRunner != null) return;
            
            TimerSystem.Instance.gameObject.AddComponent<TimerSystemSinglePlayerRunner>();
        }

        private const string CHARACTERS_DATA_RESOURCES_PATH = "CharactersSkins/ListOfCharactersData";

        /// <summary>Assigns the roster asset (from Resources) before Construct(): the runtime-created LevelController has none.</summary>
        private static ListOfCharactersData _networkedRoster;
        private static ListOfCharactersData _networkedRosterSource;

        /// <summary>The singleplayer roster with each car swapped for its networked variant, built in memory.</summary>
        private static ListOfCharactersData NetworkedRoster(ListOfCharactersData source)
        {
            if (_networkedRoster != null && _networkedRosterSource == source) return _networkedRoster;

            var prefabsByName = new Dictionary<string, GameObject>();
            var spawnables = FishNet.InstanceFinder.NetworkManager != null ? FishNet.InstanceFinder.NetworkManager.SpawnablePrefabs : null;
            if (spawnables != null)
                for (int i = 0; i < spawnables.GetObjectCount(); i++)
                {
                    var nob = spawnables.GetObject(true, i);
                    if (nob != null) prefabsByName[nob.gameObject.name] = nob.gameObject;
                }

            var roster = ScriptableObject.CreateInstance<ListOfCharactersData>();
            roster.name = source.name + " (networked)";
            roster.listOfCharactersData = new List<CharacterData>();
            foreach (var data in source.listOfCharactersData)
            {
                if (data == null) continue;
                var copy = Instantiate(data);
                copy.name = data.name;
                var car = data.CarDefaultPrefab;
                if (car != null && !car.name.StartsWith("Networked") &&
                    prefabsByName.TryGetValue($"Networked{car.name} Variant", out var networkedCar))
                    copy.CarDefaultPrefab = networkedCar;
                else if (car != null && !car.name.StartsWith("Networked"))
                    Debug.LogError($"[NetworkedLevelControllerWrapper] No networked variant 'Networked{car.name} Variant' " +
                                   $"registered for {data.CharacterName}; that character cannot spawn online.");
                roster.listOfCharactersData.Add(copy);
            }
            _networkedRoster = roster;
            _networkedRosterSource = source;
            return roster;
        }

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

            var asset = field.GetValue(_levelController) as ListOfCharactersData;   // scene-placed controller
            if (asset == null) asset = Resources.Load<ListOfCharactersData>(CHARACTERS_DATA_RESOURCES_PATH);
            if (asset == null)
            {
                Debug.LogError($"[NetworkedLevelControllerWrapper] ListOfCharactersData not found at " +
                               $"Resources/{CHARACTERS_DATA_RESOURCES_PATH}. Characters cannot spawn.");
                return;
            }

            asset = NetworkedRoster(asset);
            field.SetValue(_levelController, asset);
            Debug.Log($"[NetworkedLevelControllerWrapper] Assigned ListOfCharactersData " +
                      $"({asset.listOfCharactersData?.Count ?? 0} characters) to the runtime LevelController.");

        }
        
        private void RestoreAutoInitOnPlayerHUD(PlayerHUD hud)
        {
            SingleplayerReflectionCache.AbstractAutoInit_AutoConstructOnAwake?.SetValue(hud, true);
            SingleplayerReflectionCache.AbstractAutoInit_AutoInitializeOnStart?.SetValue(hud, true);
            SingleplayerReflectionCache.AbstractAutoInit_AutoInitializeOnEnable?.SetValue(hud, true);
        }

        /// <summary>Sets HUDManager's character directly and drops its PlayerPrefs re-read (shared between editor clones).</summary>
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

            // Its delayed re-read would load stale PlayerPrefs.
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

        /// <summary>Binds the HUD speed text to the local car only (Install binds it to every car).</summary>
        private void BindLocalPlayerHUD()
        {
            if (!HUDManager.HasInstance) return;

            var vehicles = FindObjectsByType<NetworkedVehicle>(FindObjectsSortMode.None);
            foreach (var vehicle in vehicles)
            {
                // IsLocalPlayerObject, not IsOwner (see there).
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
                
                var tmpText = speedElement.textReference ?? speedElement.GetComponent<TextMeshProUGUI>();
                if (tmpText != null)
                {
                    speedOverlay.speedOverlayText = tmpText;
                }
                break;
            }
        }

        /// <summary>Enlarges the local player's icon in the top strip (not always index 0 in multiplayer).</summary>
        private void ResizeLocalPlayerIcon()
        {
            if (_levelController == null || _levelController.characterIcons == null)
                return;

            const float LOCAL_PLAYER_ICON_SIZE = 65f;
            const float OTHER_PLAYER_ICON_SIZE = 50f;

            // Empty the stray "SpeedOverlay" placeholder under the icons.
            foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                if (text.name == "SpeedOverlay" && text.text == "New Text") text.text = string.Empty;

            foreach (var icon in _levelController.characterIcons)
            {
                if (icon == null) continue;
                // Show only the slots of characters in the match.
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

        /// <summary>One impulse channel per car on every peer (the spawner only sets them on the server).</summary>
        private static void AssignScreenShakeChannels(List<GameObject> characters)
        {
            if (characters == null) return;
            for (int i = 0; i < characters.Count; i++)
            {
                var car = characters[i];
                if (car == null) continue;
                var root = car.transform.parent != null ? car.transform.parent : car.transform;
                var camera = root.GetComponentInChildren<CameraBehaviours>(true);
                if (camera != null) camera.SetLocalCinemachineImpulseListener(i);
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

        /// <summary>Migrated match: the replay made the departed host the local player; hand its singletons to this peer's car.</summary>
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

            // As a fresh client does. GameReadyScript is skipped (the manager drives the 3-2-1) and the in-match cursor kept.
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
                // Remove the auxiliar stand-in created after the departed root went away.
                if (current != null && current != singleton && current.GetComponentInParent<NetworkObject>(true) == null)
                    Destroy(current.gameObject);
                field.SetValue(null, singleton);
                return;
            }
        }
        #endregion
    }
}
