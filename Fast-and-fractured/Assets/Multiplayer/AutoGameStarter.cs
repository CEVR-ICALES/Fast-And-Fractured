using FishNet.Object;
using FishNet.Connection;
using FishNet.Transporting;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FastAndFractured;
using Enums;
using FastAndFractured.Multiplayer.Testing;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Auto-starts game when all connected players have selected characters.
    /// Only works if enabled and on server.
    /// </summary>
    public class AutoGameStarter : NetworkBehaviour
    {
        [Header("Auto-Start Settings")]
        [Tooltip("Enable auto-start when all players ready")]
        [SerializeField] private bool autoStartEnabled = true;
        
        [Tooltip("Minimum players required to start")]
        [SerializeField] private int minPlayersToStart = 2;

        [Tooltip("Use DistributedTestSystem expected players as a lower bound when present")]
        [SerializeField] private bool useDistributedExpectedPlayers = true;

        [Tooltip("Require all currently connected players to be ready before starting")]
        [SerializeField] private bool requireAllConnectedPlayersReady = true;
        
        [Tooltip("Countdown before starting (seconds)")]
        [SerializeField] private float countdownTime = 3f;
        
        [Header("References")]
        [SerializeField] private NetworkedLevelControllerManager levelManager;
        
        private HashSet<int> _readyPlayers = new HashSet<int>();
        private HashSet<int> _connectedPlayers = new HashSet<int>();
        private bool _gameStarted = false;
        private float _countdownTimer = 0f;
        private bool _countingDown = false;
        private Coroutine _countdownCoroutine;
        private DistributedTestSystem _distributedTestSystem;
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            if (levelManager == null)
                levelManager = GetComponent<NetworkedLevelControllerManager>();
            
            if (levelManager == null)
                levelManager = FindFirstObjectByType<NetworkedLevelControllerManager>();
                
            if (levelManager == null)
            {
                Debug.LogError("[AutoGameStarter] NetworkedLevelControllerManager not found!");
                enabled = false;
                return;
            }

            _readyPlayers.Clear();
            _connectedPlayers.Clear();
            _gameStarted = false;
            _distributedTestSystem = null;
            CacheConnections();

            if (ServerManager != null)
            {
                ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
            }
        }

        public override void OnStopServer()
        {
            base.OnStopServer();

            if (ServerManager != null)
            {
                ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
            }

            _readyPlayers.Clear();
            _connectedPlayers.Clear();
            _gameStarted = false;
            _countingDown = false;
            _countdownTimer = 0f;
        }
        
        /// <summary>
        /// Call this when a player selects their character
        /// </summary>
        public void OnPlayerReady(int clientId)
        {
            if (!IsServerInitialized || !autoStartEnabled || _gameStarted)
                return;
            
            _readyPlayers.Add(clientId);
            _connectedPlayers.Add(clientId);

            CheckReadyState();
        }
        
        private void CheckReadyState()
        {
            if (_gameStarted)
                return;

            CacheConnections();
            PruneDisconnectedState();

            int readyPlayers = _readyPlayers.Count;
            int requiredReadyPlayers = ResolveRequiredReadyCount();

            if (readyPlayers < requiredReadyPlayers)
            {
                if (_countingDown)
                {
                    CancelCountdown($"Ready players dropped below required threshold ({readyPlayers}/{requiredReadyPlayers}).");
                }
                return;
            }

            // Start countdown when all requirements are met.
            if (!_countingDown)
            {
                _countingDown = true;
                _countdownTimer = countdownTime;
                NotifyCountdownStartRpc(countdownTime);
            }
        }
        
        private void Update()
        {
            if (!IsServerInitialized || !_countingDown)
                return;
            
            _countdownTimer -= Time.deltaTime;
            
            if (_countdownTimer <= 0f)
            {
                StartGame();
            }
        }
        
        private void StartGame()
        {
            if (_gameStarted)
                return;
            
            _gameStarted = true;
            _countingDown = false;

            if (levelManager != null)
            {
                levelManager.StartGame();
            }
            else
            {
                Debug.LogError("[AutoGameStarter] Cannot start game - NetworkedLevelControllerManager is null!");
            }
        }

        private void HandleRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (!autoStartEnabled || _gameStarted || conn == null)
                return;

            if (args.ConnectionState == RemoteConnectionState.Started)
            {
                _connectedPlayers.Add(conn.ClientId);

                if (_countingDown && requireAllConnectedPlayersReady && !_readyPlayers.Contains(conn.ClientId))
                {
                    CancelCountdown($"Player {conn.ClientId} joined and is not ready.");
                }
            }
            else if (args.ConnectionState == RemoteConnectionState.Stopped)
            {
                _connectedPlayers.Remove(conn.ClientId);
                OnPlayerDisconnected(conn.ClientId);
            }

            CheckReadyState();
        }

        private void CacheConnections()
        {
            if (!IsServerInitialized || ServerManager == null)
                return;

            foreach (var conn in ServerManager.Clients.Values)
            {
                if (conn != null && conn.IsActive)
                {
                    _connectedPlayers.Add(conn.ClientId);
                }
            }

            if (LocalConnection != null && LocalConnection.IsValid)
            {
                _connectedPlayers.Add(LocalConnection.ClientId);
            }
        }

        private void PruneDisconnectedState()
        {
            _connectedPlayers.RemoveWhere(clientId => !IsClientConnected(clientId));
            _readyPlayers.RemoveWhere(clientId => !IsClientConnected(clientId));
        }

        private bool IsClientConnected(int clientId)
        {
            if (LocalConnection != null && LocalConnection.IsValid && LocalConnection.ClientId == clientId)
                return true;

            if (ServerManager == null)
                return false;

            foreach (var conn in ServerManager.Clients.Values)
            {
                if (conn != null && conn.IsActive && conn.ClientId == clientId)
                    return true;
            }

            return false;
        }

        private int ResolveRequiredReadyCount()
        {
            int required = Mathf.Max(1, minPlayersToStart);

            if (useDistributedExpectedPlayers)
            {
                int distributedExpected = ResolveDistributedExpectedPlayers();
                if (distributedExpected > required)
                    required = distributedExpected;
            }

            if (requireAllConnectedPlayersReady)
            {
                required = Mathf.Max(required, _connectedPlayers.Count);
            }

            return required;
        }

        private int ResolveDistributedExpectedPlayers()
        {
            if (_distributedTestSystem == null)
            {
                _distributedTestSystem = FindFirstObjectByType<DistributedTestSystem>();
            }

            return _distributedTestSystem != null ? _distributedTestSystem.ExpectedTotalPlayers : 0;
        }

        private void CancelCountdown(string reason)
        {
            if (!_countingDown || _gameStarted)
                return;

            _countingDown = false;
            _countdownTimer = 0f;
            NotifyCountdownCancelRpc();
        }
        
        [ObserversRpc]
        private void NotifyCountdownStartRpc(float countdown)
        {
            if (_countdownCoroutine != null) StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = StartCoroutine(ShowCountdown(countdown));
        }

        private IEnumerator ShowCountdown(float countdown)
        {
            if (TryGetCountdownHud(out var hud))
            {
                hud.UpdateUIElement(UIDynamicElementType.TIMER_TEXT, true);
                hud.UpdateUIElement(UIDynamicElementType.TIMER_TEXT, Mathf.Ceil(countdown).ToString());
            }

            float current = countdown;
            while (current > 0)
            {
                yield return null;
                current -= Time.deltaTime;
                if (TryGetCountdownHud(out hud))
                {
                    hud.UpdateUIElement(UIDynamicElementType.TIMER_TEXT, Mathf.Ceil(current).ToString());
                }
            }

            if (TryGetCountdownHud(out hud))
            {
                hud.UpdateUIElement(UIDynamicElementType.TIMER_TEXT, false);
            }
        }

        [ObserversRpc]
        private void NotifyCountdownCancelRpc()
        {
            if (_countdownCoroutine != null) StopCoroutine(_countdownCoroutine);
            if (TryGetCountdownHud(out var hud))
            {
                hud.UpdateUIElement(UIDynamicElementType.TIMER_TEXT, false);
            }
        }

        private static bool TryGetCountdownHud(out HUDManager hudManager)
        {
            hudManager = HUDManager.ExistingInstance;
            return hudManager != null &&
                   hudManager.UIElements != null &&
                   hudManager.UIElements.ContainsKey(UIDynamicElementType.TIMER_TEXT);
        }

        
        /// <summary>
        /// Reset ready state (useful if player disconnects)
        /// </summary>
        public void OnPlayerDisconnected(int clientId)
        {
            _readyPlayers.Remove(clientId);
            _connectedPlayers.Remove(clientId);
            
            // Cancel countdown if was counting down
            CancelCountdown($"Player {clientId} disconnected.");
        }
        
        private new void OnValidate()
        {
            if (levelManager == null)
                levelManager = GetComponent<NetworkedLevelControllerManager>();
        }
    }
}
