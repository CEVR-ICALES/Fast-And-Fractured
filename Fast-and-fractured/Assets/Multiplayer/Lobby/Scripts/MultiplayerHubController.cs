using Assets.SimpleLocalization.Scripts;
using Enums;
using FastAndFractured;
using FastAndFractured.Multiplayer.Abstractions;
using FastAndFractured.Multiplayer.Abstractions.Tugboat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer.Lobby
{
    public sealed class MultiplayerHubController : MonoBehaviour
    {
        public const string KEY_STEAM = "MP.Network.Steam";
        public const string KEY_LAN = "MP.Network.Lan";
        public const string KEY_LAN_ONLY = "MP.Network.LanOnly";
        private const string NETWORK_BUTTON_NAME = "NetworkButton";
        private const float NETWORK_FONT_MAX = 26f;
        private const float NETWORK_FONT_MIN = 14f;
        private const float NETWORK_DARKEN = 0.45f;
        private static readonly Vector2 NETWORK_ANCHOR_MIN = new Vector2(0.3f, 0.78f);
        private static readonly Vector2 NETWORK_ANCHOR_MAX = new Vector2(0.7f, 0.85f);

        [SerializeField] private Button _hostButton;
        [SerializeField] private Button _browseButton;
        [SerializeField] private Button _directJoinButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private float  _fadeDuration = 0.5f;

        private TMP_Text _networkLabel;

        private void Awake()
        {
            _hostButton?.onClick.AddListener(()       => Go(ScreensType.MULTIPLAYER_HOST_SETUP));
            _browseButton?.onClick.AddListener(()     => Go(ScreensType.MULTIPLAYER_BROWSER));
            _directJoinButton?.onClick.AddListener(() => Go(ScreensType.MULTIPLAYER_DIRECT_JOIN));
            _backButton?.onClick.AddListener(()       => Go(BackTarget()));
            CreateNetworkButton();
        }

        private void OnEnable()
        {
            LobbyNetwork.Refresh();
            LobbyNetwork.Changed += ShowNetwork;
            LocalizationManager.OnLocalizationChanged += ShowNetwork;
            ShowNetwork();
        }

        private void OnDisable()
        {
            LobbyNetwork.Changed -= ShowNetwork;
            LocalizationManager.OnLocalizationChanged -= ShowNetwork;
        }

        /// <summary>Back returns to the Play mode screen when there is one (MainMenuPlayModesTool), else the main menu.</summary>
        private static ScreensType BackTarget() =>
            MainMenuManager.HasInstance && MainMenuManager.Instance.menuScreens.ContainsKey(ScreensType.PLAY_MODE_SELECTION)
                ? ScreensType.PLAY_MODE_SELECTION
                : ScreensType.MAIN_MENU;

        private void Go(ScreensType screen) =>
            MainMenuManager.Instance.TransitionBetweenScreens(screen, _fadeDuration);

        private void CreateNetworkButton()
        {
            if (_hostButton == null) return;
            GameObject clone = Instantiate(_hostButton.gameObject, _hostButton.transform.parent);
            clone.name = NETWORK_BUTTON_NAME;
            // Its label is written here: the copied LocalizedText would put the host text back.
            foreach (var localized in clone.GetComponentsInChildren<LocalizedText>(true))
                DestroyImmediate(localized);

            var rect = (RectTransform)clone.transform;
            rect.anchorMin = NETWORK_ANCHOR_MIN;
            rect.anchorMax = NETWORK_ANCHOR_MAX;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            if (clone.TryGetComponent(out Image background))
                background.color = Color.Lerp(background.color, Color.black, NETWORK_DARKEN);

            var button = clone.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(SwitchNetwork);

            _networkLabel = clone.GetComponentInChildren<TMP_Text>(true);
            if (_networkLabel == null) return;
            _networkLabel.enableAutoSizing = true;
            _networkLabel.fontSizeMax = NETWORK_FONT_MAX;
            _networkLabel.fontSizeMin = NETWORK_FONT_MIN;
        }

        private void SwitchNetwork()
        {
            if (LobbyNetwork.SteamAvailable)
                LobbyNetwork.Prefer(LobbyNetwork.Other(LobbyNetwork.Mode));
            else
                LobbyNetwork.Refresh();
            ShowNetwork();
        }

        public static string NetworkLabelKey(LobbyNetworkMode mode, bool steamAvailable)
        {
            if (mode == LobbyNetworkMode.Steam) return KEY_STEAM;
            return steamAvailable ? KEY_LAN : KEY_LAN_ONLY;
        }

        private void ShowNetwork()
        {
            if (_networkLabel == null) return;
            string key = NetworkLabelKey(LobbyNetwork.Mode, LobbyNetwork.SteamAvailable);
            _networkLabel.text = LocalizationManager.Localize(key, TugboatLobbyTransport.GetLocalIP());
        }
    }
}
