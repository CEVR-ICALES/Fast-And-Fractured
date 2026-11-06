using Assets.SimpleLocalization.Scripts;
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
            // Humans joining or leaving change the total too.
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

        /// <summary>A host migration spawns a new room object, and this peer may now host: bind to it when it appears.</summary>
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
                _countLabel.text = LocalizationManager.Localize("MP.Room.AiCount", _lobby.AiSlotCount.Value, _lobby.TotalSlotCount, NetworkedLobbyManager.MAX_PLAYERS);

            if (_decButton != null) _decButton.interactable = isHost && _lobby.AiSlotCount.Value > 0;
            if (_incButton != null) _incButton.interactable = isHost && _lobby.FreeSlotCount > 0;
        }
    }
}
