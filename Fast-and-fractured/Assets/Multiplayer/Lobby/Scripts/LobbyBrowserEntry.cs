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
            SetPing(listing.PingMs > 0 ? listing.PingMs : -1);
        }

        public LobbyListing Listing => _listing;

        /// <summary>Host's link + round trip to the room; -1 while not measured / unreachable.</summary>
        public void SetPing(int pingMs, string overrideText = null)
        {
            if (_pingText == null || _listing == null) return;
            if (_signal == null)
            {
                MakeRoomForSignal();
                // Sits inside the old ping label's rect, so it lands wherever the prefab put it.
                _signal = FastAndFractured.Multiplayer.NetworkSignalIndicator.Create(
                    _pingText.transform, _pingText, Mathf.Clamp(_pingText.fontSize * 0.9f, 18f, 26f));
                var rect = (RectTransform)_signal.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
            }
            _pingText.text = string.Empty;
            _signal.Set(_listing.HostLink, pingMs, overrideText);
        }

        private FastAndFractured.Multiplayer.NetworkSignalIndicator _signal;

        /// <summary>
        /// The prefab's ping column was sized for "45ms" (32 px of a 400 px row); the icon and
        /// the bars need more. Widen the row and re-split the columns (anchors are relative).
        /// </summary>
        private void MakeRoomForSignal()
        {
            var root = (RectTransform)transform;
            if (root.rect.width < 560f) root.sizeDelta = new Vector2(root.sizeDelta.x + (560f - root.rect.width), root.sizeDelta.y);
            SetColumn(_roomNameText, 0.00f, 0.45f);
            SetColumn(_playersText,  0.45f, 0.57f);
            SetColumn(_pingText,     0.58f, 0.85f);
        }

        private static void SetColumn(Component column, float from, float to)
        {
            if (column == null) return;
            var rect = (RectTransform)column.transform;
            rect.anchorMin = new Vector2(from, rect.anchorMin.y);
            rect.anchorMax = new Vector2(to, rect.anchorMax.y);
        }

        public void SetJoinable(bool joinable)
        {
            if (_joinButton != null) _joinButton.interactable = joinable;
        }

        /// <summary>Tags a room that is playing a match; it can still be joined to wait for the next one.</summary>
        public void SetInMatch(bool inMatch)
        {
            if (_roomNameText == null || _listing == null) return;
            _roomNameText.richText = true;
            _roomNameText.text = inMatch
                ? $"{_listing.RoomName}  <size=70%><color=#F5B041>IN A MATCH</color></size>"
                : _listing.RoomName;
        }
    }
}
