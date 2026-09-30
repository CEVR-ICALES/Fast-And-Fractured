using System;
using System.Threading;
using FastAndFractured.Multiplayer.Abstractions;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Auto-starts the FishNet server and creates a Steam lobby when running as a dedicated server.
    /// Place this GameObject in the multiplayer scene (or a persistent bootstrap scene).
    ///
    /// Activate only in dedicated-server builds: either use a separate scene or check
    /// Application.isBatchMode (headless builds launched with -batchmode set this to true).
    /// </summary>
    public sealed class DedicatedServerBootstrap : MonoBehaviour
    {
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

            _cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

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
