using Enums;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Utilities;

namespace FastAndFractured
{
    public class MainMenuManager : MonoBehaviour
    {
        #region Singleton

        public static MainMenuManager Instance { get; private set; }

        #endregion

        #region Private Fields

        private Dictionary<ScreensType, MenuScreen> _menuScreens = new Dictionary<ScreensType, MenuScreen>();
        private MenuScreen _currentScreen;
        private ITimer _fadeInTimer;
        private ITimer _fadeOutTimer;

        #endregion

        #region Unity Methods

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Register all Screens in scene
            RegisterScreens();
            _currentScreen = _menuScreens[ScreensType.MAIN_MENU];

            foreach (var screen in _menuScreens.Values)
            {
                screen.gameObject.SetActive(false);
                screen.SetInteractable(false);
            }

            _currentScreen = _menuScreens[ScreensType.MAIN_MENU];
            _currentScreen.gameObject.SetActive(true);
            _currentScreen.SetAlpha(1); 
            _currentScreen.SetInteractable(true); 
        }

        #endregion

        #region Start Methods

        void RegisterScreens()
        {
            foreach (MenuScreen screen in FindObjectsOfType<MenuScreen>(true))
            {
                _menuScreens[screen.screenType] = screen;
            }
        }

        #endregion

        #region Screen Management Methods

        public void ChangeScreen(ScreensType screenType)
        {
            if (_currentScreen != null)
            {
                _currentScreen.gameObject.SetActive(false);
            }
            _currentScreen = _menuScreens[screenType];
            _currentScreen.gameObject.SetActive(true);
        }

        public void TransitionBetweenScreens(ScreensType nextScreen, float fadeDuration)
        {
            _currentScreen.SetInteractable(false);

            _fadeOutTimer = TimerSystem.Instance.CreateTimer(fadeDuration,
             onTimerDecreaseComplete: () =>
             {
                 // when fade out completes, switch screens and start fade in
                 _currentScreen.gameObject.SetActive(false);
                 _currentScreen = _menuScreens[nextScreen];
                 _currentScreen.gameObject.SetActive(true);
                 _currentScreen.SetAlpha(0); 
                 _currentScreen.SetInteractable(false); 

                 // fade in new screen
                 _fadeInTimer = TimerSystem.Instance.CreateTimer(fadeDuration, TimerDirection.INCREASE,
                     onTimerIncreaseComplete: () =>
                     {
                     
                         _currentScreen.SetInteractable(true);
                     },
                     onTimerIncreaseUpdate: (progress) =>
                     {
                     
                         _currentScreen.SetAlpha(progress / fadeDuration);
                     });
             },
             onTimerDecreaseUpdate: (progress) =>
             {
             
                 _currentScreen.SetAlpha(progress / fadeDuration);
             });
        }

        #endregion

        // THIS SHOULD BE ON A FUTURE GAME MANAGER OR CUSTOM SCENE MANAGER
        #region Scene Management Methods

        public void LoadScene(int sceneIndex)
        {
            //if(SceneManager.GetSceneByBuildIndex(sceneIndex).IsValid())
            //{
            SceneManager.LoadScene(sceneIndex);
            //}
            //else
            //{
            //    Debug.LogWarning($"Scene '{sceneIndex}' not found");
            //}
        }

        public void UseBackButton()
        {
            
        }

        #endregion
    }
}

