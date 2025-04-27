using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Utilities;
using FastAndFractured;
using Enums;  

namespace Utilities.Managers.PauseSystem
{
   
    public class PauseManager : AbstractSingleton<PauseManager>
    {
        private bool _isGamePaused = false;
        private readonly List<IPausable> _pausableComponents = new List<IPausable>(); 
        public UnityEvent onGamePaused;
        public UnityEvent onGameResumed;
        public bool IsGamePaused => _isGamePaused;  

        #region Pause_Control

        
        public void PauseGame()
        {
            if (_isGamePaused) return;  

            _isGamePaused = true;
            //Time.timeScale = 0f;
            MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.PAUSE, -1);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            onGamePaused?.Invoke();
            foreach (var pausable in _pausableComponents)
            {
                pausable?.OnPause();  
            }

            //Debug.Log("Game Paused");
        }

       
        public void ResumeGame()
        {
            if (!_isGamePaused) return; 

            _isGamePaused = false;
            Time.timeScale = 1f;
            MainMenuManager.Instance.CloseScreen();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            onGameResumed?.Invoke();
            foreach (var pausable in _pausableComponents)
            {
                pausable?.OnResume();  
            }
            //Debug.Log("Game Resumed");
        }

        
        public void TogglePauseGame()
        {
            if (_isGamePaused)
            {
                if(MainMenuManager.Instance.IsInPauseMenu())
                {
                    ResumeGame();
                } else
                {
                    MainMenuManager.Instance.UseBackButton();
                }
            }
            else
            {
                PauseGame();
            }
        }

       
        public bool GetIsGamePaused()
        {
            return _isGamePaused;
        }

        #endregion

        #region Pausable_Components_Management

        
        public void RegisterPausable(IPausable pausable)
        {
            if (pausable == null)
            {
                Debug.LogError("Tried to register a null IPausable component.", this);
                return;
            }

            if (!_pausableComponents.Contains(pausable))
            {
                _pausableComponents.Add(pausable);
            }
        }

       
        public void UnregisterPausable(IPausable pausable)
        {
            if (pausable == null)
            {
                Debug.LogError("Tried to unregister a null IPausable component.", this);
                return;
            }

            _pausableComponents.Remove(pausable);
        }

        #endregion
    }
}