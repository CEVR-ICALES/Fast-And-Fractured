using UnityEditor;
using UnityEngine;
using FishNet.Managing;
using FastAndFractured.EditorTools;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>
    /// Unified entrypoint for multiplayer editor workflows: the pipeline tools (maps, cars,
    /// bullets, AI, lobby, menu), diagnostics and the network stress simulation.
    /// </summary>
    public class MultiplayerToolsHub : EditorWindow
    {
        private const float SECTION_SPACING = 8f;
        private const float SMALL_SPACING = 4f;
        private const float PRIMARY_BUTTON_HEIGHT = 32f;
        private const float BUTTON_HEIGHT = 28f;
        private const float SMALL_BUTTON_HEIGHT = 24f;
        private static readonly Vector2 MIN_WINDOW_SIZE = new Vector2(480f, 420f);

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
            window.minSize = MIN_WINDOW_SIZE;
            window.Show();
        }

        private void OnEnable()
        {
            ApplyPresetValues(_preset);
        }

        private void OnGUI()
        {
            DrawSetupSection();
            EditorGUILayout.Space(SECTION_SPACING);
            DrawValidationAndTestingSection();
            EditorGUILayout.Space(SECTION_SPACING);
            DrawStressSection();
        }

        private void DrawSetupSection()
        {
            EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button("Lobby Setup Wizard  (MapCatalog + MenuScreens)", GUILayout.Height(PRIMARY_BUTTON_HEIGHT)))
                {
                    LobbySetupWizard.ShowWindow();
                }

                if (GUILayout.Button("Map Registration Tool  (register scenes as network maps)", GUILayout.Height(BUTTON_HEIGHT)))
                {
                    NetworkedMapRegistrationTool.ShowWindow();
                }

                EditorGUILayout.Space(SMALL_SPACING);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Vehicle Setup Tool", GUILayout.Height(BUTTON_HEIGHT)))
                    {
                        NetworkedVehicleSetupTool.ShowWindow();
                    }

                    if (GUILayout.Button("Bullet Converter", GUILayout.Height(BUTTON_HEIGHT)))
                    {
                        NetworkedBulletConverter.ShowWindow();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("AI Setup Tool", GUILayout.Height(BUTTON_HEIGHT)))
                    {
                        NetworkedAISetupTool.ShowWindow();
                    }

                    if (GUILayout.Button("Main Menu: Singleplayer / Multiplayer", GUILayout.Height(BUTTON_HEIGHT)))
                    {
                        MainMenuPlayModesTool.Apply();
                    }
                }

                if (GUILayout.Button("Networked Prop Setup  (hazard prefab variants)", GUILayout.Height(BUTTON_HEIGHT)))
                {
                    NetworkedPropSetupTool.ShowWindow();
                }

                if (GUILayout.Button("Scene Migration  (duplicate map + swap props)", GUILayout.Height(BUTTON_HEIGHT)))
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
                    if (GUILayout.Button("Validate Multiplayer Scene", GUILayout.Height(BUTTON_HEIGHT)))
                    {
                        MultiplayerSceneValidator.ShowWindow();
                    }

                    if (GUILayout.Button("Multiplayer Log Filter", GUILayout.Height(BUTTON_HEIGHT)))
                    {
                        MultiplayerLogFilterWindow.ShowWindow();
                    }
                }
            }
        }

        private void DrawStressSection()
        {
            EditorGUILayout.LabelField("Network Stress Simulation (FishNet LatencySimulator)", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _preset = (StressPreset)EditorGUILayout.EnumPopup("Preset", _preset);
                if (GUILayout.Button("Load Preset Values", GUILayout.Height(SMALL_BUTTON_HEIGHT)))
                {
                    ApplyPresetValues(_preset);
                }

                _latencyMs = EditorGUILayout.LongField("Latency (ms)", _latencyMs);
                _packetLoss = EditorGUILayout.Slider("Packet Loss", (float)_packetLoss, 0f, 1f);
                _outOfOrder = EditorGUILayout.Slider("Out Of Order", (float)_outOfOrder, 0f, 1f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Apply Runtime Settings", GUILayout.Height(BUTTON_HEIGHT)))
                    {
                        ApplyRuntimeStressSettings(enable: true);
                    }

                    if (GUILayout.Button("Disable Simulation", GUILayout.Height(BUTTON_HEIGHT)))
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
