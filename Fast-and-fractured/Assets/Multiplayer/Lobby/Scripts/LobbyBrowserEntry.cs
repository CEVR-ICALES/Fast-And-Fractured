using FastAndFractured.Multiplayer.Abstractions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer.Lobby
{
    public sealed class LobbyBrowserEntry : MonoBehaviour
    {
        [SerializeField] private TMP_Text _roomNameText;
        [SerializeField] private TMP_Text _playersText;
        [SerializeField] private TMP_Text _pingText;
        [SerializeField] private Button   _joinButton;

        private LobbyListing _listing;
        private System.Action<LobbyListing> _onJoin;

        private void Awake() =>
            _joinButton?.onClick.AddListener(() => _onJoin?.Invoke(_listing));

        public void Populate(LobbyListing listing, System.Action<LobbyListing> onJoin)
        {
            _listing = listing;
            _onJoin  = onJoin;

            if (_roomNameText != null) _roomNameText.text = listing.RoomName;
            if (_playersText  != null) _playersText.text  = $"{listing.CurrentPlayers}/{listing.MaxPlayers}";
            if (_pingText     != null) _pingText.text      = listing.PingMs > 0 ? $"{listing.PingMs}ms" : "--";
        }

        public void SetJoinable(bool joinable)
        {
            if (_joinButton != null) _joinButton.interactable = joinable;
        }
    }
}
