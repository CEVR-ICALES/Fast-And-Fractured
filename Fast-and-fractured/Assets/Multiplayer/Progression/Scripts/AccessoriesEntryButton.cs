using Enums;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer.Progression
{
    /// <summary>Opens the ACCESSORIES screen, which then returns to <see cref="returnTo"/>.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class AccessoriesEntryButton : MonoBehaviour
    {
        public ScreensType returnTo = ScreensType.PLAY_MODE_SELECTION;

        private void Awake() => GetComponent<Button>().onClick.AddListener(Open);

        private void Open()
        {
            AccessoriesScreen.ReturnTo = returnTo;
            if (MainMenuManager.HasInstance) MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.ACCESSORIES, 0.25f);
        }
    }
}
