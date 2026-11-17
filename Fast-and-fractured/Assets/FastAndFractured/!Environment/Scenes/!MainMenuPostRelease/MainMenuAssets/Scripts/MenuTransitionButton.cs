using System.Collections.Generic;
using UnityEngine;
using Enums;

namespace FastAndFractured
{
    public class MenuTransitionButton : MonoBehaviour
    {
        public ScreensType screenToOpen;
        public float fadeDuration = 0.8f;

        public void OpenScreen()
        {
            var menu = MainMenuManager.ExistingInstance;
            if (menu == null) return;
            var destination = ResolveDestination(screenToOpen, menu.menuScreens);
            menu.TransitionBetweenScreens(destination, fadeDuration);
        }

        private static ScreensType ResolveDestination(ScreensType requested, IReadOnlyDictionary<ScreensType, MenuScreen> screens)
        {
            if (requested == ScreensType.MAIN_MENU && !screens.ContainsKey(ScreensType.MAIN_MENU) && screens.ContainsKey(ScreensType.PAUSE))
                return ScreensType.PAUSE;
            return requested;
        }
    }
}
