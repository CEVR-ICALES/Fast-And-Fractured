// SCENE SETUP:
//   - Place anywhere inside the LOBBY_ROOM MenuScreen.
//   - Wire _countLabel  (TMP_Text)
//   - Wire _decButton   (Button, −)
//   - Wire _incButton   (Button, +)
//   - Panel is shown to all but buttons are only interactable for the host.
using FastAndFractured.Multiplayer.Abstractions;
using FishNet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer.Lobby
{
    public sealed class AiSlotsUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text _countLabel;
        [SerializeField] private Button   _decButton;
        [SerializeField] private Button   _incButton;

        private NetworkedLobbyManager _lobby;

        private void OnEnable()
        {
            _lobby = LobbyLocator.GetLobbyManager();
            if (_lobby == null) return;

            _lobby.AiSlotCount.OnChange += OnAiCountChanged;
            // Also refresh when a human joins/leaves — otherwise the "Total: X/8" label
            // stays stale until someone touches the AI counter.
            _lobby.PlayerReadyStates.OnChange += OnReadyStatesChanged;
            _decButton?.onClick.AddListener(() => ChangeAi(-1));
            _incButton?.onClick.AddListener(() => ChangeAi(+1));

            RefreshUI();
        }

        private void OnDisable()
        {
            if (_lobby != null)
            {
                _lobby.AiSlotCount.OnChange -= OnAiCountChanged;
                _lobby.PlayerReadyStates.OnChange -= OnReadyStatesChanged;
            }

            _decButton?.onClick.RemoveAllListeners();
            _incButton?.onClick.RemoveAllListeners();
        }

        /// <summary>
        /// The room is a new object after a host migration (the new host spawns its own), and
        /// this peer may now be the host: bind again as soon as it appears.
        /// </summary>
        private void Update()
        {
            if (_lobby != null || LobbyLocator.GetLobbyManager() == null) return;
            OnDisable();
            OnEnable();
        }

        private void OnReadyStatesChanged(FishNet.Object.Synchronizing.SyncDictionaryOperation op,
                                          int key, bool value, bool asServer) => RefreshUI();

        private void ChangeAi(int delta)
        {
            if (_lobby == null) return;
            _lobby.ServerSetAiSlotCount(_lobby.AiSlotCount.Value + delta);
        }

        private void OnAiCountChanged(int prev, int next, bool asServer) => RefreshUI();

        private void RefreshUI()
        {
            if (_lobby == null) return;

            bool isHost = InstanceFinder.IsServerStarted;

            if (_countLabel != null)
                _countLabel.text = $"AI: {_lobby.AiSlotCount.Value}  |  Total: {_lobby.TotalSlotCount}/{NetworkedLobbyManager.MAX_PLAYERS}";

            if (_decButton != null) _decButton.interactable = isHost && _lobby.AiSlotCount.Value > 0;
            if (_incButton != null) _incButton.interactable = isHost && _lobby.FreeSlotCount > 0;
        }
    }
}
