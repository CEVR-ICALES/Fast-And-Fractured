// SCENE SETUP:
//   - Place on the MULTIPLAYER_DIRECT_JOIN MenuScreen GameObject.
//   - Wire _ipInput     (TMP_InputField, placeholder "localhost")
//   - Wire _portInput   (TMP_InputField, default "7777")
//   - Wire _joinButton  (Button)
//   - Wire _backButton  (Button)
//   - Wire _statusLabel (TMP_Text)
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
        [SerializeField] private TMP_InputField _ipInput;
        [SerializeField] private TMP_InputField _portInput;
        [SerializeField] private Button         _joinButton;
        [SerializeField] private Button         _backButton;
        [SerializeField] private TMP_Text       _statusLabel;
        [SerializeField] private float          _fadeDuration = 0.5f;

        private bool _busy;
        private CancellationTokenSource _cts;

        private void Awake()
        {
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
            SetStatus(string.Empty);

            string ip = _ipInput != null && !string.IsNullOrWhiteSpace(_ipInput.text)
                ? _ipInput.text.Trim()
                : "localhost";

            if (!ushort.TryParse(_portInput?.text, out ushort port))
                port = 7777;

            _cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                bool ok = await LobbyServiceLocator.Transport.JoinLobbyAsync(
                    LobbyHandle.FromAddress(ip, port), _cts.Token);

                if (ok)
                    MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.LOBBY_ROOM, _fadeDuration);
                else
                    SetStatus(LocalizationManager.Localize("MP.Join.Failed"));
            }
            catch (OperationCanceledException) { SetStatus(LocalizationManager.Localize("MP.ConnectionTimedOut")); }
            catch (Exception ex)               { SetStatus(LocalizationManager.Localize("MP.Error", ex.Message)); }
            finally
            {
                _busy = false;
                SetInteractable(true);
            }
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
