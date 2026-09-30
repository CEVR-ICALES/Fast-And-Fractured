using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using FishNet.Managing;

namespace FastAndFractured.Multiplayer.Testing.Editor
{
    /// <summary>
    /// Editor tool for setting up distributed multiplayer tests with ParrelSync.
    /// Supports up to 3 editor instances (1 original + 2 clones).
    /// </summary>
    public class DistributedTestSetupWindow : EditorWindow
    {
        private bool _isClone = false;
        private string _cloneArgument = "";
        private int _cloneIndex = -1; // -1 = original, 0 = clone_0, 1 = clone_1
        private Vector2 _scrollPosition;
        
        // Test settings
        private int _minPlayersToStart = 2;
        private float _countdownTime = 3f;
        private bool _autoStartEnabled = true;

        [MenuItem("Fast And Fractured/Multiplayer/Distributed Test Setup", priority = 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<DistributedTestSetupWindow>("Distributed Tests");
            window.minSize = new Vector2(420, 600);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshCloneStatus();
            LoadTestSettings();
        }

        private void RefreshCloneStatus()
        {
            string projectPath = Application.dataPath.Replace("/Assets", "");
            _isClone = System.IO.File.Exists(System.IO.Path.Combine(projectPath, ".clone"));
            
            string argPath = System.IO.Path.Combine(projectPath, ".arg");
            _cloneArgument = System.IO.File.Exists(argPath) 
                ? System.IO.File.ReadAllText(argPath).Trim() 
                : "";
            
            // Detect clone index from project path
            _cloneIndex = -1;
            if (_isClone)
            {
                if (projectPath.Contains("_clone_0"))
                    _cloneIndex = 0;
                else if (projectPath.Contains("_clone_1"))
                    _cloneIndex = 1;
                else
                    _cloneIndex = 0; // Default for unnamed clones
            }
        }

        private void LoadTestSettings()
        {
            _minPlayersToStart = EditorPrefs.GetInt("FF_MP_MinPlayers", 2);
            _countdownTime = EditorPrefs.GetFloat("FF_MP_Countdown", 3f);
            _autoStartEnabled = EditorPrefs.GetBool("FF_MP_AutoStart", true);
        }

        private void SaveTestSettings()
        {
            EditorPrefs.SetInt("FF_MP_MinPlayers", _minPlayersToStart);
            EditorPrefs.SetFloat("FF_MP_Countdown", _countdownTime);
            EditorPrefs.SetBool("FF_MP_AutoStart", _autoStartEnabled);
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Header
            GUILayout.Label("Distributed Multiplayer Testing", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Clone Status
            DrawCloneStatus();
            EditorGUILayout.Space();
            
            // Test Settings
            DrawTestSettings();
            EditorGUILayout.Space();

            // Setup Section
            DrawSetupSection();
            EditorGUILayout.Space();

            // Instructions
            DrawInstructions();
            EditorGUILayout.Space();

            // Quick Actions
            DrawQuickActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawCloneStatus()
        {
            EditorGUILayout.LabelField("ParrelSync Status", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string roleLabel;
                string roleDescription;
                Color roleColor;
                
                if (!_isClone)
                {
                    roleLabel = "ORIGINAL (HOST)";
                    roleDescription = "Will start as server/host";
                    roleColor = new Color(0.2f, 0.8f, 0.2f);
                }
                else if (_cloneIndex == 0)
                {
                    roleLabel = "CLONE 1 (CLIENT)";
                    roleDescription = "Will connect as client";
                    roleColor = new Color(0.4f, 0.6f, 1f);
                }
                else
                {
                    roleLabel = "CLONE 2 (CLIENT)";
                    roleDescription = "Will connect as client";
                    roleColor = new Color(0.8f, 0.4f, 1f);
                }
                
                var prevColor = GUI.contentColor;
                GUI.contentColor = roleColor;
                EditorGUILayout.LabelField(roleLabel, EditorStyles.boldLabel);
                GUI.contentColor = prevColor;
                
                EditorGUILayout.LabelField(roleDescription);
                
                if (!string.IsNullOrEmpty(_cloneArgument))
                {
                    EditorGUILayout.LabelField($"Clone Argument: {_cloneArgument}");
                }
            }

            if (GUILayout.Button("Refresh Status", GUILayout.Height(25)))
            {
                RefreshCloneStatus();
            }
        }
        
        private void DrawTestSettings()
        {
            EditorGUILayout.LabelField("Test Settings", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                
                _autoStartEnabled = EditorGUILayout.Toggle("Auto-Start Game", _autoStartEnabled);
                
                using (new EditorGUI.DisabledScope(!_autoStartEnabled))
                {
                    _minPlayersToStart = EditorGUILayout.IntSlider("Min Players to Start", _minPlayersToStart, 1, 3);
                    _countdownTime = EditorGUILayout.Slider("Countdown (seconds)", _countdownTime, 1f, 10f);
                }
                
                if (EditorGUI.EndChangeCheck())
                {
                    SaveTestSettings();
                }
                
                EditorGUILayout.Space(4);
                
                // Quick presets
                EditorGUILayout.LabelField("Quick Presets:", EditorStyles.miniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Solo (1P)", GUILayout.Height(24)))
                    {
                        _minPlayersToStart = 1;
                        _countdownTime = 1f;
                        SaveTestSettings();
                    }
                    if (GUILayout.Button("Duo (2P)", GUILayout.Height(24)))
                    {
                        _minPlayersToStart = 2;
                        _countdownTime = 3f;
                        SaveTestSettings();
                    }
                    if (GUILayout.Button("Trio (3P)", GUILayout.Height(24)))
                    {
                        _minPlayersToStart = 3;
                        _countdownTime = 5f;
                        SaveTestSettings();
                    }
                }
                
                EditorGUILayout.Space(4);
                
                if (GUILayout.Button("Apply Settings to Scene", GUILayout.Height(28)))
                {
                    ApplySettingsToScene();
                }
            }
        }

        private void DrawSetupSection()
        {
            EditorGUILayout.LabelField("Scene Setup", EditorStyles.boldLabel);

            if (GUILayout.Button("Setup Current Scene for Distributed Tests", GUILayout.Height(30)))
            {
                SetupCurrentScene();
            }

            if (GUILayout.Button("Open MultiplayerRoomTest Scene", GUILayout.Height(25)))
            {
                string scenePath = "Assets/Multiplayer/Testing/Scenes/MultiplayerRoomTest.unity";
                if (System.IO.File.Exists(scenePath))
                {
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        EditorSceneManager.OpenScene(scenePath);
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("Scene Not Found", 
                        $"Could not find scene at: {scenePath}", "OK");
                }
            }
        }

        private void DrawInstructions()
        {
            EditorGUILayout.LabelField("How to Run Distributed Tests", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
@"1. SETUP (do once):
   - Open ParrelSync > Clones Manager
   - Create 1 or 2 clones for 2P/3P testing
   - Open each clone in a separate Unity instance

2. CONFIGURE:
   - Set 'Min Players to Start' above (1-3)
   - Click 'Apply Settings to Scene'
   - Save the scene

3. RUN TESTS:
   - In ORIGINAL editor: Enter Play Mode FIRST (HOST)
   - In CLONE editors: Enter Play Mode SECOND (CLIENTs)
   - Game auto-starts when min players reached

4. VIEW RESULTS:
   - Check Console for real-time results
   - Results: Assets/Multiplayer/Testing/test_results.json

NOTE: All editors share the same scene via ParrelSync symlinks!",
                MessageType.Info);
        }

        private void DrawQuickActions()
        {
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Clones Manager", GUILayout.Height(25)))
                {
                    EditorApplication.ExecuteMenuItem("ParrelSync/Clones Manager");
                }

                if (GUILayout.Button("View Test Results", GUILayout.Height(25)))
                {
                    string resultsPath = System.IO.Path.Combine(
                        Application.dataPath, "Multiplayer", "Testing", "test_results.json");
                    
                    if (System.IO.File.Exists(resultsPath))
                    {
                        string content = System.IO.File.ReadAllText(resultsPath);
                        Debug.Log($"=== TEST RESULTS ===\n{content}");
                        EditorUtility.DisplayDialog("Test Results", content, "OK");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("No Results", 
                            "No test results file found. Run tests first!", "OK");
                    }
                }
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Validate Test Components", GUILayout.Height(25)))
            {
                ValidateTestComponents();
            }
        }
        
        private void ApplySettingsToScene()
        {
            // Find AutoGameStarter in scene
            var autoStarter = FindFirstObjectByType<AutoGameStarter>();
            if (autoStarter == null)
            {
                EditorUtility.DisplayDialog("Not Found", 
                    "AutoGameStarter not found in scene.\nRun 'Setup Current Scene' first.", "OK");
                return;
            }
            
            // Use SerializedObject to modify the component
            SerializedObject so = new SerializedObject(autoStarter);
            
            var autoStartProp = so.FindProperty("autoStartEnabled");
            var minPlayersProp = so.FindProperty("minPlayersToStart");
            var countdownProp = so.FindProperty("countdownTime");
            
            if (autoStartProp != null) autoStartProp.boolValue = _autoStartEnabled;
            if (minPlayersProp != null) minPlayersProp.intValue = _minPlayersToStart;
            if (countdownProp != null) countdownProp.floatValue = _countdownTime;
            
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(autoStarter);

            ApplyDistributedScenePlayerSettings();
            
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            
            Debug.Log($"[DistributedTests] Applied settings: AutoStart={_autoStartEnabled}, MinPlayers={_minPlayersToStart}, Countdown={_countdownTime}s");
            EditorUtility.DisplayDialog("Settings Applied", 
                $"AutoGameStarter updated:\n" +
                $"• Auto-Start: {_autoStartEnabled}\n" +
                $"• Min Players: {_minPlayersToStart}\n" +
                $"• Countdown: {_countdownTime}s\n\n" +
                $"Remember to save the scene!", "OK");
        }

        private void SetupCurrentScene()
        {
            // Find NetworkManager
            var networkManager = FindFirstObjectByType<NetworkManager>();
            if (networkManager == null)
            {
                EditorUtility.DisplayDialog("Error", 
                    "NetworkManager not found in scene. Add FishNet NetworkManager first.", "OK");
                return;
            }

            // Find or create test coordinator object
            GameObject testCoordinator = GameObject.Find("DistributedTestCoordinator");
            if (testCoordinator == null)
            {
                testCoordinator = new GameObject("DistributedTestCoordinator");
                Undo.RegisterCreatedObjectUndo(testCoordinator, "Create Test Coordinator");
            }

            // Add ParrelSyncAutoConfig
            if (testCoordinator.GetComponent<ParrelSyncAutoConfig>() == null)
            {
                Undo.AddComponent<ParrelSyncAutoConfig>(testCoordinator);
                Debug.Log("[Setup] Added ParrelSyncAutoConfig");
            }

            // Add DistributedTestSystem
            var testSystem = testCoordinator.GetComponent<DistributedTestSystem>();
            if (testSystem == null)
            {
                testSystem = Undo.AddComponent<DistributedTestSystem>(testCoordinator);
                Debug.Log("[Setup] Added DistributedTestSystem");
            }

            // Add NetworkObject if needed (for RPCs)
            var networkObject = testCoordinator.GetComponent<FishNet.Object.NetworkObject>();
            if (networkObject == null)
            {
                networkObject = Undo.AddComponent<FishNet.Object.NetworkObject>(testCoordinator);
                Debug.Log("[Setup] Added NetworkObject");
            }
            
            // Find or setup AutoGameStarter
            var autoStarter = FindFirstObjectByType<AutoGameStarter>();
            if (autoStarter == null)
            {
                // Try to find NetworkedLevelControllerManager and add AutoGameStarter to it
                var levelManager = FindFirstObjectByType<NetworkedLevelControllerManager>();
                if (levelManager != null)
                {
                    autoStarter = Undo.AddComponent<AutoGameStarter>(levelManager.gameObject);
                    Debug.Log("[Setup] Added AutoGameStarter to NetworkedLevelControllerManager");
                }
            }
            
            // Apply current settings to AutoGameStarter
            if (autoStarter != null)
            {
                SerializedObject so = new SerializedObject(autoStarter);
                var autoStartProp = so.FindProperty("autoStartEnabled");
                var minPlayersProp = so.FindProperty("minPlayersToStart");
                var countdownProp = so.FindProperty("countdownTime");
                
                if (autoStartProp != null) autoStartProp.boolValue = _autoStartEnabled;
                if (minPlayersProp != null) minPlayersProp.intValue = _minPlayersToStart;
                if (countdownProp != null) countdownProp.floatValue = _countdownTime;
                
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(autoStarter);
            }

            ApplyDistributedScenePlayerSettings();

            // Mark scene dirty
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Setup Complete", 
                "Distributed test components added to scene!\n\n" +
                $"Settings applied:\n" +
                $"• Min Players: {_minPlayersToStart}\n" +
                $"• Countdown: {_countdownTime}s\n\n" +
                "Remember to save the scene before running tests.", "OK");
        }

        /// <summary>
        /// Keeps scene-wide runtime limits aligned with the selected distributed-test player count.
        /// Prevents a common mismatch where min players is 3 but LevelController can only spawn 2.
        /// </summary>
        private void ApplyDistributedScenePlayerSettings()
        {
            int targetPlayerCount = Mathf.Max(1, _minPlayersToStart);

            var levelController = FindFirstObjectByType<FastAndFractured.LevelController>();
            if (levelController != null)
            {
                SerializedObject levelSo = new SerializedObject(levelController);
                var maxCharactersProp = levelSo.FindProperty("maxCharactersInGame");
                if (maxCharactersProp != null)
                {
                    maxCharactersProp.intValue = targetPlayerCount;
                    levelSo.ApplyModifiedProperties();
                    EditorUtility.SetDirty(levelController);
                }
            }

            var testSystem = FindFirstObjectByType<DistributedTestSystem>();
            if (testSystem != null)
            {
                SerializedObject testSo = new SerializedObject(testSystem);
                var requiredClientsProp = testSo.FindProperty("_requiredClients");
                if (requiredClientsProp != null)
                {
                    // Server-side client count in this flow tracks remote clients only.
                    requiredClientsProp.intValue = Mathf.Max(1, targetPlayerCount - 1);
                    testSo.ApplyModifiedProperties();
                    EditorUtility.SetDirty(testSystem);
                }
            }
        }

        private void ValidateTestComponents()
        {
            var networkManager = FindFirstObjectByType<NetworkManager>();
            var autoConfig = FindFirstObjectByType<ParrelSyncAutoConfig>();
            var testSystem = FindFirstObjectByType<DistributedTestSystem>();
            var autoStarter = FindFirstObjectByType<AutoGameStarter>();

            string status = "Component Validation:\n\n";
            bool allGood = true;

            if (networkManager != null)
                status += "[OK] NetworkManager found\n";
            else
            {
                status += "[MISSING] NetworkManager\n";
                allGood = false;
            }

            if (autoConfig != null)
                status += "[OK] ParrelSyncAutoConfig found\n";
            else
            {
                status += "[MISSING] ParrelSyncAutoConfig\n";
                allGood = false;
            }

            if (testSystem != null)
            {
                var no = testSystem.GetComponent<FishNet.Object.NetworkObject>();
                if (no != null)
                    status += "[OK] DistributedTestSystem with NetworkObject\n";
                else
                {
                    status += "[WARNING] DistributedTestSystem missing NetworkObject\n";
                    allGood = false;
                }
            }
            else
            {
                status += "[MISSING] DistributedTestSystem\n";
                allGood = false;
            }
            
            if (autoStarter != null)
            {
                // Read current settings
                SerializedObject so = new SerializedObject(autoStarter);
                var minPlayers = so.FindProperty("minPlayersToStart");
                var countdown = so.FindProperty("countdownTime");
                var autoStart = so.FindProperty("autoStartEnabled");
                
                status += $"[OK] AutoGameStarter: ";
                status += $"MinPlayers={minPlayers?.intValue ?? 0}, ";
                status += $"Countdown={countdown?.floatValue ?? 0}s, ";
                status += $"AutoStart={autoStart?.boolValue ?? false}\n";
            }
            else
            {
                status += "[WARNING] AutoGameStarter not found\n";
            }

            if (allGood)
            {
                status += "\nAll components valid! Ready for testing.";
            }
            else
            {
                status += "\nClick 'Setup Current Scene' to add missing components.";
            }

            EditorUtility.DisplayDialog("Validation Results", status, "OK");
        }
    }
}
