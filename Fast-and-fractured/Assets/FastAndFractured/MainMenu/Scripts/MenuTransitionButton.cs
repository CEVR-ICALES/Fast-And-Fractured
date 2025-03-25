using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
