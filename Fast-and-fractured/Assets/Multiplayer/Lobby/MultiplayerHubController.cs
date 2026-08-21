// SCENE SETUP:
//   - Place on the MULTIPLAYER_HUB MenuScreen GameObject.
//   - Wire _hostButton      → navigates to MULTIPLAYER_HOST_SETUP
//   - Wire _browseButton    → navigates to MULTIPLAYER_BROWSER
//   - Wire _directJoinButton → navigates to MULTIPLAYER_DIRECT_JOIN
//   - Wire _backButton      → navigates to MAIN_MENU
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
            _backButton?.onClick.AddListener(()       => Go(ScreensType.MAIN_MENU));
        }

        private void Go(ScreensType screen) =>
            MainMenuManager.Instance.TransitionBetweenScreens(screen, _fadeDuration);
    }
}
