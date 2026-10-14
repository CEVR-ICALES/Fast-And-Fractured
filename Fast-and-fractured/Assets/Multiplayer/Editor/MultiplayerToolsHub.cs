using UnityEditor;
using UnityEngine;
using FishNet.Managing;
using FastAndFractured.Multiplayer.Testing.Editor;
using FastAndFractured.EditorTools;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>
    /// Unified entrypoint for multiplayer editor workflows: the pipeline tools (maps, cars,
    /// bullets, AI, lobby, menu), diagnostics and the network stress simulation.
    /// </summary>
    public class MultiplayerToolsHub : EditorWindow
    {
        private enum StressPreset
        {
            Disabled,
            GoodWifi,
            Mobile4G,
            BadWifi,
            Extreme
        }

        private StressPreset _preset = StressPreset.Disabled;
        private long _latencyMs;
        private double _packetLoss;
        private double _outOfOrder;

        [MenuItem("Tools/Fast And Fractured/Multiplayer Tools Hub", priority = 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<MultiplayerToolsHub>("MP Tools Hub");
            window.minSize = new Vector2(480f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            ApplyPresetValues(_preset);
        }

        private void OnGUI()
        {
            DrawSetupSection();
            EditorGUILayout.Space(8f);
            DrawValidationAndTestingSection();
            EditorGUILayout.Space(8f);
            DrawStressSection();
        }

        private void DrawSetupSection()
        {
            EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button("Lobby Setup Wizard  (MapCatalog + MenuScreens)", GUILayout.Height(32f)))
                {
                    LobbySetupWizard.ShowWindow();
                }

                if (GUILayout.Button("Map Registration Tool  (register scenes as network maps)", GUILayout.Height(28f)))
                {
                    NetworkedMapRegistrationTool.ShowWindow();
                }

                EditorGUILayout.Space(4f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Vehicle Setup Tool", GUILayout.Height(28f)))
                    {
                        NetworkedVehicleSetupTool.ShowWindow();
                    }

                    if (GUILayout.Button("Bullet Converter", GUILayout.Height(28f)))
                    {
                        NetworkedBulletConverter.ShowWindow();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("AI Setup Tool", GUILayout.Height(28f)))
                    {
                        NetworkedAISetupTool.ShowWindow();
                    }

                    if (GUILayout.Button("Main Menu: Singleplayer / Multiplayer", GUILayout.Height(28f)))
                    {
                        MainMenuPlayModesTool.Apply();
                    }
                }

                if (GUILayout.Button("Networked Prop Setup  (hazard prefab variants)", GUILayout.Height(28f)))
                {
                    NetworkedPropSetupTool.ShowWindow();
                }

                if (GUILayout.Button("Scene Migration  (duplicate map + swap props)", GUILayout.Height(28f)))
                {
                    NetworkedSceneMigrationTool.ShowWindow();
                }
            }
        }

        private void DrawValidationAndTestingSection()
        {
            EditorGUILayout.LabelField("Validation & Testing", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Validate Multiplayer Scene", GUILayout.Height(28f)))
                    {
                        MultiplayerSceneValidator.ShowWindow();
                    }

                    if (GUILayout.Button("Multiplayer Log Filter", GUILayout.Height(28f)))
                    {
                        MultiplayerLogFilterWindow.ShowWindow();
                    }
                }

                if (GUILayout.Button("Distributed Test Setup", GUILayout.Height(28f)))
                {
                    DistributedTestSetupWindow.ShowWindow();
                }
            }
        }

        private void DrawStressSection()
        {
            EditorGUILayout.LabelField("Network Stress Simulation (FishNet LatencySimulator)", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _preset = (StressPreset)EditorGUILayout.EnumPopup("Preset", _preset);
                if (GUILayout.Button("Load Preset Values", GUILayout.Height(24f)))
                {
                    ApplyPresetValues(_preset);
                }

                _latencyMs = EditorGUILayout.LongField("Latency (ms)", _latencyMs);
                _packetLoss = EditorGUILayout.Slider("Packet Loss", (float)_packetLoss, 0f, 1f);
                _outOfOrder = EditorGUILayout.Slider("Out Of Order", (float)_outOfOrder, 0f, 1f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Apply Runtime Settings", GUILayout.Height(28f)))
                    {
                        ApplyRuntimeStressSettings(enable: true);
                    }

                    if (GUILayout.Button("Disable Simulation", GUILayout.Height(28f)))
                    {
                        DisableRuntimeStressSettings();
                    }
                }
            }
        }

        private void ApplyPresetValues(StressPreset preset)
        {
            switch (preset)
            {
                case StressPreset.Disabled:
                    _latencyMs = 0;
                    _packetLoss = 0d;
                    _outOfOrder = 0d;
                    break;
                case StressPreset.GoodWifi:
                    _latencyMs = 25;
                    _packetLoss = 0.005d;
                    _outOfOrder = 0.01d;
                    break;
                case StressPreset.Mobile4G:
                    _latencyMs = 75;
                    _packetLoss = 0.02d;
                    _outOfOrder = 0.03d;
                    break;
                case StressPreset.BadWifi:
                    _latencyMs = 140;
                    _packetLoss = 0.07d;
                    _outOfOrder = 0.08d;
                    break;
                case StressPreset.Extreme:
                    _latencyMs = 250;
                    _packetLoss = 0.15d;
                    _outOfOrder = 0.15d;
                    break;
            }
        }

        private void ApplyRuntimeStressSettings(bool enable)
        {
            NetworkManager manager = FindFirstObjectByType<NetworkManager>();
            if (manager == null || manager.TransportManager == null)
            {
                EditorUtility.DisplayDialog("NetworkManager Missing",
                    "Could not find an active NetworkManager with TransportManager (enter Play mode first).",
                    "OK");
                return;
            }

            var simulator = manager.TransportManager.LatencySimulator;
            if (simulator == null)
            {
                EditorUtility.DisplayDialog("LatencySimulator Missing",
                    "TransportManager has no LatencySimulator instance.",
                    "OK");
                return;
            }

            simulator.SetLatency(Mathf.Max(0, (int)_latencyMs));
            simulator.SetPacketLoss(Mathf.Clamp01((float)_packetLoss));
            simulator.SetOutOfOrder(Mathf.Clamp01((float)_outOfOrder));
            simulator.SetEnabled(enable);

            Debug.Log($"[MP Tools Hub] LatencySimulator {(enable ? "enabled" : "disabled")} | latency={_latencyMs}ms, loss={_packetLoss:0.###}, outOfOrder={_outOfOrder:0.###}");
        }

        private void DisableRuntimeStressSettings()
        {
            _preset = StressPreset.Disabled;
            ApplyPresetValues(_preset);
            ApplyRuntimeStressSettings(enable: false);
        }
    }
}
