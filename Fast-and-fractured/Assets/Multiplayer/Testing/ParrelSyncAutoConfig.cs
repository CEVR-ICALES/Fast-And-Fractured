using UnityEngine;
using FishNet.Managing;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FastAndFractured.Multiplayer.Testing
{
    /// <summary>
    /// Network role to start with. Because FishNet has no host migration, the recommended test
    /// setup is one ServerOnly instance (a dedicated process that never plays and so never
    /// "leaves") plus N ClientOnly instances — instead of a Host that, if it quits, kills the match.
    /// </summary>
    public enum NetworkRole
    {
        /// <summary>Original editor → Host, ParrelSync clone → Client. Legacy default.</summary>
        Auto,
        /// <summary>Server + local client in one process (P2P listen-server). No host migration.</summary>
        Host,
        /// <summary>Server with NO local client — the dedicated-server test role.</summary>
        ServerOnly,
        /// <summary>Client only; connects to a separate server/host.</summary>
        ClientOnly,
    }

    /// <summary>
    /// Automatic ParrelSync configuration for multiplayer testing.
    ///
    /// Resolves the network role in this priority order:
    ///   1. _roleOverride (inspector) if not Auto.
    ///   2. ParrelSync clone .arg file: "server" → ServerOnly, "client" → ClientOnly.
    ///   3. Auto fallback: original editor → Host, clone → ClientOnly.
    ///
    /// Place on a GameObject in the test scene alongside the NetworkManager.
    /// </summary>
    public class ParrelSyncAutoConfig : MonoBehaviour
    {
        [Header("Auto-Configuration")]
        [SerializeField] private bool _enableAutoConfig = true;
        [Tooltip("Force a role regardless of ParrelSync detection. Leave Auto to use clone arg / original-vs-clone.")]
        [SerializeField] private NetworkRole _roleOverride = NetworkRole.Auto;
        [SerializeField] private float _connectionDelay = 1.0f;
        [SerializeField] private float _clientExtraDelay = 2.0f; // Extra delay for client to wait for server

        [Header("Network Settings")]
        [SerializeField] private string _serverAddress = "localhost";
        [SerializeField] private ushort _serverPort = 7770;

        [Header("Debug")]
        [SerializeField] private bool _isClone = false;
        [SerializeField] private string _cloneArgument = "";

        private NetworkManager _networkManager;
        private bool _connectionStarted = false;

        private void Awake()
        {
            // Detect ParrelSync clone status
            _isClone = IsParrelSyncClone();
            _cloneArgument = GetParrelSyncArgument();
            
            Debug.Log($"<color=cyan>[ParrelSyncAutoConfig] IsClone: {_isClone}, Argument: '{_cloneArgument}'</color>");
        }

        private void Start()
        {
            if (!_enableAutoConfig)
            {
                Debug.Log("<color=grey>[ParrelSyncAutoConfig] Auto-config disabled</color>");
                return;
            }

            _networkManager = FindFirstObjectByType<NetworkManager>();
            if (_networkManager == null)
            {
                Debug.LogError("[ParrelSyncAutoConfig] NetworkManager not found!");
                return;
            }

            // Configure transport address if needed
            ConfigureTransport();

            // Clients wait a bit longer so the server is up first.
            bool startsAsClient = ResolveRole() == NetworkRole.ClientOnly;
            float delay = startsAsClient ? (_connectionDelay + _clientExtraDelay) : _connectionDelay;
            Invoke(nameof(StartConnection), delay);
        }

        /// <summary>Resolves the effective role from override → clone arg → original/clone fallback.</summary>
        private NetworkRole ResolveRole()
        {
            if (_roleOverride != NetworkRole.Auto)
                return _roleOverride;

            string arg = _cloneArgument.Trim().ToLowerInvariant();
            if (arg == "server") return NetworkRole.ServerOnly;
            if (arg == "client") return NetworkRole.ClientOnly;

            return _isClone ? NetworkRole.ClientOnly : NetworkRole.Host;
        }

        private void ConfigureTransport()
        {
            // Get the transport and set address/port
            var transport = _networkManager.TransportManager.Transport;
            if (transport != null)
            {
                // Most transports have SetClientAddress and SetPort methods
                // For Tugboat (default FishNet transport):
                var tugboat = transport as FishNet.Transporting.Tugboat.Tugboat;
                if (tugboat != null)
                {
                    tugboat.SetClientAddress(_serverAddress);
                    tugboat.SetPort(_serverPort);
                    Debug.Log($"<color=cyan>[ParrelSyncAutoConfig] Configured Tugboat: {_serverAddress}:{_serverPort}</color>");
                }
            }
        }

        private void StartConnection()
        {
            if (_connectionStarted) return;
            _connectionStarted = true;
            
            if (_networkManager == null)
            {
                _networkManager = FindFirstObjectByType<NetworkManager>();
                if (_networkManager == null)
                {
                    Debug.LogError("[ParrelSyncAutoConfig] NetworkManager not found when trying to start connection.");
                    return;
                }
            }
            
            // Avoid double-start if another auto-connect component already started networking.
            if (_networkManager.ServerManager.Started || _networkManager.ClientManager.Started)
            {
                Debug.Log("<color=grey>[ParrelSyncAutoConfig] Connection already started by another system. Skipping auto-start.</color>");
                return;
            }

            StartWithRole(ResolveRole());
        }

        private void StartWithRole(NetworkRole role)
        {
            switch (role)
            {
                case NetworkRole.ServerOnly:
                    Debug.Log("<color=cyan>[ParrelSyncAutoConfig] Starting as DEDICATED SERVER (no local client)</color>");
                    _networkManager.ServerManager.StartConnection();
                    break;

                case NetworkRole.ClientOnly:
                    Debug.Log("<color=yellow>[ParrelSyncAutoConfig] Starting as CLIENT</color>");
                    _networkManager.ClientManager.StartConnection();
                    break;

                case NetworkRole.Host:
                default:
                    Debug.Log("<color=green>[ParrelSyncAutoConfig] Starting as HOST (server + local client)</color>");
                    _networkManager.ServerManager.StartConnection();
                    _networkManager.ClientManager.StartConnection();
                    break;
            }
        }

        /// <summary>
        /// Check if this Unity instance is a ParrelSync clone
        /// </summary>
        private bool IsParrelSyncClone()
        {
#if UNITY_EDITOR
            string projectPath = Application.dataPath.Replace("/Assets", "");
            string cloneFilePath = Path.Combine(projectPath, ".clone");
            return File.Exists(cloneFilePath);
#else
            return false;
#endif
        }

        /// <summary>
        /// Get the argument passed to this ParrelSync clone (if any)
        /// </summary>
        private string GetParrelSyncArgument()
        {
#if UNITY_EDITOR
            string projectPath = Application.dataPath.Replace("/Assets", "");
            string argFilePath = Path.Combine(projectPath, ".arg");
            if (File.Exists(argFilePath))
            {
                return File.ReadAllText(argFilePath).Trim();
            }
#endif
            return "";
        }

        /// <summary>
        /// Manually trigger connection (for testing)
        /// </summary>
        [ContextMenu("Manual Connect")]
        public void ManualConnect()
        {
            _connectionStarted = false;
            StartConnection();
        }

        /// <summary>
        /// Force start as host (ignores ParrelSync detection)
        /// </summary>
        [ContextMenu("Force Start as Host")]
        public void ForceStartAsHost()
        {
            if (_networkManager == null)
            {
                _networkManager = FindFirstObjectByType<NetworkManager>();
            }
            
            Debug.Log("<color=green>[ParrelSyncAutoConfig] Force starting as HOST</color>");
            _networkManager.ServerManager.StartConnection();
            _networkManager.ClientManager.StartConnection();
        }

        /// <summary>
        /// Force start as client (ignores ParrelSync detection)
        /// </summary>
        [ContextMenu("Force Start as Client")]
        public void ForceStartAsClient()
        {
            if (_networkManager == null)
            {
                _networkManager = FindFirstObjectByType<NetworkManager>();
            }

            Debug.Log("<color=yellow>[ParrelSyncAutoConfig] Force starting as CLIENT</color>");
            _networkManager.ClientManager.StartConnection();
        }

        /// <summary>
        /// Force start as a dedicated server with no local client (ignores ParrelSync detection).
        /// </summary>
        [ContextMenu("Force Start as Dedicated Server")]
        public void ForceStartAsServer()
        {
            if (_networkManager == null)
            {
                _networkManager = FindFirstObjectByType<NetworkManager>();
            }

            Debug.Log("<color=cyan>[ParrelSyncAutoConfig] Force starting as DEDICATED SERVER</color>");
            _networkManager.ServerManager.StartConnection();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _isClone = IsParrelSyncClone();
            _cloneArgument = GetParrelSyncArgument();
        }

        [CustomEditor(typeof(ParrelSyncAutoConfig))]
        public class ParrelSyncAutoConfigEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();

                var config = (ParrelSyncAutoConfig)target;

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
                
                GUI.enabled = false;
                EditorGUILayout.Toggle("Is ParrelSync Clone", config._isClone);
                EditorGUILayout.TextField("Clone Argument", config._cloneArgument);
                GUI.enabled = true;

                EditorGUILayout.Space();

                var resolved = config.ResolveRole();
                EditorGUILayout.LabelField("Resolved Role", resolved.ToString());

                string arg = config._cloneArgument.Trim().ToLowerInvariant();
                string source;
                if (config._roleOverride != NetworkRole.Auto)
                    source = "from inspector override";
                else if (arg == "server" || arg == "client")
                    source = $"from ParrelSync arg '{config._cloneArgument}'";
                else
                    source = config._isClone ? "clone default → Client" : "original default → Host";

                EditorGUILayout.HelpBox(
                    $"Will start as {resolved} ({source}).\n\n" +
                    "Recommended (no host migration): tag one clone's ParrelSync argument 'server' " +
                    "(or set Role Override = ServerOnly) and the rest 'client'.",
                    MessageType.Info);
            }
        }
#endif
    }
}
