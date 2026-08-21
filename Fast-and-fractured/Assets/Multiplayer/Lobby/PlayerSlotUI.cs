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

        /// <summary>
        /// Returns the SELECT CHARACTER button so the lobby UI can wire its onClick.
        /// </summary>
        public Button GetSelectButton() => _selectButton;
    }
}
