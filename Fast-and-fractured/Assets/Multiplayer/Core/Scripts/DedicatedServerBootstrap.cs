using System;
using System.Threading;
using FastAndFractured.Multiplayer.Abstractions;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Dedicated server builds: starts the FishNet server and creates a Steam lobby. Only for those builds (separate
    /// scene, or Application.isBatchMode).
    /// </summary>
    public sealed class DedicatedServerBootstrap : MonoBehaviour
    {
        private const int START_TIMEOUT_SECONDS = 30;

        [Header("Lobby Settings")]
        [SerializeField] private string _roomName  = "Dedicated Server";
        [SerializeField] private ushort _port      = 7777;
        [SerializeField] private int    _maxPlayers = 8;
        [SerializeField] private int    _maxAiSlots = 0;

        [Header("Activation")]
        [Tooltip("Only run when launched with -batchmode (headless). Disable to always run.")]
        [SerializeField] private bool _requireBatchMode = true;

        private CancellationTokenSource _cts;

        private async void Start()
        {
            if (_requireBatchMode && !Application.isBatchMode)
            {
                Debug.Log("[DedicatedServer] Not in batch mode — skipping auto-start.");
                return;
            }

            _cts = new CancellationTokenSource(TimeSpan.FromSeconds(START_TIMEOUT_SECONDS));

            var options = new LobbyCreateOptions
            {
                RoomName      = _roomName,
                Port          = _port,
                MaxPlayers    = _maxPlayers,
                InitialAiSlots = _maxAiSlots,
                IsPublic      = true,
                HostAsServer  = true,   // dedicated server: no local client
            };

            try
            {
                var handle = await LobbyServiceLocator.Transport.CreateLobbyAsync(options, _cts.Token);
                Debug.Log($"[DedicatedServer] Lobby created. ID={handle.PlatformId}  IP={handle.Address}:{handle.Port}");
            }
            catch (OperationCanceledException)
            {
                Debug.LogError("[DedicatedServer] Lobby creation timed out.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DedicatedServer] Failed to create lobby: {ex.Message}");
            }
        }

        private void OnDestroy() => _cts?.Cancel();
    }
}
