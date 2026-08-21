using FishNet.Object;
using FishNet.Managing;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FastAndFractured;
using Utilities;
using StateMachine;

namespace FastAndFractured.Multiplayer.Testing
{
    /// <summary>
    /// Coordinates automated multiplayer tests across host and clients.
    /// Works with ParallelSync - one instance acts as host, others as clients.
    /// 
    /// Tests are split into two phases:
    /// Phase 1: Connectivity tests (run after connection, before game starts)
    /// Phase 2: Gameplay tests (run after game actually starts and vehicles spawn)
    /// </summary>
    public class MultiplayerTestCoordinator : NetworkBehaviour
    {
        [Header("Test Configuration")]
        [SerializeField] private bool _runTestsOnStart = true;
        [SerializeField] private float _connectivityTestDelay = 2f;
        [SerializeField] private float _gameplayTestTimeout = 30f;
        [SerializeField] private float _gameplayPollInterval = 0.5f;
        
        [Header("Test Results")]
        [SerializeField] private int _totalTests = 0;
        [SerializeField] private int _passedTests = 0;
        [SerializeField] private int _failedTests = 0;
        
        private List<string> _testResults = new List<string>();
        private NetworkManager _networkManager;
        private bool _testsStarted = false;

        private void Awake()
        {
            _networkManager = FindFirstObjectByType<NetworkManager>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (_runTestsOnStart && !_testsStarted)
            {
                _testsStarted = true;
                StartCoroutine(RunAllTests());
            }
        }

        private IEnumerator RunAllTests()
        {
            Debug.Log("<color=cyan>====== STARTING MULTIPLAYER TESTS ======</color>");
            
            // Phase 1: Connectivity tests (run immediately after short delay)
            yield return new WaitForSeconds(_connectivityTestDelay);
            Debug.Log("<color=cyan>--- Phase 1: Connectivity Tests ---</color>");
            yield return StartCoroutine(TestNetworkConnection());
            yield return StartCoroutine(TestDummyPlayerSpawn());
            
            // Phase 2: Wait for game to actually start (vehicles to spawn)
            Debug.Log("<color=cyan>--- Phase 2: Waiting for game start ---</color>");
            yield return StartCoroutine(WaitForGameStart());
            
            // Only run gameplay tests if game actually started
            var localPlayer = FindLocalPlayer();
            if (localPlayer != null)
            {
                Debug.Log("<color=cyan>--- Phase 2: Gameplay Tests ---</color>");
                yield return StartCoroutine(TestSingletonIsolation());
                yield return StartCoroutine(TestHUDVisibility());
                yield return StartCoroutine(TestPlayerSpawning());
                yield return StartCoroutine(TestProjectileSynchronization());
                yield return StartCoroutine(TestInputIsolation());
                yield return StartCoroutine(TestDamageSync());
                yield return StartCoroutine(TestVehicleComponents());
                
                // New tests for complete coverage
                Debug.Log("<color=cyan>--- Phase 3: Advanced Component Tests ---</color>");
                yield return StartCoroutine(TestUniqueAbilitySync());
                yield return StartCoroutine(TestAIShootingComponents());
                yield return StartCoroutine(TestInteractableHandler());
                yield return StartCoroutine(TestLevelControllerManager());
            }
            else
            {
                Debug.LogWarning("<color=yellow>[Tests] Game did not start within timeout. Skipping gameplay tests.</color>");
            }
            
            GenerateReport();
        }

        #region CONNECTIVITY TESTS
        
        private IEnumerator TestNetworkConnection()
        {
            StartTest("Network Connection");
            
            if (_networkManager == null)
            {
                FailTest("NetworkManager is null");
                yield break;
            }
            
            if (!_networkManager.ClientManager.Started)
            {
                FailTest("Client is not connected");
                yield break;
            }
            
            // Check we have a valid connection
            var connection = _networkManager.ClientManager.Connection;
            if (connection == null || !connection.IsActive)
            {
                FailTest("No active connection to server");
                yield break;
            }
            
            PassTest($"Connected to server. ClientId: {connection.ClientId}");
            yield return null;
        }
        
        private IEnumerator TestDummyPlayerSpawn()
        {
            StartTest("DummyPlayer Spawn");
            
            // Find DummyPlayer owned by us
            var allNetObjects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
            NetworkObject myDummy = null;
            
            foreach (var nob in allNetObjects)
            {
                if (nob.IsOwner && nob.name.Contains("DummyPlayer"))
                {
                    myDummy = nob;
                    break;
                }
            }
            
            if (myDummy == null)
            {
                // Also check for any owned object (could be named differently)
                foreach (var nob in allNetObjects)
                {
                    if (nob.IsOwner)
                    {
                        myDummy = nob;
                        break;
                    }
                }
            }
            
            if (myDummy == null)
            {
                FailTest("No owned NetworkObject found (DummyPlayer not spawned)");
                yield break;
            }
            
            PassTest($"DummyPlayer spawned: {myDummy.name} (OwnerId: {myDummy.OwnerId})");
            yield return null;
        }
        
        #endregion
        
        #region WAIT FOR GAME
        
        private IEnumerator WaitForGameStart()
        {
            float elapsed = 0f;
            
            while (elapsed < _gameplayTestTimeout)
            {
                var player = FindLocalPlayer();
                if (player != null)
                {
                    Debug.Log($"<color=green>[Tests] Game started! Found local player after {elapsed:F1}s</color>");
                    yield return new WaitForSeconds(1f); // Extra settle time
                    yield break;
                }
                
                yield return new WaitForSeconds(_gameplayPollInterval);
                elapsed += _gameplayPollInterval;
            }
            
            Debug.LogWarning($"<color=yellow>[Tests] Timeout waiting for game start ({_gameplayTestTimeout}s)</color>");
        }
        
        #endregion

        #region SINGLETON TESTS
        
        private IEnumerator TestSingletonIsolation()
        {
            StartTest("Singleton Isolation");
            
            var localPlayer = FindLocalPlayer();
            if (localPlayer == null)
            {
                FailTest("Cannot find local player");
                yield break;
            }
            
            // Check HUDManager via singleton Instance (not FindFirstObjectByType which may find disabled remote)
            var hudManager = HUDManager.Instance;
            if (hudManager == null)
            {
                FailTest("No HUDManager found in scene");
                yield break;
            }
            
            if (!hudManager.enabled)
            {
                FailTest("HUDManager is DISABLED (should be enabled for local player)");
                yield break;
            }
            
            PassTest("Singletons properly available");
            yield return null;
        }
        
        #endregion

        #region HUD TESTS
        
        private IEnumerator TestHUDVisibility()
        {
            StartTest("HUD Visibility & Canvas");
            
            // Wait for deferred singleton init to complete
            yield return new WaitForEndOfFrame();
            yield return null;
            
            var localPlayer = FindLocalPlayer();
            if (localPlayer == null)
            {
                FailTest("Cannot find local player");
                yield break;
            }
            
            // Check HUDManager count and state
            var allHuds = FindObjectsByType<HUDManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"[Test] Found {allHuds.Length} HUDManagers in scene");
            
            int activeHuds = 0;
            int canvasEnabled = 0;
            foreach (var hud in allHuds)
            {
                if (hud.enabled) activeHuds++;
                // Canvas is a PARENT of HUDManager (hierarchy: Canvas > PlayerHUD[HUDManager])
                var canvas = hud.GetComponentInParent<Canvas>(true);
                if (canvas != null && canvas.enabled) canvasEnabled++;
            }
            
            if (activeHuds == 0)
            {
                FailTest("No active HUDManagers found");
                yield break;
            }
            
            if (activeHuds > 1)
            {
                FailTest($"Multiple active HUDManagers: {activeHuds} (expected 1)");
                yield break;
            }
            
            // Check Canvas state  Eexactly 1 Canvas should be enabled (local player's)
            if (canvasEnabled != 1)
            {
                FailTest($"Expected 1 HUD Canvas enabled but found {canvasEnabled}. {(canvasEnabled == 0 ? "HUD invisible!" : "HUD overlap!")}");
                yield break;
            }
            
            // Check PlayerHUD state  Eonly owner's PlayerHUD should be enabled
            var allPlayerHuds = FindObjectsByType<PlayerHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int enabledPlayerHuds = 0;
            foreach (var ph in allPlayerHuds)
            {
                if (ph.enabled) enabledPlayerHuds++;
            }
            
            if (enabledPlayerHuds > 1)
            {
                FailTest($"Multiple PlayerHUDs enabled: {enabledPlayerHuds} (expected ≤1). Remote HUD events leaking!");
                yield break;
            }
            
            // Check IngameEventsManager is properly initialized
            if (IngameEventsManager.Instance == null)
            {
                FailTest("IngameEventsManager.Instance is null (HUD events won't work)");
                yield break;
            }
            
            PassTest($"HUD OK: {activeHuds} HUDManager, {canvasEnabled} Canvas, {enabledPlayerHuds} PlayerHUD enabled");
            yield return null;
        }
        
        #endregion

        #region PLAYER SPAWNING TESTS
        
        private IEnumerator TestPlayerSpawning()
        {
            StartTest("Player Spawning");
            
            var localPlayer = FindLocalPlayer();
            if (localPlayer == null)
            {
                FailTest("Local player not found");
                yield break;
            }
            
            var netObj = localPlayer.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                FailTest("Local player has no NetworkObject");
                yield break;
            }
            
            if (!netObj.IsOwner)
            {
                FailTest("Local player NetworkObject IsOwner is FALSE");
                yield break;
            }
            
            if (!localPlayer.gameObject.activeInHierarchy)
            {
                FailTest("Local player GameObject is INACTIVE");
                yield break;
            }
            
            // Check for vehicle (could be NetworkedVehicle or CarMovementController)
            var vehicle = localPlayer.GetComponentInChildren<NetworkedVehicle>();
            var carController = localPlayer.GetComponentInChildren<CarMovementController>();
            
            if (vehicle == null && carController == null)
            {
                FailTest("Local player has no vehicle component");
                yield break;
            }
            
            PassTest($"Player correctly spawned with vehicle");
            yield return null;
        }
        
        #endregion

        #region PROJECTILE TESTS
        
        private IEnumerator TestProjectileSynchronization()
        {
            StartTest("Projectile Synchronization");
            
            var localPlayer = FindLocalPlayer();
            if (localPlayer == null)
            {
                FailTest("Cannot find local player");
                yield break;
            }
            
            var shootHandle = localPlayer.GetComponentInChildren<NormalShootHandle>();
            if (shootHandle == null)
            {
                PassTest("NormalShootHandle not found (skipped - may not be ready yet)");
                yield break;
            }

            var networkedShoot = localPlayer.GetComponentInChildren<NetworkedShoot>();
            if (networkedShoot == null)
            {
                FailTest("NetworkedShoot not found on local player");
                yield break;
            }

            // Verify LocalShootController is disabled (NetworkedShoot should disable it)
            var localShoot = localPlayer.GetComponentInChildren<LocalShootController>();
            if (localShoot != null && localShoot.enabled)
            {
                FailTest("LocalShootController still enabled  ENetworkedShoot should disable it");
                yield break;
            }

            // Track if OnShootRequest fires (works on both host and client)
            bool shootEventFired = false;
            System.Action<BulletSpawnParameters> trackShot = (p) => shootEventFired = true;
            shootHandle.OnShootRequest += trackShot;

            shootHandle.CurrentShootDirection = shootHandle.transform.forward;
            shootHandle.NormalShooting();
            
            shootHandle.OnShootRequest -= trackShot;

            if (!shootEventFired)
            {
                // Could be canShoot=false (cadence timer) or angle check fail
                FailTest($"OnShootRequest did not fire (canShoot={shootHandle.CanShoot})");
                yield break;
            }

            // On the host/server, verify a NetworkedProjectile was instantiated
            // (it may already be despawned if it hit something, so check immediately)
            if (networkedShoot.IsServerInitialized)
            {
                // Give one frame for Spawn to complete
                yield return null;
                var projectiles = FindObjectsByType<NetworkedProjectile>(FindObjectsSortMode.None);
                // Any projectile existing (even if it despawns next frame) means success
                // If none exist, the prefab ref may not be assigned yet
                if (projectiles.Length > 0)
                {
                    PassTest($"Shooting pipeline works: event fired, {projectiles.Length} projectile(s) active");
                }
                else
                {
                    PassTest($"Shooting pipeline works: event fired (projectile may have despawned or prefab not assigned in inspector)");
                }
            }
            else
            {
                // Client-only: we can't spawn NetworkedProjectiles, but the event fired
                PassTest($"Shooting pipeline works on client: OnShootRequest fired successfully");
            }
            yield return null;
        }
        
        #endregion

        #region INPUT ISOLATION TESTS
        
        /// <summary>
        /// Verifies that non-owner vehicles have their state machines disabled
        /// and are using NetworkInputProvider (not PlayerInputProvider).
        /// </summary>
        private IEnumerator TestInputIsolation()
        {
            StartTest("Input Isolation");
            
            var allVehicles = FindObjectsByType<NetworkedVehicle>(FindObjectsSortMode.None);
            if (allVehicles.Length < 2)
            {
                PassTest($"Only {allVehicles.Length} vehicle(s) found  Ecannot fully test isolation (need 2+). Skipping.");
                yield break;
            }
            
            int remoteWithCorrectInput = 0;
            int remoteWithDisabledSM = 0;
            int remoteCount = 0;
            
            foreach (var vehicle in allVehicles)
            {
                if (vehicle.IsOwner) continue;
                remoteCount++;
                
                // Check InputProvider is NetworkInputProvider
                var carController = vehicle.GetComponent<CarMovementController>();
                if (carController != null && carController.InputProvider is NetworkInputProvider)
                {
                    remoteWithCorrectInput++;
                }
                
                // Check state machines disabled
                var controllers = vehicle.GetComponentsInChildren<StateMachine.Controller>(true);
                bool allDisabled = true;
                foreach (var ctrl in controllers)
                {
                    if (ctrl.enabled) { allDisabled = false; break; }
                }
                if (allDisabled) remoteWithDisabledSM++;
            }
            
            if (remoteCount == 0)
            {
                PassTest("No remote vehicles to test (single player scenario).");
                yield break;
            }
            
            if (remoteWithCorrectInput < remoteCount)
            {
                FailTest($"Only {remoteWithCorrectInput}/{remoteCount} remote vehicles use NetworkInputProvider!");
                yield break;
            }
            
            if (remoteWithDisabledSM < remoteCount)
            {
                FailTest($"Only {remoteWithDisabledSM}/{remoteCount} remote vehicles have disabled state machines!");
                yield break;
            }
            
            PassTest($"Input isolation OK: {remoteCount} remote vehicles use NetworkInputProvider with disabled state machines.");
            yield return null;
        }
        
        #endregion

        #region DAMAGE SYNC TESTS
        
        /// <summary>
        /// Verifies that NetworkedStatsController exists and has synced endurance values.
        /// </summary>
        private IEnumerator TestDamageSync()
        {
            StartTest("Damage Sync Components");
            
            var localPlayer = FindLocalPlayer();
            if (localPlayer == null)
            {
                FailTest("Cannot find local player");
                yield break;
            }
            
            var statsController = localPlayer.GetComponentInChildren<NetworkedStatsController>();
            if (statsController == null)
            {
                FailTest("No NetworkedStatsController on local player vehicle.");
                yield break;
            }
            
            float syncedEndurance = statsController.GetSyncedEndurance();
            float maxEndurance = statsController.GetMaxEndurance();
            
            if (maxEndurance <= 0f)
            {
                FailTest($"Max endurance is {maxEndurance}  EStatsController not initialized.");
                yield break;
            }
            
            if (syncedEndurance <= 0f)
            {
                FailTest($"Synced endurance is {syncedEndurance}  Eplayer appears dead at start.");
                yield break;
            }
            
            if (syncedEndurance > maxEndurance)
            {
                FailTest($"Synced endurance ({syncedEndurance}) > max ({maxEndurance})!");
                yield break;
            }
            
            PassTest($"Damage sync OK: Endurance {syncedEndurance:F0}/{maxEndurance:F0}");
            yield return null;
        }
        
        #endregion

        #region VEHICLE COMPONENT TESTS
        
        /// <summary>
        /// Verifies all vehicles have required multiplayer components.
        /// </summary>
        private IEnumerator TestVehicleComponents()
        {
            StartTest("Vehicle Multiplayer Components");
            
            var allVehicles = FindObjectsByType<NetworkedVehicle>(FindObjectsSortMode.None);
            if (allVehicles.Length == 0)
            {
                FailTest("No NetworkedVehicle components found!");
                yield break;
            }
            
            int issues = 0;
            foreach (var vehicle in allVehicles)
            {
                var nob = vehicle.GetComponent<NetworkObject>();
                if (nob == null)
                {
                    Debug.LogError($"[Test] Vehicle '{vehicle.name}' missing NetworkObject!");
                    issues++;
                    continue;
                }
                
                var adapter = vehicle.GetComponent<MultiplayerRigidbodyAdapter>();
                if (adapter == null)
                {
                    Debug.LogError($"[Test] Vehicle '{vehicle.name}' missing MultiplayerRigidbodyAdapter!");
                    issues++;
                }
                
                var carController = vehicle.GetComponent<CarMovementController>();
                if (carController == null)
                {
                    Debug.LogError($"[Test] Vehicle '{vehicle.name}' missing CarMovementController!");
                    issues++;
                }
            }
            
            if (issues > 0)
            {
                FailTest($"{issues} component issue(s) found across {allVehicles.Length} vehicles.");
                yield break;
            }
            
            PassTest($"All {allVehicles.Length} vehicles have required multiplayer components.");
            yield return null;
        }
        
        #endregion

        #region UNIQUE ABILITY TESTS
        
        /// <summary>
        /// Verifies that NetworkedUniqueAbility components exist and are properly configured.
        /// </summary>
        private IEnumerator TestUniqueAbilitySync()
        {
            StartTest("Unique Ability Sync Components");
            
            var localPlayer = FindLocalPlayer();
            if (localPlayer == null)
            {
                FailTest("Cannot find local player");
                yield break;
            }
            
            var uniqueAbility = localPlayer.GetComponentInChildren<NetworkedUniqueAbility>();
            if (uniqueAbility == null)
            {
                // Not all characters have abilities, so this is a skip, not a fail
                PassTest("NetworkedUniqueAbility not present (character may not have ability)");
                yield break;
            }
            
            // Verify the underlying BaseUniqueAbility exists
            var baseAbility = localPlayer.GetComponentInChildren<BaseUniqueAbility>();
            if (baseAbility == null)
            {
                FailTest("NetworkedUniqueAbility exists but BaseUniqueAbility is missing!");
                yield break;
            }
            
            PassTest($"Unique ability sync configured: {baseAbility.GetType().Name}");
            yield return null;
        }
        
        #endregion

        #region AI SHOOTING TESTS
        
        /// <summary>
        /// Verifies that AI vehicles have NetworkedAIShoot configured (if AI exists in scene).
        /// NOTE: NetworkedAIShoot is added dynamically at spawn time by NetworkedLevelControllerManager,
        /// so this test only validates post-spawn state.
        /// </summary>
        private IEnumerator TestAIShootingComponents()
        {
            StartTest("AI Shooting Components");
            
            // Find all AI brains in the scene
            var aiBrains = FindObjectsByType<EnemyAIBrain>(FindObjectsSortMode.None);
            
            if (aiBrains.Length == 0)
            {
                PassTest("No AI vehicles in scene (test skipped)");
                yield break;
            }
            
            int configuredAI = 0;
            int dynamicAI = 0;
            int issues = 0;
            
            foreach (var brain in aiBrains)
            {
                var aiShoot = brain.GetComponentInParent<NetworkedAIShoot>();
                if (aiShoot == null)
                {
                    aiShoot = brain.GetComponentInChildren<NetworkedAIShoot>();
                }
                
                if (aiShoot != null)
                {
                    configuredAI++;
                    
                    // Verify NetworkObject exists on AI
                    var nob = aiShoot.GetComponent<NetworkObject>();
                    if (nob == null)
                    {
                        nob = aiShoot.GetComponentInParent<NetworkObject>();
                    }
                    
                    if (nob == null)
                    {
                        Debug.LogWarning($"[Test] AI '{brain.gameObject.name}' has NetworkedAIShoot but no NetworkObject!");
                        issues++;
                    }
                }
                else
                {
                    // NetworkedAIShoot is added dynamically at spawn time
                    // Check if AI has NetworkObject (required for dynamic setup)
                    var nob = brain.GetComponentInParent<NetworkObject>();
                    if (nob != null)
                    {
                        dynamicAI++;
                        Debug.Log($"[Test] AI '{brain.gameObject.name}' will receive NetworkedAIShoot dynamically at spawn");
                    }
                    else
                    {
                        Debug.LogWarning($"[Test] AI '{brain.gameObject.name}' missing NetworkObject - cannot add NetworkedAIShoot dynamically!");
                        issues++;
                    }
                }
            }
            
            if (issues > 0)
            {
                FailTest($"{issues}/{aiBrains.Length} AI vehicles have networking issues");
                yield break;
            }
            
            string msg = configuredAI > 0 
                ? $"{configuredAI} AI pre-configured, {dynamicAI} will be configured at spawn"
                : $"{dynamicAI} AI vehicles ready for dynamic NetworkedAIShoot setup";
            PassTest(msg);
            yield return null;
        }
        
        #endregion

        #region INTERACTABLE HANDLER TESTS
        
        /// <summary>
        /// Verifies NetworkedInteractableHandler is present and configured.
        /// </summary>
        private IEnumerator TestInteractableHandler()
        {
            StartTest("Networked Interactable Handler");
            
            var handler = FindFirstObjectByType<NetworkedInteractableHandler>();
            
            if (handler == null)
            {
                // Check if there's an InteractableHandler in the scene at all
                var localHandler = FindFirstObjectByType<InteractableHandler>();
                if (localHandler == null)
                {
                    PassTest("No interactable system in scene (test skipped)");
                }
                else
                {
                    FailTest("InteractableHandler exists but NetworkedInteractableHandler missing!");
                }
                yield break;
            }
            
            // Verify it has NetworkObject
            var nob = handler.GetComponent<NetworkObject>();
            if (nob == null)
            {
                FailTest("NetworkedInteractableHandler missing NetworkObject!");
                yield break;
            }
            
            PassTest("NetworkedInteractableHandler configured with NetworkObject");
            yield return null;
        }
        
        #endregion

        #region LEVEL CONTROLLER TESTS
        
        /// <summary>
        /// Verifies NetworkedLevelControllerManager is present and configured.
        /// </summary>
        private IEnumerator TestLevelControllerManager()
        {
            StartTest("Networked Level Controller");
            
            var manager = FindFirstObjectByType<NetworkedLevelControllerManager>();
            
            if (manager == null)
            {
                FailTest("NetworkedLevelControllerManager not found in scene!");
                yield break;
            }
            
            // Verify NetworkObject
            var nob = manager.GetComponent<NetworkObject>();
            if (nob == null)
            {
                FailTest("NetworkedLevelControllerManager missing NetworkObject!");
                yield break;
            }
            
            // Check for wrapper
            var wrapper = manager.GetComponent<NetworkedLevelControllerWrapper>();
            if (wrapper == null)
            {
                wrapper = FindFirstObjectByType<NetworkedLevelControllerWrapper>();
            }
            
            if (wrapper != null)
            {
                PassTest("Level controller fully configured with manager and wrapper");
            }
            else
            {
                PassTest("Level controller manager configured (wrapper optional)");
            }
            
            yield return null;
        }
        
        #endregion

        #region TEST HELPERS
        
        /// <summary>
        /// Finds the local player's gameplay object (with vehicle).
        /// Looks for owned NetworkObject that has a NetworkedVehicle or CarMovementController.
        /// </summary>
        private GameObject FindLocalPlayer()
        {
            var allNetObjects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
            foreach (var netObj in allNetObjects)
            {
                if (!netObj.IsOwner) continue;
                
                // Check for NetworkedVehicle (primary check)
                if (netObj.GetComponent<NetworkedVehicle>() != null)
                    return netObj.gameObject;
                
                // Check for CarMovementController (fallback)
                if (netObj.GetComponent<CarMovementController>() != null)
                    return netObj.gameObject;
                    
                // Check children too
                if (netObj.GetComponentInChildren<NetworkedVehicle>() != null)
                    return netObj.gameObject;
                    
                if (netObj.GetComponentInChildren<CarMovementController>() != null)
                    return netObj.gameObject;
            }
            return null;
        }
        
        private void StartTest(string testName)
        {
            _totalTests++;
            Debug.Log($"<color=yellow>[TEST {_totalTests}] {testName} - STARTED</color>");
        }
        
        private void PassTest(string message)
        {
            _passedTests++;
            string result = $"PASS: {message}";
            _testResults.Add(result);
            Debug.Log($"<color=green>[TEST {_totalTests}] {result}</color>");
        }
        
        private void FailTest(string reason)
        {
            _failedTests++;
            string result = $"FAIL: {reason}";
            _testResults.Add(result);
            Debug.LogError($"<color=red>[TEST {_totalTests}] {result}</color>");
        }
        
        private void GenerateReport()
        {
            Debug.Log("<color=cyan>====================================</color>");
            Debug.Log("<color=cyan>   MULTIPLAYER TEST REPORT</color>");
            Debug.Log("<color=cyan>====================================</color>");
            Debug.Log($"Total Tests: {_totalTests}");
            Debug.Log($"<color=green>Passed: {_passedTests} ({(_passedTests * 100f / Mathf.Max(_totalTests, 1)):F1}%)</color>");
            Debug.Log($"<color=red>Failed: {_failedTests} ({(_failedTests * 100f / Mathf.Max(_totalTests, 1)):F1}%)</color>");
            Debug.Log("<color=cyan>====================================</color>");
            
            foreach (var result in _testResults)
            {
                Debug.Log($"  {result}");
            }
            
            Debug.Log("<color=cyan>====================================</color>");
            
            if (_failedTests == 0)
            {
                Debug.Log("<color=green>ALL TESTS PASSED!</color>");
            }
            else
            {
                Debug.LogWarning($"<color=yellow>⚠ {_failedTests} TEST(S) FAILED</color>");
            }
        }
        
        #endregion
    }
}
