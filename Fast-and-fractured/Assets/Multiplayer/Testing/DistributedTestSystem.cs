using FishNet.Object;
using FishNet.Managing;
using FishNet.Connection;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FastAndFractured;

namespace FastAndFractured.Multiplayer.Testing
{
    /// <summary>
    /// Distributed Test System for real Host+Client testing using ParrelSync.
    /// 
    /// This system coordinates tests between the main editor (Host) and clone editors (Clients).
    /// Tests are synchronized via FishNet RPCs and results are written to a shared file.
    /// 
    /// Setup:
    /// 1. Main Editor: Set TestRole to HOST
    /// 2. Clone Editor: Set TestRole to CLIENT  
    /// 3. Enter Play Mode on both (Host first)
    /// 4. Tests run automatically and coordinate between instances
    /// </summary>
    public class DistributedTestSystem : NetworkBehaviour
    {
        public enum TestRole
        {
            AUTO_DETECT,  // Uses ParrelSync to detect
            HOST,
            CLIENT
        }

        public enum TestPhase
        {
            WAITING_FOR_PLAYERS,
            CONNECTIVITY,
            VEHICLE_SYNC,
            SHOOTING,
            DAMAGE,
            ABILITIES,
            COMPLETED
        }

        [Header("Test Configuration")]
        [SerializeField] private TestRole _testRole = TestRole.AUTO_DETECT;
        [SerializeField] private int _requiredClients = 1;
        [SerializeField] private float _testTimeout = 60f;
        [SerializeField] private bool _autoStartTests = true;

        public int RequiredClients => Mathf.Max(0, _requiredClients);
        public int ExpectedTotalPlayers => Mathf.Max(1, RequiredClients + 1);

        [Header("Auto Character Selection")]
        [SerializeField] private bool _autoSelectCharacterOnConnect = true;
        [SerializeField] private bool _autoSetReadyOnSelect = false;
        [SerializeField] private float _autoSelectTimeout = 8f;
        [SerializeField] private List<string> _autoCharacterPool = new List<string>
        {
            "Pepe",
            "Josefino",
            "MariaAntonia",
            "Carme"
        };

        [Header("Current State")]
        [SerializeField] private TestPhase _currentPhase = TestPhase.WAITING_FOR_PLAYERS;
        [SerializeField] private int _connectedClients = 0;
        [SerializeField] private bool _isHostInstance = false;

        [Header("Results")]
        [SerializeField] private int _totalTests = 0;
        [SerializeField] private int _passedTests = 0;
        [SerializeField] private int _failedTests = 0;

        private NetworkManager _networkManager;
        private Dictionary<int, bool> _clientTestResults = new Dictionary<int, bool>();
        private List<DistributedTestResult> _allResults = new List<DistributedTestResult>();
        private string _resultsFilePath;
        private bool _testsRunning = false;

        [Serializable]
        public class DistributedTestResult
        {
            public string testName;
            public bool passed;
            public string message;
            public string instance; // "Host" or "Client-X"
            public DateTime timestamp;
        }

        #region INITIALIZATION

        private void Awake()
        {
            _networkManager = FindFirstObjectByType<NetworkManager>();
            _resultsFilePath = Path.Combine(Application.dataPath, "Multiplayer", "Testing", "test_results.json");
            
            // Auto-detect role using ParrelSync
            if (_testRole == TestRole.AUTO_DETECT)
            {
                _isHostInstance = !IsParrelSyncClone();
                _testRole = _isHostInstance ? TestRole.HOST : TestRole.CLIENT;
            }
            else
            {
                _isHostInstance = _testRole == TestRole.HOST;
            }

            Debug.Log($"<color=cyan>[DistributedTest] Role: {_testRole} (IsClone: {IsParrelSyncClone()})</color>");
        }

        private bool IsParrelSyncClone()
        {
#if UNITY_EDITOR
            // Check for ParrelSync clone file
            string cloneFilePath = Path.Combine(Application.dataPath.Replace("/Assets", ""), ".clone");
            return File.Exists(cloneFilePath);
#else
            return false;
#endif
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (_isHostInstance)
            {
                Debug.Log("<color=green>[DistributedTest] Server started - waiting for clients...</color>");
                _networkManager.ServerManager.OnRemoteConnectionState += OnClientConnectionChanged;
                
                // Clear old results
                ClearResultsFile();
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (IsClientInitialized)
            {
                Debug.Log($"<color=cyan>[DistributedTest] Client connected (Owner: {IsOwner})</color>");
                
                if (_autoStartTests)
                {
                    StartCoroutine(WaitAndStartTests());
                }
            }
        }

        private void OnClientConnectionChanged(NetworkConnection conn, FishNet.Transporting.RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Started)
            {
                _connectedClients++;
                Debug.Log($"<color=green>[DistributedTest] Client connected. Total: {_connectedClients}/{_requiredClients}</color>");
                
                if (_connectedClients >= _requiredClients && !_testsRunning && _autoStartTests)
                {
                    StartCoroutine(BeginDistributedTests());
                }
            }
            else if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Stopped)
            {
                _connectedClients = Math.Max(0, _connectedClients - 1);
            }
        }

        #endregion

        #region TEST COORDINATION

        private IEnumerator WaitAndStartTests()
        {
            // Wait for connection to stabilize
            yield return new WaitForSeconds(2f);

            if (_autoSelectCharacterOnConnect && IsClientInitialized)
            {
                yield return StartCoroutine(AutoSelectCharacterForLocalClient());
            }
            
            // Client reports ready to host
            if (!_isHostInstance && IsClientInitialized)
            {
                ReportClientReadyServerRpc(LocalConnection.ClientId);
            }
        }

        private IEnumerator AutoSelectCharacterForLocalClient()
        {
            float timeout = Mathf.Max(1f, _autoSelectTimeout);
            const float retryDelay = 0.25f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                if (!IsClientInitialized || LocalConnection == null || !LocalConnection.IsValid)
                {
                    yield return new WaitForSeconds(retryDelay);
                    elapsed += retryDelay;
                    continue;
                }

                var lobbyManager = FindFirstObjectByType<NetworkedLobbyManager>();
                if (lobbyManager == null)
                {
                    yield return new WaitForSeconds(retryDelay);
                    elapsed += retryDelay;
                    continue;
                }

                int localClientId = LocalConnection.ClientId;
                if (lobbyManager.PlayerCharacterSelections.TryGetValue(localClientId, out string existingSelection) &&
                    !string.IsNullOrEmpty(existingSelection))
                {
                    if (_autoSetReadyOnSelect)
                        lobbyManager.ServerSetReady(true);

                    Debug.Log($"<color=grey>[DistributedTest] Client {localClientId} already has character '{existingSelection}'.</color>");
                    yield break;
                }

                string characterCode = ResolveAutoCharacterCode(localClientId);
                if (string.IsNullOrEmpty(characterCode))
                {
                    Debug.LogWarning("[DistributedTest] Auto character pool is empty or invalid.");
                    yield break;
                }

                lobbyManager.ServerSelectCharacter(characterCode);
                if (_autoSetReadyOnSelect)
                    lobbyManager.ServerSetReady(true);

                Debug.Log($"<color=cyan>[DistributedTest] Auto-selected '{characterCode}' for client {localClientId}</color>");
                yield break;
            }

            Debug.LogWarning("[DistributedTest] Timed out while trying to auto-select character.");
        }

        private string ResolveAutoCharacterCode(int clientId)
        {
            if (_autoCharacterPool == null || _autoCharacterPool.Count == 0)
                return string.Empty;

            int characterIndex = Mathf.Abs(clientId) % _autoCharacterPool.Count;
            string raw = _autoCharacterPool[characterIndex];
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            return NormalizeCharacterCode(raw);
        }

        private static string NormalizeCharacterCode(string characterNameOrCode)
        {
            if (string.IsNullOrWhiteSpace(characterNameOrCode))
                return string.Empty;

            string trimmed = characterNameOrCode.Trim();
            int separatorIndex = trimmed.LastIndexOf('_');
            if (separatorIndex > 0 &&
                separatorIndex < trimmed.Length - 1 &&
                int.TryParse(trimmed.Substring(separatorIndex + 1), out _))
            {
                return trimmed;
            }

            return $"{trimmed}_0";
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportClientReadyServerRpc(int clientId)
        {
            Debug.Log($"<color=green>[DistributedTest] Client {clientId} reported ready</color>");
            
            if (!_clientTestResults.ContainsKey(clientId))
            {
                _clientTestResults[clientId] = false;
            }
        }

        private IEnumerator BeginDistributedTests()
        {
            if (_testsRunning) yield break;
            _testsRunning = true;

            Debug.Log("<color=cyan>====== STARTING DISTRIBUTED MULTIPLAYER TESTS ======</color>");
            WriteToResultsFile("=== Distributed Test Session Started ===\n");

            // Phase 1: Connectivity
            _currentPhase = TestPhase.CONNECTIVITY;
            NotifyPhaseChangeObserversRpc(TestPhase.CONNECTIVITY);
            yield return StartCoroutine(RunConnectivityTests());

            // Wait for vehicles to spawn
            yield return new WaitForSeconds(3f);

            // Phase 2: Vehicle Sync
            _currentPhase = TestPhase.VEHICLE_SYNC;
            NotifyPhaseChangeObserversRpc(TestPhase.VEHICLE_SYNC);
            yield return StartCoroutine(RunVehicleSyncTests());

            // Phase 3: Shooting
            _currentPhase = TestPhase.SHOOTING;
            NotifyPhaseChangeObserversRpc(TestPhase.SHOOTING);
            yield return StartCoroutine(RunShootingTests());

            // Phase 4: Damage
            _currentPhase = TestPhase.DAMAGE;
            NotifyPhaseChangeObserversRpc(TestPhase.DAMAGE);
            yield return StartCoroutine(RunDamageTests());

            // Phase 5: Abilities
            _currentPhase = TestPhase.ABILITIES;
            NotifyPhaseChangeObserversRpc(TestPhase.ABILITIES);
            yield return StartCoroutine(RunAbilityTests());

            // Complete
            _currentPhase = TestPhase.COMPLETED;
            NotifyPhaseChangeObserversRpc(TestPhase.COMPLETED);
            
            GenerateFinalReport();
        }

        [ObserversRpc]
        private void NotifyPhaseChangeObserversRpc(TestPhase phase)
        {
            _currentPhase = phase;
            Debug.Log($"<color=yellow>[DistributedTest] Phase changed to: {phase}</color>");
        }

        #endregion

        #region CONNECTIVITY TESTS

        private IEnumerator RunConnectivityTests()
        {
            Debug.Log("<color=cyan>--- Running Connectivity Tests ---</color>");

            // Test 1: Server is running
            RecordTest("Server Active", IsServerInitialized, 
                IsServerInitialized ? "Server is running" : "Server not initialized");

            // Test 2: Client count
            int clientCount = _networkManager.ServerManager.Clients.Count;
            RecordTest("Client Count", clientCount >= _requiredClients, 
                $"Connected clients: {clientCount} (expected >= {_requiredClients})");

            // Test 3: NetworkObjects spawned
            var networkObjects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
            RecordTest("NetworkObjects Present", networkObjects.Length > 0, 
                $"Found {networkObjects.Length} NetworkObjects");

            // Request client-side connectivity validation
            RequestClientConnectivityTestObserversRpc();
            yield return new WaitForSeconds(2f);
        }

        [ObserversRpc]
        private void RequestClientConnectivityTestObserversRpc()
        {
            if (_isHostInstance) return;

            bool isConnected = IsClientInitialized && _networkManager.ClientManager.Connection != null;
            bool hasLocalConnection = LocalConnection != null && LocalConnection.IsValid;
            
            ReportClientTestResultServerRpc(
                LocalConnection?.ClientId ?? -1,
                "Client Connectivity",
                isConnected && hasLocalConnection,
                $"Connected: {isConnected}, ValidConnection: {hasLocalConnection}"
            );
        }

        #endregion

        #region VEHICLE SYNC TESTS

        private IEnumerator RunVehicleSyncTests()
        {
            Debug.Log("<color=cyan>--- Running Vehicle Sync Tests ---</color>");

            // Find all networked vehicles
            var vehicles = FindObjectsByType<NetworkedVehicle>(FindObjectsSortMode.None);
            RecordTest("NetworkedVehicles Found", vehicles.Length > 0, 
                $"Found {vehicles.Length} NetworkedVehicle components");

            if (vehicles.Length > 0)
            {
                // Test ownership
                var ownedVehicles = vehicles.Where(v => v.IsOwner).ToArray();
                RecordTest("Vehicle Ownership", ownedVehicles.Length >= 1, 
                    $"Owned vehicles: {ownedVehicles.Length}");

                // Test components
                foreach (var vehicle in ownedVehicles.Take(1))
                {
                    var rigidbody = vehicle.GetComponent<Rigidbody>();
                    RecordTest("Vehicle Rigidbody", rigidbody != null, 
                        rigidbody != null ? "Rigidbody present" : "Missing Rigidbody");

                    var networkedShoot = vehicle.GetComponent<NetworkedShoot>();
                    RecordTest("NetworkedShoot Component", networkedShoot != null, 
                        networkedShoot != null ? "NetworkedShoot present" : "Missing NetworkedShoot");
                }
            }

            // Request client vehicle validation
            RequestClientVehicleTestObserversRpc();
            yield return new WaitForSeconds(2f);
        }

        [ObserversRpc]
        private void RequestClientVehicleTestObserversRpc()
        {
            if (_isHostInstance) return;

            var vehicles = FindObjectsByType<NetworkedVehicle>(FindObjectsSortMode.None);
            var myVehicle = vehicles.FirstOrDefault(v => v.IsOwner);
            
            ReportClientTestResultServerRpc(
                LocalConnection?.ClientId ?? -1,
                "Client Vehicle Ownership",
                myVehicle != null,
                myVehicle != null ? $"Client owns vehicle: {myVehicle.gameObject.name}" : "No owned vehicle found"
            );

            // Test that we can see other vehicles (non-owned)
            var otherVehicles = vehicles.Where(v => !v.IsOwner).ToArray();
            ReportClientTestResultServerRpc(
                LocalConnection?.ClientId ?? -1,
                "Client Sees Other Vehicles",
                otherVehicles.Length > 0,
                $"Can see {otherVehicles.Length} other vehicles"
            );
        }

        #endregion

        #region SHOOTING TESTS

        private IEnumerator RunShootingTests()
        {
            Debug.Log("<color=cyan>--- Running Shooting Tests ---</color>");

            var shoots = FindObjectsByType<NetworkedShoot>(FindObjectsSortMode.None);
            RecordTest("NetworkedShoot Components", shoots.Length > 0, 
                $"Found {shoots.Length} NetworkedShoot components");

            // Test projectile spawning (server-side)
            if (IsServerInitialized && shoots.Length > 0)
            {
                var hostShoot = shoots.FirstOrDefault(s => s.IsOwner);
                if (hostShoot != null)
                {
                    var normalHandle = hostShoot.GetComponentInChildren<NormalShootHandle>(true);
                    if (normalHandle == null)
                    {
                        RecordTest("Projectile Spawning", false,
                            "Host owner NetworkedShoot is missing NormalShootHandle.");
                        yield return new WaitForSeconds(1f);
                        yield break;
                    }

                    int projectilesBefore = FindObjectsByType<NetworkedProjectile>(FindObjectsSortMode.None).Length;

                    bool shootEventFired = false;
                    System.Action<BulletSpawnParameters> trackShot = (_) => shootEventFired = true;
                    normalHandle.OnShootRequest += trackShot;
                    normalHandle.CurrentShootDirection = hostShoot.transform.forward;
                    normalHandle.NormalShooting();
                    normalHandle.OnShootRequest -= trackShot;

                    if (!shootEventFired)
                    {
                        RecordTest("Projectile Spawning", false,
                            $"Host shoot request did not fire (canShoot={normalHandle.CanShoot}).");
                        yield return new WaitForSeconds(1f);
                        yield break;
                    }

                    // Give spawn and first simulation tick a moment to run.
                    yield return null;
                    yield return new WaitForSeconds(0.25f);
                     
                    int projectilesAfter = FindObjectsByType<NetworkedProjectile>(FindObjectsSortMode.None).Length;
                    RecordTest("Projectile Spawning", projectilesAfter > projectilesBefore || projectilesAfter > 0, 
                        $"Projectiles before: {projectilesBefore}, after: {projectilesAfter}");
                }
            }

            yield return new WaitForSeconds(1f);
        }

        [ObserversRpc]
        private void TriggerTestShotObserversRpc()
        {
            // Each client with ownership should fire a test shot
            var myShoot = FindObjectsByType<NetworkedShoot>(FindObjectsSortMode.None)
                .FirstOrDefault(s => s.IsOwner);
                
            if (myShoot != null)
            {
                Debug.Log($"<color=yellow>[DistributedTest] Triggering test shot from {(_isHostInstance ? "Host" : "Client")}</color>");
                // The actual shooting is handled by NetworkedShoot's normal input system
                // We just verify the component exists and is properly configured
            }
        }

        #endregion

        #region DAMAGE TESTS

        private IEnumerator RunDamageTests()
        {
            Debug.Log("<color=cyan>--- Running Damage Tests ---</color>");

            var statsControllers = FindObjectsByType<NetworkedStatsController>(FindObjectsSortMode.None);
            RecordTest("NetworkedStatsController Present", statsControllers.Length > 0, 
                $"Found {statsControllers.Length} NetworkedStatsController components");

            if (statsControllers.Length > 0)
            {
                var controller = statsControllers.First();
                float health = controller.GetSyncedEndurance();
                RecordTest("Health Sync Available", health > 0, 
                    $"Current synced health: {health}");
            }

            // Request client damage validation
            RequestClientDamageTestObserversRpc();
            yield return new WaitForSeconds(2f);
        }

        [ObserversRpc]
        private void RequestClientDamageTestObserversRpc()
        {
            if (_isHostInstance) return;

            var statsControllers = FindObjectsByType<NetworkedStatsController>(FindObjectsSortMode.None);
            var myStats = statsControllers.FirstOrDefault(s => s.IsOwner);
            
            ReportClientTestResultServerRpc(
                LocalConnection?.ClientId ?? -1,
                "Client Health Sync",
                myStats != null && myStats.GetSyncedEndurance() > 0,
                myStats != null ? $"Client health: {myStats.GetSyncedEndurance()}" : "No NetworkedStatsController found"
            );
        }

        #endregion

        #region ABILITY TESTS

        private IEnumerator RunAbilityTests()
        {
            Debug.Log("<color=cyan>--- Running Ability Tests ---</color>");

            var abilities = FindObjectsByType<NetworkedUniqueAbility>(FindObjectsSortMode.None);
            RecordTest("NetworkedUniqueAbility Present", abilities.Length > 0, 
                $"Found {abilities.Length} NetworkedUniqueAbility components");

            if (abilities.Length > 0)
            {
                foreach (var ability in abilities.Take(2))
                {
                    bool hasWrapped = ability.GetComponent<BaseUniqueAbility>() != null;
                    RecordTest($"Ability Wrapper ({ability.gameObject.name})", hasWrapped, 
                        hasWrapped ? "BaseUniqueAbility found" : "Missing wrapped ability");
                }
            }

            yield return new WaitForSeconds(1f);
        }

        #endregion

        #region CLIENT RESULT REPORTING

        [ServerRpc(RequireOwnership = false)]
        private void ReportClientTestResultServerRpc(int clientId, string testName, bool passed, string message)
        {
            string instanceName = $"Client-{clientId}";
            
            var result = new DistributedTestResult
            {
                testName = testName,
                passed = passed,
                message = message,
                instance = instanceName,
                timestamp = DateTime.Now
            };

            _allResults.Add(result);
            _totalTests++;
            if (passed) _passedTests++;
            else _failedTests++;

            string color = passed ? "green" : "red";
            Debug.Log($"<color={color}>[{instanceName}] {testName}: {(passed ? "PASS" : "FAIL")} - {message}</color>");
            
            WriteToResultsFile($"[{instanceName}] {testName}: {(passed ? "PASS" : "FAIL")} - {message}\n");
        }

        #endregion

        #region RESULT RECORDING

        private void RecordTest(string testName, bool passed, string message)
        {
            string instanceName = _isHostInstance ? "Host" : $"Client-{LocalConnection?.ClientId ?? 0}";
            
            var result = new DistributedTestResult
            {
                testName = testName,
                passed = passed,
                message = message,
                instance = instanceName,
                timestamp = DateTime.Now
            };

            _allResults.Add(result);
            _totalTests++;
            if (passed) _passedTests++;
            else _failedTests++;

            string color = passed ? "green" : "red";
            Debug.Log($"<color={color}>[{instanceName}] {testName}: {(passed ? "PASS" : "FAIL")} - {message}</color>");
            
            WriteToResultsFile($"[{instanceName}] {testName}: {(passed ? "PASS" : "FAIL")} - {message}\n");
        }

        private void WriteToResultsFile(string content)
        {
            try
            {
                File.AppendAllText(_resultsFilePath, content);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DistributedTest] Could not write to results file: {e.Message}");
            }
        }

        private void ClearResultsFile()
        {
            try
            {
                if (File.Exists(_resultsFilePath))
                {
                    File.Delete(_resultsFilePath);
                }
                File.WriteAllText(_resultsFilePath, $"=== Test Results - {DateTime.Now} ===\n\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DistributedTest] Could not clear results file: {e.Message}");
            }
        }

        private void GenerateFinalReport()
        {
            string report = $@"
====================================
  DISTRIBUTED TEST RESULTS SUMMARY
====================================
Total Tests: {_totalTests}
Passed: {_passedTests}
Failed: {_failedTests}
Success Rate: {(_totalTests > 0 ? (_passedTests * 100f / _totalTests) : 0):F1}%

Detailed Results:
";
            foreach (var result in _allResults)
            {
                report += $"  [{result.instance}] {result.testName}: {(result.passed ? "PASS" : "FAIL")}\n";
                if (!result.passed)
                {
                    report += $"    -> {result.message}\n";
                }
            }

            report += "====================================\n";

            Debug.Log($"<color=cyan>{report}</color>");
            WriteToResultsFile(report);

            // Notify all clients of completion
            NotifyTestsCompleteObserversRpc(_totalTests, _passedTests, _failedTests);
        }

        [ObserversRpc]
        private void NotifyTestsCompleteObserversRpc(int total, int passed, int failed)
        {
            Debug.Log($"<color=cyan>====== DISTRIBUTED TESTS COMPLETE ======</color>");
            Debug.Log($"<color=cyan>Total: {total} | Passed: {passed} | Failed: {failed}</color>");
            
            if (failed == 0)
            {
                Debug.Log("<color=green>ALL TESTS PASSED!</color>");
            }
            else
            {
                Debug.Log($"<color=red>{failed} TESTS FAILED - Check test_results.json for details</color>");
            }
        }

        #endregion

        #region MANUAL TEST TRIGGERS

        /// <summary>
        /// Manually start tests (call from Inspector or code)
        /// </summary>
        [ContextMenu("Start Distributed Tests")]
        public void ManualStartTests()
        {
            if (IsServerInitialized && !_testsRunning)
            {
                StartCoroutine(BeginDistributedTests());
            }
            else
            {
                Debug.LogWarning("[DistributedTest] Cannot start tests - either not server or tests already running");
            }
        }

        /// <summary>
        /// View current results
        /// </summary>
        [ContextMenu("View Results")]
        public void ViewResults()
        {
            if (File.Exists(_resultsFilePath))
            {
                string content = File.ReadAllText(_resultsFilePath);
                Debug.Log(content);
            }
            else
            {
                Debug.Log("No results file found yet.");
            }
        }

        #endregion
    }
}
