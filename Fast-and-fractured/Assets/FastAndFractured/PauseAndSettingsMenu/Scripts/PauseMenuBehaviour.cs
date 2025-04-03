using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities.Managers.PauseSystem;
namespace FastAndFractured
{
    public class PauseMenuBehaviour : MonoBehaviour
    {
        [SerializeField] private GameObject pauseMenuUI;
        [SerializeField] private GameObject settingsMenuUI;
        void Start()
        {
            pauseMenuUI.SetActive(false);
        }
        public void ResumeGame()
        {
            PauseManager.Instance.ResumeGame();
            pauseMenuUI.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;    
        }
        public void ReturnToMenu()
        {
            //TODO return to main menu when done
        }
        public void OpenSettings()
        {
            settingsMenuUI.SetActive(true);
            pauseMenuUI.SetActive(false);
        }
        public void PauseGame()
        {
            pauseMenuUI.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
