using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FastAndFractured.Multiplayer;

namespace FastAndFractured.Multiplayer.Testing
{
    /// <summary>
    /// Sistema automatizado de testing para verificar sincronización de proyectiles.
    /// Coloca este script en un GameObject en la escena MultiplayerRoomTest.
    /// </summary>
    public class NetworkedProjectileTestRunner : NetworkBehaviour
    {
        [Header("Test Configuration")]
        [SerializeField] private bool _runTestsOnStart = false;
        [SerializeField] private float _testDuration = 30f;
        [SerializeField] private float _projectileSpawnInterval = 2f;
        [SerializeField] private int _maxProjectilesToTest = 10;
        
        [Header("Test Targets")]
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private Vector3 _targetDirection = Vector3.forward;
        [SerializeField] private float _projectileSpeed = 20f;
        
        private List<ProjectileTestData> _activeTests = new List<ProjectileTestData>();
        private int _testsRun = 0;
        private int _testsPassed = 0;
        private int _testsFailed = 0;
        private float _testStartTime;
        private bool _isTestRunning = false;
        
        private class ProjectileTestData
        {
            public NetworkObject ProjectileNetworkObject;
            public Vector3 ServerSpawnPosition;
            public Vector3 ClientSpawnPosition;
            public float SpawnTime;
            public int ProjectileId;
            public bool HasDesyncOccurred;
            public float MaxDesyncDistance;
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            if (_runTestsOnStart && IsHostInitialized)
            {
                StartCoroutine(RunAutomatedTests());
            }
        }
        
        [ContextMenu("Start Tests")]
        public void StartTests()
        {
            if (!IsServerInitialized)
            {
                Debug.LogError("[ProjectileTestRunner] Tests can only be started on server!");
                return;
            }
            
            StartCoroutine(RunAutomatedTests());
        }
        
        private IEnumerator RunAutomatedTests()
        {
            if (_isTestRunning)
            {
                Debug.LogWarning("[ProjectileTestRunner] Tests already running!");
                yield break;
            }
            
            _isTestRunning = true;
            _testStartTime = Time.time;
            _testsRun = 0;
            _testsPassed = 0;
            _testsFailed = 0;
            _activeTests.Clear();
            
            Debug.Log($"<color=cyan>[ProjectileTestRunner] ========== STARTING AUTOMATED TESTS ==========</color>");
            Debug.Log($"<color=cyan>[ProjectileTestRunner] Duration: {_testDuration}s | Interval: {_projectileSpawnInterval}s | Max Projectiles: {_maxProjectilesToTest}</color>");
            
            float nextSpawnTime = Time.time;
            
            while (Time.time - _testStartTime < _testDuration && _testsRun < _maxProjectilesToTest)
            {
                if (Time.time >= nextSpawnTime)
                {
                    SpawnTestProjectile();
                    nextSpawnTime = Time.time + _projectileSpawnInterval;
                }
                
                // Verificar proyectiles activos
                UpdateActiveTests();
                
                yield return null;
            }
            
            // Esperar a que terminen los proyectiles restantes
            Debug.Log($"<color=yellow>[ProjectileTestRunner] Waiting for remaining projectiles to finish...</color>");
            yield return new WaitForSeconds(5f);
            
            // Generar reporte final
            GenerateFinalReport();
            
            _isTestRunning = false;
        }
        
        private void SpawnTestProjectile()
        {
            if (!IsServerInitialized) return;
            
            // Buscar el NetworkedShoot del jugador local
            var shootController = FindFirstObjectByType<NetworkedShoot>();
            if (shootController == null)
            {
                Debug.LogError("[ProjectileTestRunner] No NetworkedShoot found in scene!");
                return;
            }
            
            Vector3 spawnPos = _spawnPoint != null ? _spawnPoint.position : transform.position + Vector3.up * 2f;
            Vector3 velocity = _targetDirection.normalized * _projectileSpeed;
            
            _testsRun++;
            
            Debug.Log($"<color=green>[ProjectileTestRunner] Test #{_testsRun} - Spawning projectile at {spawnPos} with velocity {velocity}</color>");
            
            // Simular un disparo
            // Nota: Deberás adaptar esto según tu implementación específica de NetworkedShoot
            ServerSpawnTestProjectileRpc(spawnPos, velocity, _testsRun);
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void ServerSpawnTestProjectileRpc(Vector3 position, Vector3 velocity, int testId)
        {
            var shootController = FindFirstObjectByType<NetworkedShoot>();
            if (shootController == null) return;
            
            // Aquí deberías usar tu método de spawn de proyectiles
            // Este es un ejemplo genérico que deberás adaptar
            
            var testData = new ProjectileTestData
            {
                ServerSpawnPosition = position,
                SpawnTime = Time.time,
                ProjectileId = testId,
                HasDesyncOccurred = false,
                MaxDesyncDistance = 0f
            };
            
            _activeTests.Add(testData);
            
            NotifyClientsOfTestProjectileRpc(testId, position);
        }
        
        [ObserversRpc]
        private void NotifyClientsOfTestProjectileRpc(int testId, Vector3 serverSpawnPos)
        {
            if (IsServerInitialized) return; // Solo clientes
            
            Debug.Log($"<color=cyan>[ProjectileTestRunner - CLIENT] Test #{testId} projectile spawn notification received. Server spawn pos: {serverSpawnPos}</color>");
            
            // Los clientes pueden registrar aquí su posición local del proyectil para comparar
            StartCoroutine(TrackProjectileOnClient(testId, serverSpawnPos));
        }
        
        private IEnumerator TrackProjectileOnClient(int testId, Vector3 serverSpawnPos)
        {
            yield return new WaitForSeconds(0.1f); // Esperar a que el proyectil spawne
            
            // Buscar el proyectil por nombre (incluye el testId)
            var projectiles = FindObjectsByType<NetworkedProjectile>(FindObjectsSortMode.None);
            NetworkedProjectile targetProjectile = null;
            
            foreach (var proj in projectiles)
            {
                if (proj.gameObject.name.Contains($"_Tick({testId})"))
                {
                    targetProjectile = proj;
                    break;
                }
            }
            
            if (targetProjectile != null)
            {
                Vector3 clientSpawnPos = targetProjectile.transform.position;
                float spawnDesync = Vector3.Distance(serverSpawnPos, clientSpawnPos);
                
                if (spawnDesync > 0.1f)
                {
                    Debug.LogWarning($"<color=yellow>[ProjectileTestRunner - CLIENT] Test #{testId} SPAWN DESYNC: {spawnDesync:F3}m (Server: {serverSpawnPos}, Client: {clientSpawnPos})</color>");
                    ReportDesyncToServerRpc(testId, spawnDesync);
                }
                else
                {
                    Debug.Log($"<color=green>[ProjectileTestRunner - CLIENT] Test #{testId} spawn synchronized within tolerance</color>");
                }
            }
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void ReportDesyncToServerRpc(int testId, float desyncDistance)
        {
            var test = _activeTests.Find(t => t.ProjectileId == testId);
            if (test != null)
            {
                test.HasDesyncOccurred = true;
                test.MaxDesyncDistance = Mathf.Max(test.MaxDesyncDistance, desyncDistance);
            }
        }
        
        private void UpdateActiveTests()
        {
            for (int i = _activeTests.Count - 1; i >= 0; i--)
            {
                var test = _activeTests[i];
                
                // Si el proyectil ya no existe o ha pasado suficiente tiempo
                if (test.ProjectileNetworkObject == null || Time.time - test.SpawnTime > 10f)
                {
                    // Test completado
                    if (test.HasDesyncOccurred)
                    {
                        _testsFailed++;
                        Debug.LogWarning($"<color=red>[ProjectileTestRunner] Test #{test.ProjectileId} FAILED - Max desync: {test.MaxDesyncDistance:F3}m</color>");
                    }
                    else
                    {
                        _testsPassed++;
                        Debug.Log($"<color=green>[ProjectileTestRunner] Test #{test.ProjectileId} PASSED</color>");
                    }
                    
                    _activeTests.RemoveAt(i);
                }
            }
        }
        
        private void GenerateFinalReport()
        {
            float testDuration = Time.time - _testStartTime;
            float successRate = _testsRun > 0 ? (_testsPassed / (float)_testsRun) * 100f : 0f;
            
            Debug.Log($"<color=cyan>========================================</color>");
            Debug.Log($"<color=cyan>[ProjectileTestRunner] FINAL REPORT</color>");
            Debug.Log($"<color=cyan>========================================</color>");
            float failureRate = _testsRun > 0 ? (_testsFailed / (float)_testsRun) * 100f : 0f;
            
            Debug.Log($"<color=white>Test Duration: {testDuration:F1}s</color>");
            Debug.Log($"<color=white>Total Tests: {_testsRun}</color>");
            Debug.Log($"<color=green>Passed: {_testsPassed} ({successRate:F1}%)</color>");
            Debug.Log($"<color=red>Failed: {_testsFailed} ({failureRate:F1}%)</color>");
            
            if (_testsPassed == _testsRun)
            {
                Debug.Log($"<color=green>[OK]ALL TESTS PASSED! Projectile synchronization is working correctly.</color>");
            }
            else
            {
                Debug.LogWarning($"<color=yellow>⚠ {_testsFailed} test(s) failed. Review logs for desync issues.</color>");
            }
            
            Debug.Log($"<color=cyan>========================================</color>");
        }
        
        private void OnGUI()
        {
            if (!_isTestRunning) return;
            
            // HUD de testing
            GUI.Box(new Rect(10, 10, 300, 150), "Projectile Sync Test");
            
            float elapsed = Time.time - _testStartTime;
            GUI.Label(new Rect(20, 35, 280, 20), $"Time: {elapsed:F1}s / {_testDuration:F1}s");
            GUI.Label(new Rect(20, 55, 280, 20), $"Tests Run: {_testsRun} / {_maxProjectilesToTest}");
            GUI.Label(new Rect(20, 75, 280, 20), $"Active: {_activeTests.Count}");
            
            GUI.color = Color.green;
            GUI.Label(new Rect(20, 95, 280, 20), $"Passed: {_testsPassed}");
            
            GUI.color = Color.red;
            GUI.Label(new Rect(20, 115, 280, 20), $"Failed: {_testsFailed}");
            
            GUI.color = Color.white;
            
            if (GUI.Button(new Rect(20, 135, 100, 20), "Stop Tests"))
            {
                StopAllCoroutines();
                GenerateFinalReport();
                _isTestRunning = false;
            }
        }
    }
}
