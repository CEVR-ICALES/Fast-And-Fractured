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
    public sealed class MultiplayerDirectJoinController : MonoBehaviour
    {
        private const ushort DEFAULT_PORT = 7777;
        private const int JOIN_TIMEOUT_SECONDS = 10;

        [SerializeField] private TMP_InputField _ipInput;
        [SerializeField] private TMP_InputField _portInput;
        [SerializeField] private Button         _joinButton;
        [SerializeField] private Button         _backButton;
        [SerializeField] private TMP_Text       _statusLabel;
        [SerializeField] private float          _fadeDuration = 0.5f;

        private bool _busy;
        private CancellationTokenSource _cts;
        private SearchingIndicator _connectingIndicator;

        private void Awake()
        {
            if (_statusLabel != null) _connectingIndicator = SearchingIndicator.Create(_statusLabel);
            if (_portInput != null) _portInput.text = "7777";
            _joinButton?.onClick.AddListener(OnJoinClicked);
            _backButton?.onClick.AddListener(() => MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.MULTIPLAYER_HUB, _fadeDuration));
        }

        private void OnDestroy() => _cts?.Cancel();

        private async void OnJoinClicked()
        {
            if (_busy) return;
            _busy = true;
            SetInteractable(false);
            SetStatus(LocalizationManager.Localize("MP.Connecting"));
            SetConnecting(true);

            string ip = _ipInput != null && !string.IsNullOrWhiteSpace(_ipInput.text)
                ? _ipInput.text.Trim()
                : "localhost";

            if (!ushort.TryParse(_portInput?.text, out ushort port))
                port = DEFAULT_PORT;

            _cts = new CancellationTokenSource(TimeSpan.FromSeconds(JOIN_TIMEOUT_SECONDS));
            try
            {
                bool ok = await LobbyServiceLocator.Transport.JoinLobbyAsync(
                    LobbyHandle.FromAddress(ip, port), _cts.Token);

                if (ok)
                {
                    SetStatus(string.Empty);
                    MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.LOBBY_ROOM, _fadeDuration);
                }
                else
                    SetStatus(LocalizationManager.Localize("MP.Join.Failed"));
            }
            catch (OperationCanceledException) { SetStatus(LocalizationManager.Localize("MP.ConnectionTimedOut")); }
            catch (Exception ex)               { SetStatus(LocalizationManager.Localize("MP.Error", ex.Message)); }
            finally
            {
                _busy = false;
                SetConnecting(false);
                SetInteractable(true);
            }
        }

        private void SetConnecting(bool connecting)
        {
            if (_connectingIndicator == null) return;
            if (connecting) _connectingIndicator.FollowText(_statusLabel);
            _connectingIndicator.gameObject.SetActive(connecting);
        }

        private void SetStatus(string msg)
        {
            if (_statusLabel != null) _statusLabel.text = msg;
        }

        private void SetInteractable(bool value)
        {
            if (_joinButton != null) _joinButton.interactable = value;
            if (_backButton != null) _backButton.interactable = value;
        }
    }
}
