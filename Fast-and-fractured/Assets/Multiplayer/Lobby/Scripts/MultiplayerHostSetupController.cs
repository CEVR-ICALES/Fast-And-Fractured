// SCENE SETUP:
//   - Place on the MULTIPLAYER_HOST_SETUP MenuScreen GameObject.
//   - Wire _roomNameInput  (TMP_InputField)
//   - Wire _portInput      (TMP_InputField, default "7777")
//   - Wire _startButton    (Button, begins hosting)
//   - Wire _backButton     (Button)
//   - Wire _statusLabel    (TMP_Text, shows errors)
//
// AI slot count is configured from the lobby ready screen (AiSlotsUI), not here.
using Assets.SimpleLocalization.Scripts;
using System;
using System.Threading;
using Enums;
using FastAndFractured;
using FastAndFractured.Multiplayer.Abstractions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer.Lobby
{
    public sealed class MultiplayerHostSetupController : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _roomNameInput;
        [SerializeField] private TMP_InputField _portInput;
        [SerializeField] private Button         _startButton;
        [SerializeField] private Button         _backButton;
        [SerializeField] private TMP_Text       _statusLabel;
        [SerializeField] private float          _fadeDuration = 0.5f;

        private bool _busy;
        private CancellationTokenSource _cts;

        private void Awake()
        {
            if (_portInput != null) _portInput.text = "7777";
            _startButton?.onClick.AddListener(OnStartClicked);
            _backButton?.onClick.AddListener(() => MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.MULTIPLAYER_HUB, _fadeDuration));
        }

        private void OnDestroy() => _cts?.Cancel();

        private async void OnStartClicked()
        {
            if (_busy) return;
            _busy = true;
            SetInteractable(false);
            SetStatus(string.Empty);

            ushort port = 7777;
            if (_portInput != null && !ushort.TryParse(_portInput.text, out port))
            {
                SetStatus(LocalizationManager.Localize("MP.HostSetup.InvalidPort"));
                SetInteractable(true);
                _busy = false;
                return;
            }

            var options = new LobbyCreateOptions
            {
                RoomName      = _roomNameInput != null && !string.IsNullOrWhiteSpace(_roomNameInput.text)
                                    ? _roomNameInput.text.Trim()
                                    : LocalizationManager.Localize("MP.HostSetup.DefaultRoom", Environment.UserName),
                Port          = port,
                InitialAiSlots = 0,
                IsPublic      = true,
            };

            _cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await LobbyServiceLocator.Transport.CreateLobbyAsync(options, _cts.Token);

                // Propagate the room name to the lobby manager. AI count is configured later
                // from the lobby ready screen — no need to push it here.
                ApplyHostChoicesToLobby(options.RoomName);

                MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.LOBBY_ROOM, _fadeDuration);
            }
            catch (OperationCanceledException)
            {
                SetStatus(LocalizationManager.Localize("MP.ConnectionTimedOut"));
                SetInteractable(true);
            }
            catch (Exception ex)
            {
                SetStatus(LocalizationManager.Localize("MP.Error", ex.Message));
                SetInteractable(true);
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>
        /// After the server is up, push the host's room name onto the lobby manager. We poll
        /// because the lobby manager is a NetworkBehaviour and may not be spawned the instant
        /// CreateLobbyAsync returns.
        /// </summary>
        private async void ApplyHostChoicesToLobby(string roomName)
        {
            const int MAX_WAIT_MS = 5000;
            int waited = 0;
            NetworkedLobbyManager lobby = null;
            while (waited < MAX_WAIT_MS)
            {
                lobby = LobbyLocator.GetLobbyManager();
                if (lobby != null && lobby.IsServerInitialized) break;
                await System.Threading.Tasks.Task.Delay(100);
                waited += 100;
            }

            if (lobby == null || !lobby.IsServerInitialized)
            {
                Debug.LogWarning("[MultiplayerHostSetupController] Could not find NetworkedLobbyManager after " +
                                 $"{MAX_WAIT_MS}ms. Room name = '{roomName}' NOT applied. " +
                                 "Ensure a NetworkedLobbyManager exists as a child of the NetworkManager.");
                return;
            }

            Debug.Log($"[MultiplayerHostSetupController] Applying host choices: room='{roomName}'.");
            lobby.ApplyRoomName(roomName);
        }

        private void SetStatus(string msg)
        {
            if (_statusLabel != null) _statusLabel.text = msg;
        }

        private void SetInteractable(bool value)
        {
            if (_startButton != null) _startButton.interactable = value;
            if (_backButton  != null) _backButton.interactable  = value;
        }
    }
}
