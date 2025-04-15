using Enums;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Utilities;
using UnityEngine.EventSystems;
using UnityEditor;

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
        private bool isCurrentScreenInteractable;
        private ITimer _fadeInTimer;
        private ITimer _fadeOutTimer;
        EventSystem _eventSystem;

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
            _eventSystem = EventSystem.current;

            // Register all Screens in scene
            RegisterScreens();
            if(_menuScreens.TryGetValue(ScreensType.MAIN_MENU, out MenuScreen menuScreen))
                _currentScreen = menuScreen;

            foreach (var screen in _menuScreens.Values)
            {
                screen.gameObject.SetActive(false);
                screen.SetInteractable(false);
            }

            
            if(_currentScreen != null)
            {
                _currentScreen.gameObject.SetActive(true);
                _currentScreen.SetAlpha(1);
                _currentScreen.SetInteractable(true);
                isCurrentScreenInteractable = true;
                LockFocusOnButton();
            }
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
            if (fadeDuration == -1)
            {
                if(_currentScreen!=null)
                    _currentScreen.gameObject.SetActive(false);
                _currentScreen = _menuScreens[nextScreen];
                _currentScreen.SetInteractable(true);
                isCurrentScreenInteractable = true;
                _currentScreen.gameObject.SetActive(true);
                return;

            }
            _currentScreen.SetInteractable(false);
            isCurrentScreenInteractable = false;
            _fadeOutTimer = TimerSystem.Instance.CreateTimer(fadeDuration,
             onTimerDecreaseComplete: () =>
             {
                 // when fade out completes, switch screens and start fade in
                 _currentScreen.gameObject.SetActive(false);
                 _currentScreen = _menuScreens[nextScreen];
                 _currentScreen.gameObject.SetActive(true);
                 _currentScreen.SetAlpha(0);
                 _currentScreen.SetInteractable(false);
                 isCurrentScreenInteractable = false;

                 // fade in new screen
                 _fadeInTimer = TimerSystem.Instance.CreateTimer(fadeDuration, TimerDirection.INCREASE,
                     onTimerIncreaseComplete: () =>
                     {

                         _currentScreen.SetInteractable(true);
                         LockFocusOnButton();
                         isCurrentScreenInteractable = true;
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

        public void CloseScreen()
        {
            _currentScreen?.gameObject.SetActive(false);
            _currentScreen = null;
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
            if (_currentScreen.backButton != null && isCurrentScreenInteractable)
            {
                _currentScreen.backButton.onClick.Invoke();
            }
        }

        private void LockFocusOnButton()
        {
            if (_currentScreen.defaultButton != null)
            {
                _eventSystem.SetSelectedGameObject(_currentScreen.defaultButton.gameObject);
            }
            else
            {
                Debug.LogWarning("Default button is not assigned!");
            }
        }

        public void ExitButton()
        {
#if UNITY_EDITOR
            // Exit play mode in the Unity Editor
            EditorApplication.isPlaying = false;
#else
        // Quit the application when running as a build
        Application.Quit();
#endif
        }
        public bool IsInPauseMenu()
        {
            return _currentScreen.screenType == ScreensType.PAUSE;
        }
        #endregion
    }
}

