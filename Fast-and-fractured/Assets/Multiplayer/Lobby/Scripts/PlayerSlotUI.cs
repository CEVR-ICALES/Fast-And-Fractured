using FastAndFractured.Multiplayer.Lobby;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Visual slot for a single player in the LOBBY_ROOM player list.
    /// Renders:
    ///   - character icon  (Resources/PlayerIcons/&lt;code&gt;.png  e.g. Pepe_0)
    ///   - player name + ready badge
    ///   - voted map thumbnail (MapCatalog.Entry.Thumbnail)
    ///   - SELECT CHARACTER &amp; MAP button (visible only on the local player's slot)
    /// LobbyReadyUI instantiates this prefab once per known clientId.
    /// </summary>
    public sealed class PlayerSlotUI : MonoBehaviour
    {
        [Header("Character")]
        [SerializeField] private Image      _charIcon;
        [SerializeField] private Sprite     _emptyCharIcon;
        [SerializeField] private TMP_Text   _nameLabel;

        [Header("Ready state")]
        [SerializeField] private GameObject _readyBadge;
        [SerializeField] private TMP_Text   _readyLabel;
        [SerializeField] private Image      _backgroundTint;
        [SerializeField] private Color      _notReadyColor = new Color(0.15f, 0.15f, 0.15f, 0.80f);
        [SerializeField] private Color      _readyColor    = new Color(0.15f, 0.40f, 0.15f, 0.80f);

        [Header("Map vote")]
        [SerializeField] private Image      _mapVoteThumbnail;
        [SerializeField] private GameObject _mapVoteContainer;   // hidden when no vote

        [Header("Local-only")]
        [SerializeField] private Button     _selectButton;
        [SerializeField] private GameObject _selectButtonRoot;   // hidden on remote slots

        /// <summary>
        /// Populates the slot for one player.
        /// </summary>
        /// <param name="clientId">For displaying "Player N".</param>
        /// <param name="characterCode">e.g. "Pepe_0". Empty/null = unselected.</param>
        /// <param name="isReady">Ready state.</param>
        /// <param name="mapVoteThumbnail">Sprite from MapCatalog.Entry.Thumbnail. Null = no vote yet.</param>
        /// <param name="isLocal">True for the slot belonging to the local connection — shows the action button.</param>
        public void SetData(int clientId, string characterCode, bool isReady, Sprite mapVoteThumbnail, bool isLocal, string displayName = null)
        {
            // AI slots use negative ids and have no connection to show.
            _networkClientId = clientId;
            _networkVersion = -1;
            string playerName = string.IsNullOrEmpty(displayName) ? $"Player {clientId}" : displayName;

            if (_nameLabel != null)
                _nameLabel.text = string.IsNullOrEmpty(characterCode)
                    ? playerName
                    : $"{playerName}  ·  {characterCode}";

            if (_charIcon != null)
            {
                Sprite icon = string.IsNullOrEmpty(characterCode)
                    ? _emptyCharIcon
                    : Resources.Load<Sprite>("PlayerIcons/" + characterCode);
                _charIcon.sprite  = icon != null ? icon : _emptyCharIcon;
                _charIcon.enabled = _charIcon.sprite != null;
            }

            if (_readyBadge != null) _readyBadge.SetActive(isReady);
            if (_readyLabel != null) _readyLabel.text = isReady ? "READY" : "...";
            if (_backgroundTint != null) _backgroundTint.color = isReady ? _readyColor : _notReadyColor;

            if (_mapVoteThumbnail != null) _mapVoteThumbnail.sprite = mapVoteThumbnail;
            if (_mapVoteContainer != null) _mapVoteContainer.SetActive(mapVoteThumbnail != null);

            if (_selectButtonRoot != null) _selectButtonRoot.SetActive(isLocal);
        }

        private static readonly Color InMatchColor = new Color(0.45f, 0.30f, 0.08f, 0.85f);

        /// <summary>
        /// While a match runs, marks the players in it (the rest are waiting in the room). Call
        /// after <see cref="SetData"/>.
        /// </summary>
        public void SetInMatch(bool inMatch)
        {
            if (!inMatch) return;
            if (_readyBadge != null) _readyBadge.SetActive(true);
            if (_readyLabel != null)
            {
                // The badge was sized for "READY": shrink to fit on one line.
                if (!_readyLabel.enableAutoSizing)
                {
                    _readyLabel.enableWordWrapping = false;
                    _readyLabel.fontSizeMax = _readyLabel.fontSize;
                    _readyLabel.fontSizeMin = 6f;
                    _readyLabel.enableAutoSizing = true;
                }
                _readyLabel.text = "IN MATCH";
            }
            if (_backgroundTint != null) _backgroundTint.color = InMatchColor;
            if (_selectButtonRoot != null) _selectButtonRoot.SetActive(false);
        }

        // ── Connection (Wi-Fi / cable, ping to the host) ──────────────────────────────
        private int _networkClientId = -1;
        private int _networkVersion = -1;
        private NetworkSignalIndicator _signal;

        private void Update()
        {
            if (_networkVersion == NetworkQuality.Version) return;
            _networkVersion = NetworkQuality.Version;

            bool human = _networkClientId >= 0;
            if (!human)
            {
                if (_signal != null) _signal.gameObject.SetActive(false);
                return;
            }
            if (_signal == null) _signal = CreateSignal();
            _signal.gameObject.SetActive(true);

            if (NetworkQuality.TryGet(_networkClientId, out var report))
                _signal.Set((LinkKind)report.Link, report.PingMs, report.IsHost ? "HOST" : null);
            else
                _signal.Set(LinkKind.Unknown, -1);
        }

        /// <summary>Top-right corner of the slot, outside its layout.</summary>
        private NetworkSignalIndicator CreateSignal()
        {
            float height = _nameLabel != null ? Mathf.Clamp(_nameLabel.fontSize * 0.9f, 20f, 28f) : 22f;
            var signal = NetworkSignalIndicator.Create(transform, _nameLabel, height);
            var rect = (RectTransform)signal.transform;
            rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-8f, -6f);
            return signal;
        }

        /// <summary>
        /// Returns the SELECT CHARACTER button so the lobby UI can wire its onClick.
        /// </summary>
        public Button GetSelectButton() => _selectButton;
    }
}
