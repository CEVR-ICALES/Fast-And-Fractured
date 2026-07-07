using System.Collections;
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
            //MainMenuManager.Instance.ChangeScreen(screenToOpen);
            MainMenuManager.Instance.TransitionBetweenScreens(screenToOpen, fadeDuration);
        }
    }

}
