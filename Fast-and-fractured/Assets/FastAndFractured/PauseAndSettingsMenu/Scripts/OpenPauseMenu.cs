using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities.Managers.PauseSystem;
using Enums;
namespace FastAndFractured
{
    public class OpenPauseMenu : MonoBehaviour
    {
        [SerializeField] private ScreensType pauseMenu;
        [SerializeField] private float fadeDuration = 0.8f;
        public void ResumeGame()
        {
            MainMenuManager.Instance.CloseScreen();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;    
        }
        public void PauseGame()
        {
            MainMenuManager.Instance.TransitionBetweenScreens(pauseMenu, -1);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
