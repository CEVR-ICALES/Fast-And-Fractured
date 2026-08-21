using UnityEngine;
using FishNet.Managing;
using System.IO;

namespace FastAndFractured.Multiplayer.Testing
{
    /// <summary>
    /// Automatically starts Host or Client when entering Play Mode
    /// Useful for ParallelSync testing:
    /// - Main Editor: Configure as AUTO_HOST
    /// - Clone Editor: Configure as AUTO_CLIENT
    /// </summary>
    public class AutoConnectOnPlayMode : MonoBehaviour
    {
        public enum ConnectionMode
        {
            DISABLED,
            AUTO_HOST,
            AUTO_CLIENT
        }
        
        [Header("Auto-Connection Configuration")]
        [SerializeField] private ConnectionMode _connectionMode = ConnectionMode.DISABLED;
        [SerializeField] private float _delaySeconds = 0.5f;
        [SerializeField] private bool _enforceCloneAsClient = true;
        
        [Header("References")]
        [SerializeField] private NetworkManager _networkManager;
        
        private void Start()
        {
            // ParrelSyncAutoConfig is the preferred orchestrator for distributed tests.
            // If present, this legacy helper must stay idle to prevent double start/stop cycles.
            var parrelSyncAutoConfig = FindFirstObjectByType<ParrelSyncAutoConfig>();
            if (parrelSyncAutoConfig != null && parrelSyncAutoConfig.isActiveAndEnabled)
            {
                Debug.Log("<color=grey>[AutoConnect] ParrelSyncAutoConfig detected. Legacy AutoConnect skipped.</color>");
                return;
            }

            if (_connectionMode == ConnectionMode.DISABLED)
            {
                Debug.Log("<color=grey>[AutoConnect] DISABLED - Manual connection required</color>");
                return;
            }
            
            if (_networkManager == null)
            {
                _networkManager = FindObjectOfType<NetworkManager>();
                if (_networkManager == null)
                {
                    Debug.LogError("[AutoConnect] NetworkManager not found!");
                    return;
                }
            }
            
            Invoke(nameof(AutoConnect), _delaySeconds);
        }
        
        private void AutoConnect()
        {
            if (_networkManager.ServerManager.Started || _networkManager.ClientManager.Started)
            {
                Debug.Log("<color=grey>[AutoConnect] Connection already started. Skipping.</color>");
                return;
            }

            ConnectionMode runtimeMode = ResolveRuntimeConnectionMode();

            switch (runtimeMode)
            {
                case ConnectionMode.AUTO_HOST:
                    Debug.Log("<color=cyan>[AutoConnect] Starting as HOST...</color>");
                    _networkManager.ServerManager.StartConnection();
                    _networkManager.ClientManager.StartConnection();
                    break;
                    
                case ConnectionMode.AUTO_CLIENT:
                    Debug.Log("<color=cyan>[AutoConnect] Starting as CLIENT...</color>");
                    _networkManager.ClientManager.StartConnection();
                    break;
            }
        }

        private ConnectionMode ResolveRuntimeConnectionMode()
        {
            if (!_enforceCloneAsClient)
                return _connectionMode;

            bool isClone = IsParrelSyncClone();
            if (isClone && _connectionMode == ConnectionMode.AUTO_HOST)
            {
                Debug.LogWarning("[AutoConnect] ParrelSync clone detected with AUTO_HOST mode. Forcing AUTO_CLIENT to avoid port conflicts.");
                return ConnectionMode.AUTO_CLIENT;
            }

            return _connectionMode;
        }

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
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_networkManager == null)
            {
                _networkManager = FindObjectOfType<NetworkManager>();
            }
        }
#endif
    }
}
