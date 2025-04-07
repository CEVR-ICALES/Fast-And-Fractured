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
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;    
        }
        public void PauseGame()
        {
            MainMenuManager.Instance.TransitionBetweenScreens(pauseMenu, fadeDuration);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
