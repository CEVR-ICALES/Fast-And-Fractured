using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using FishNet.Managing;
using FishNet.Object;
using FastAndFractured.Multiplayer;
using FastAndFractured;
using System.Collections.Generic;
using Utilities;

namespace FastAndFractured.Multiplayer.Editor
{
    public class MultiplayerSceneValidator : EditorWindow
    {
        private const float BUTTON_HEIGHT = 30f;

        private Vector2 _scrollPosition;
        private List<ValidationResult> _results = new List<ValidationResult>();
        private bool _hasRun = false;

        private struct ValidationResult
        {
            public string Category;
            public string Message;
            public MessageType Severity;
        }

        [MenuItem("Tools/Fast And Fractured/Diagnostics/Validate Multiplayer Scene", priority = 30)]
        public static void ShowWindow()
        {
            var window = GetWindow<MultiplayerSceneValidator>("Scene Validator");
            window.RunValidation();
        }

        private void OnGUI()
        {
            GUILayout.Label("Multiplayer Scene Validator", EditorStyles.boldLabel);

            if (GUILayout.Button("Run Validation", GUILayout.Height(BUTTON_HEIGHT)))
            {
                RunValidation();
            }

            if (!_hasRun) return;

            int errors = 0, warnings = 0, info = 0;
            foreach (var r in _results)
            {
                if (r.Severity == MessageType.Error) errors++;
                else if (r.Severity == MessageType.Warning) warnings++;
                else info++;
            }

            EditorGUILayout.Space(5);
            var color = errors > 0 ? "red" : (warnings > 0 ? "yellow" : "green");
            EditorGUILayout.LabelField($"Results: {errors} errors, {warnings} warnings, {info} info");

            EditorGUILayout.Space(5);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            string lastCategory = "";
            foreach (var result in _results)
            {
                if (result.Category != lastCategory)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField(result.Category, EditorStyles.boldLabel);
                    lastCategory = result.Category;
                }
                EditorGUILayout.HelpBox(result.Message, result.Severity);
            }

            EditorGUILayout.EndScrollView();
        }

        private void RunValidation()
        {
            _results.Clear();
            _hasRun = true;

            ValidateNetworkManager();
            ValidateLevelController();
            ValidateSpawnableVehicles();
            ValidateSpawnableBullets();
            ValidateSceneObjects();

            Repaint();
        }

        private void ValidateNetworkManager()
        {
            string cat = "Network Manager";
            var nm = Object.FindFirstObjectByType<NetworkManager>();
            if (nm == null)
            {
                AddResult(cat, "No NetworkManager found in scene!", MessageType.Error);
                return;
            }
            AddResult(cat, "NetworkManager found.", MessageType.Info);

            if (nm.TransportManager == null || nm.TransportManager.Transport == null)
            {
                AddResult(cat, "NetworkManager has no transport configured!", MessageType.Error);
            }
            else
            {
                AddResult(cat, $"Transport: {nm.TransportManager.Transport.GetType().Name}", MessageType.Info);
            }

            var spawnables = nm.SpawnablePrefabs;
            if (spawnables == null)
            {
                AddResult(cat, "No SpawnablePrefabs collection configured!", MessageType.Error);
            }
        }

        private void ValidateLevelController()
        {
            string cat = "Level Controller";

            var levelManager = Object.FindFirstObjectByType<NetworkedLevelControllerManager>();
            if (levelManager == null)
            {
                AddResult(cat, "No NetworkedLevelControllerManager found in scene!", MessageType.Error);
                return;
            }
            AddResult(cat, "NetworkedLevelControllerManager found.", MessageType.Info);

            var nob = levelManager.GetComponent<NetworkObject>();
            if (nob == null)
            {
                AddResult(cat, "NetworkedLevelControllerManager is missing NetworkObject!", MessageType.Error);
            }

            var wrapper = levelManager.GetComponent<NetworkedLevelControllerWrapper>();
            if (wrapper == null)
            {
                AddResult(cat, "NetworkedLevelControllerWrapper is missing!", MessageType.Error);
            }

            var levelController = Object.FindFirstObjectByType<LevelController>(FindObjectsInactive.Include);
            if (levelController == null)
            {
                AddResult(cat, "LevelController not found in scene!", MessageType.Error);
            }
            else
            {
                AddResult(cat, "LevelController found.", MessageType.Info);
            }
        }

        private void ValidateSpawnableVehicles()
        {
            string cat = "Vehicle Prefabs";

            string[] guids = AssetDatabase.FindAssets("t:GameObject Networked", new[] { "Assets/Multiplayer" });
            int vehicleCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                var vehicle = go.GetComponent<NetworkedVehicle>();
                if (vehicle == null) continue;

                vehicleCount++;
                var vehicleNob = go.GetComponent<NetworkObject>();
                var shoot = go.GetComponent<NetworkedShoot>();
                var stats = go.GetComponent<NetworkedStatsController>();
                var adapter = go.GetComponent<MultiplayerRigidbodyAdapter>();

                string issues = "";
                if (vehicleNob == null) issues += "Missing NetworkObject! ";
                if (adapter == null) issues += "Missing MultiplayerRigidbodyAdapter! ";
                if (shoot == null) issues += "No NetworkedShoot. ";
                if (stats == null) issues += "No NetworkedStatsController. ";

                if (!string.IsNullOrEmpty(issues))
                {
                    AddResult(cat, $"{go.name}: {issues}", MessageType.Warning);
                }
                else
                {
                    var so = new SerializedObject(shoot);
                    bool normalSet = so.FindProperty("_networkedNormalBulletPrefab")?.objectReferenceValue != null;
                    bool pushSet = so.FindProperty("_networkedPushBulletPrefab")?.objectReferenceValue != null;

                    if (!normalSet || !pushSet)
                    {
                        AddResult(cat, $"{go.name}: Missing bullet prefab assignments on NetworkedShoot.", MessageType.Warning);
                    }
                    else
                    {
                        AddResult(cat, $"{go.name}: [OK]Fully configured.", MessageType.Info);
                    }
                }
            }

            if (vehicleCount == 0)
            {
                AddResult(cat, "No networked vehicle prefabs found in Assets/Multiplayer!", MessageType.Error);
            }
        }

        private void ValidateSpawnableBullets()
        {
            string cat = "Bullet Prefabs";

            string[] guids = AssetDatabase.FindAssets("t:GameObject Networked", new[] { "Assets/Multiplayer/Pool" });
            int bulletCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                var projectile = go.GetComponent<NetworkedProjectile>();
                if (projectile == null) continue;

                bulletCount++;
                var nob = go.GetComponent<NetworkObject>();
                var adapter = go.GetComponent<MultiplayerRigidbodyAdapter>();

                if (nob == null)
                {
                    AddResult(cat, $"{go.name}: Missing NetworkObject!", MessageType.Error);
                }
                else if (adapter == null)
                {
                    AddResult(cat, $"{go.name}: Missing MultiplayerRigidbodyAdapter!", MessageType.Warning);
                }
                else
                {
                    AddResult(cat, $"{go.name}: [OK]OK.", MessageType.Info);
                }
            }

            if (bulletCount == 0)
            {
                AddResult(cat, "No networked bullet prefabs found in Assets/Multiplayer/Pool!", MessageType.Warning);
            }
        }

        private void ValidateSceneObjects()
        {
            string cat = "Scene Objects";

            var timerSystem = Object.FindFirstObjectByType<TimerSystem>(FindObjectsInactive.Include);
            if (timerSystem == null)
            {
                AddResult(cat, "No TimerSystem found. Game timers won't work.", MessageType.Warning);
            }

            var guard = Object.FindFirstObjectByType<MultiplayerSingletonGuard>();
            if (guard == null)
            {
                AddResult(cat, "No MultiplayerSingletonGuard found. Singleton lifecycle may break.", MessageType.Warning);
            }

            var spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                var byName = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
                int spawnCount = 0;
                foreach (var t in byName)
                {
                    if (t.name.Contains("SpawnPoint")) spawnCount++;
                }
                if (spawnCount > 0)
                {
                    AddResult(cat, $"Found {spawnCount} spawn points by name.", MessageType.Info);
                }
                else
                {
                    AddResult(cat, "No spawn points found in scene.", MessageType.Warning);
                }
            }
            else
            {
                AddResult(cat, $"Found {spawnPoints.Length} spawn points.", MessageType.Info);
            }
        }

        private void AddResult(string category, string message, MessageType severity)
        {
            _results.Add(new ValidationResult
            {
                Category = category,
                Message = message,
                Severity = severity
            });
        }
    }
}
