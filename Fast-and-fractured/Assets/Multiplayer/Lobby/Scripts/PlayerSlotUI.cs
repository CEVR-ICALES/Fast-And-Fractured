using Assets.SimpleLocalization.Scripts;
using FastAndFractured.Multiplayer.Lobby;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer
{
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

        public void SetData(int clientId, string characterCode, bool isReady, Sprite mapVoteThumbnail, bool isLocal, string displayName = null, int level = 0, int wins = -1)
        {
            // AI slots use negative ids and have no connection to show.
            _networkClientId = clientId;
            _networkVersion = -1;
            string playerName = string.IsNullOrEmpty(displayName) ? $"Player {clientId}" : displayName;
            if (level > 0) playerName = $"{playerName}  <color=#FFC759>{LocalizationManager.Localize("MP.Room.Level", level)}</color>";
            if (wins >= 0) playerName = $"{playerName}  <nobr><color=#7CE38B>{LocalizationManager.Localize(wins == 1 ? "MP.Room.Win" : "MP.Room.Wins", wins)}</color></nobr>";

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
            if (_readyLabel != null) _readyLabel.text = isReady ? LocalizationManager.Localize("MP.Room.Ready") : "...";
            if (_backgroundTint != null) _backgroundTint.color = isReady ? _readyColor : _notReadyColor;

            if (_mapVoteThumbnail != null) _mapVoteThumbnail.sprite = mapVoteThumbnail;
            if (_mapVoteContainer != null) _mapVoteContainer.SetActive(mapVoteThumbnail != null);

            if (_selectButtonRoot != null) _selectButtonRoot.SetActive(isLocal);
            SetLocalMarker(isLocal);
        }

        private static readonly Color YouColor = new Color(1f, 0.78f, 0.22f, 1f);
        private static readonly Color YouTextColor = new Color(0.12f, 0.08f, 0.04f, 1f);
        private static readonly Vector2 YouTagSize = new Vector2(58f, 22f);
        private static readonly Vector2 YouTagOffset = new Vector2(12f, -6f);
        private const float YOU_ARROW_SIZE = 12f;
        private const float YOU_FONT_SIZE = 15f;
        private const float YOU_OUTLINE_WIDTH = 3f;
        private GameObject _youTag;
        private Outline _youOutline;

        private void SetLocalMarker(bool isLocal)
        {
            if (!isLocal)
            {
                if (_youTag != null) _youTag.SetActive(false);
                if (_youOutline != null) _youOutline.enabled = false;
                return;
            }
            if (_youTag == null) _youTag = CreateYouTag();
            _youTag.SetActive(true);
            _youTag.transform.SetAsLastSibling();
            if (_backgroundTint != null)
            {
                if (_youOutline == null) _youOutline = _backgroundTint.gameObject.AddComponent<Outline>();
                _youOutline.effectColor = YouColor;
                _youOutline.effectDistance = new Vector2(YOU_OUTLINE_WIDTH, -YOU_OUTLINE_WIDTH);
                _youOutline.enabled = true;
            }
        }

        private GameObject CreateYouTag()
        {
            var go = new GameObject("YouTag", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = YouTagOffset;
            rect.sizeDelta = YouTagSize;

            var arrow = new GameObject("Arrow", typeof(RectTransform));
            var arrowRect = (RectTransform)arrow.transform;
            arrowRect.SetParent(rect, false);
            arrowRect.anchorMin = arrowRect.anchorMax = new Vector2(0.5f, 0f);
            arrowRect.sizeDelta = new Vector2(YOU_ARROW_SIZE, YOU_ARROW_SIZE);
            arrowRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var arrowImage = arrow.AddComponent<Image>();
            arrowImage.color = YouColor;
            arrowImage.raycastTarget = false;

            var pill = go.AddComponent<Image>();
            pill.color = YouColor;
            pill.raycastTarget = false;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            if (_nameLabel != null) label.font = _nameLabel.font;
            label.fontSize = YOU_FONT_SIZE;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = YouTextColor;
            label.raycastTarget = false;
            label.text = LocalizationManager.Localize("MP.Room.You");
            return go;
        }

        private static readonly Color InMatchColor = new Color(0.45f, 0.30f, 0.08f, 0.85f);
        private const float MIN_BADGE_FONT_SIZE = 6f;
        private const float SIGNAL_HEIGHT_PER_FONT_SIZE = 0.9f;
        private const float MIN_SIGNAL_HEIGHT = 20f;
        private const float MAX_SIGNAL_HEIGHT = 28f;
        private const float DEFAULT_SIGNAL_HEIGHT = 22f;

        /// <summary>While a match runs, marks the players in it. Call after <see cref="SetData"/>.</summary>
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
                    _readyLabel.fontSizeMin = MIN_BADGE_FONT_SIZE;
                    _readyLabel.enableAutoSizing = true;
                }
                _readyLabel.text = LocalizationManager.Localize("MP.Room.InMatch");
            }
            if (_backgroundTint != null) _backgroundTint.color = InMatchColor;
            if (_selectButtonRoot != null) _selectButtonRoot.SetActive(false);
        }

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
                _signal.Set((LinkKind)report.Link, report.PingMs, report.IsHost ? LocalizationManager.Localize("MP.Host") : null);
            else
                _signal.Set(LinkKind.Unknown, -1);
        }

        /// <summary>Top-right corner of the slot, outside its layout.</summary>
        private NetworkSignalIndicator CreateSignal()
        {
            float height = _nameLabel != null ? Mathf.Clamp(_nameLabel.fontSize * SIGNAL_HEIGHT_PER_FONT_SIZE, MIN_SIGNAL_HEIGHT, MAX_SIGNAL_HEIGHT) : DEFAULT_SIGNAL_HEIGHT;
            var signal = NetworkSignalIndicator.Create(transform, _nameLabel, height);
            var rect = (RectTransform)signal.transform;
            rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-8f, -6f);
            return signal;
        }

        public Button GetSelectButton() => _selectButton;

        private static readonly Color ReservedColor = new Color(0.12f, 0.20f, 0.36f, 0.80f);
        private static readonly Color KickColor = new Color(0.78f, 0.18f, 0.16f, 0.95f);
        private static readonly Vector2 KickSize = new Vector2(30f, 30f);
        private const float KICK_FONT_SIZE = 20f;
        private Button _kickButton;

        public void SetReserved()
        {
            if (_readyBadge != null) _readyBadge.SetActive(false);
            if (_readyLabel != null) _readyLabel.text = string.Empty;
            if (_backgroundTint != null) _backgroundTint.color = ReservedColor;
            if (_charIcon != null) _charIcon.enabled = false;
        }

        public void SetKick(System.Action onKick)
        {
            if (onKick == null)
            {
                if (_kickButton != null) _kickButton.gameObject.SetActive(false);
                return;
            }
            if (_kickButton == null) _kickButton = CreateKickButton();
            _kickButton.gameObject.SetActive(true);
            _kickButton.onClick.RemoveAllListeners();
            _kickButton.onClick.AddListener(() => onKick());
        }

        // Top-left corner of the seat, outside its layout (the network signal holds the top-right one).
        private Button CreateKickButton()
        {
            var go = new GameObject("KickButton", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(4f, -4f);
            rect.sizeDelta = KickSize;
            var image = go.AddComponent<Image>();
            image.color = KickColor;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            if (_nameLabel != null) label.font = _nameLabel.font;
            label.fontSize = KICK_FONT_SIZE;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = "X";
            return button;
        }
    }
}
