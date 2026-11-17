using Assets.SimpleLocalization.Scripts;
using Enums;
using FastAndFractured.Multiplayer.Abstractions;

namespace FastAndFractured.Multiplayer
{
    public static class RoomKick
    {
        private const float SCREEN_TRANSITION_SECONDS = 0.5f;

        public static async void LeaveKicked()
        {
            // Without this the watchdog would read the stop as a lost host and try to take the room over.
            HostDisconnectWatchdog.NotifyIntentionalLeave();
            ConnectionLostNotifier.Show(LocalizationManager.Localize("MP.Room.Kicked"));

            try { await LobbyServiceLocator.Transport.LeaveLobbyAsync(); }
            catch { /* the server cuts the connection anyway */ }

            if (MainMenuManager.HasInstance)
                MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.MULTIPLAYER_HUB, SCREEN_TRANSITION_SECONDS);
            else
                MultiplayerSceneTeardown.LoadMainMenu();
        }
    }
}
