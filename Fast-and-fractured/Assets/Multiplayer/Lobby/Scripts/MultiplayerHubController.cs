using Enums;
using FastAndFractured;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer.Lobby
{
    public sealed class MultiplayerHubController : MonoBehaviour
    {
        [SerializeField] private Button _hostButton;
        [SerializeField] private Button _browseButton;
        [SerializeField] private Button _directJoinButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private float  _fadeDuration = 0.5f;

        private void Awake()
        {
            _hostButton?.onClick.AddListener(()       => Go(ScreensType.MULTIPLAYER_HOST_SETUP));
            _browseButton?.onClick.AddListener(()     => Go(ScreensType.MULTIPLAYER_BROWSER));
            _directJoinButton?.onClick.AddListener(() => Go(ScreensType.MULTIPLAYER_DIRECT_JOIN));
            _backButton?.onClick.AddListener(()       => Go(BackTarget()));
        }

        /// <summary>Back returns to the Play mode screen when there is one (MainMenuPlayModesTool), else the main menu.</summary>
        private static ScreensType BackTarget() =>
            MainMenuManager.HasInstance && MainMenuManager.Instance.menuScreens.ContainsKey(ScreensType.PLAY_MODE_SELECTION)
                ? ScreensType.PLAY_MODE_SELECTION
                : ScreensType.MAIN_MENU;

        private void Go(ScreensType screen) =>
            MainMenuManager.Instance.TransitionBetweenScreens(screen, _fadeDuration);
    }
}
