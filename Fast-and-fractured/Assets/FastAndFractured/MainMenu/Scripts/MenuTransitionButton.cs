using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enums;

namespace FastAndFractured
{
    public class MenuTransitionButton : MonoBehaviour
    {
        public ScreensType screenToOpen;
        public void OpenScreen()
        {
            MainMenuManager.Instance.ChangeScreen(screenToOpen);
        }
    }

}
