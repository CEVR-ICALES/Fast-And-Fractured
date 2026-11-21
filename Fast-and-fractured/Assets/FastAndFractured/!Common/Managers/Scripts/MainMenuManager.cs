using Enums;
using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Utilities;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEngine.Events;

namespace FastAndFractured
{
    public class MainMenuManager : AbstractSingleton<MainMenuManager>
    {
        #region Public Fields

        public MenuScreen CurrentScreen => _currentScreen;
        public Dictionary<ScreensType, MenuScreen> menuScreens => _menuScreens;
        public ScreensType defaultScreenType = ScreensType.MAIN_MENU;

        [Header("Splash Screen")]
        [SerializeField] private ParticleSystem splashParticles;

        #endregion

        // #region Serialized Fields

        // [SerializeField] private GameObject mainMenuTimeline;
        // [SerializeField] private GameObject pauseMenuTimeline;

        // #endregion


        #region Private Fields

        private Dictionary<ScreensType, MenuScreen> _menuScreens = new Dictionary<ScreensType, MenuScreen>();
        private MenuScreen _currentScreen; 
        private bool isCurrentScreenInteractable;
        private const float VISIBLE_ALPHA = 1f;
        private ITimer _fadeInTimer;
        private ITimer _fadeOutTimer;
        EventSystem _eventSystem;

        #endregion
        public UnityEvent OnInitialized;

        #region Unity Methods
        protected override void Construct()
        {
            base.Construct();
            _eventSystem = EventSystem.current;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        protected override void Initialize()
        {
            base.Initialize();
            // Register all Screens in scene
            RegisterScreens();
            if(_menuScreens.TryGetValue(defaultScreenType, out MenuScreen menuScreen))
                _currentScreen = menuScreen;

            foreach (var screen in _menuScreens.Values)
            {
                screen.gameObject.SetActive(false);
                screen.SetInteractable(false);
            }

            
            if(_currentScreen != null)
            {
                TransitionBetweenScreens(_currentScreen.screenType, -1f);
                if (_currentScreen.screenType == ScreensType.SPLASH_SCREEN)
                {
                    // Play splash particles after 0.5 seconds
                    TimerSystem.Instance.CreateTimer(2.5f, onTimerDecreaseComplete: () =>
                    {
                        splashParticles.Play();
                    });
                    LoadScene(1, 6);
                }
            }

            OnInitialized?.Invoke();
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
                _currentScreen.DeactivateCamera();
            }
            _currentScreen = _menuScreens[screenType];
            _currentScreen.gameObject.SetActive(true);
            _currentScreen.ActivateCamera();
        }

        public void TransitionBetweenScreens(ScreensType nextScreen, float fadeDuration)
        {
            if (!_menuScreens.TryGetValue(nextScreen, out MenuScreen targetScreen))
            {
                Debug.LogWarning($"[MainMenuManager] Screen {nextScreen} not registered.");
                return;
            }

            StopFade();

            if (fadeDuration == -1 || _currentScreen == null)
            {
                if (_currentScreen != null)
                {
                    _currentScreen.gameObject.SetActive(false);
                    _currentScreen.DeactivateCamera();
                }
                _currentScreen = targetScreen;
                // A screen that left with a fade keeps alpha 0: opened without one it was there but invisible.
                _currentScreen.SetAlpha(VISIBLE_ALPHA);
                _currentScreen.SetInteractable(true);
                isCurrentScreenInteractable = true;
                _currentScreen.gameObject.SetActive(true);
                _currentScreen.ActivateCamera();
                LockFocusOnButton();
                return;
            }

            MenuScreen leavingScreen = _currentScreen;
            leavingScreen.SetInteractable(false);
            isCurrentScreenInteractable = false;
            _fadeOutTimer = TimerSystem.Instance.CreateTimer(fadeDuration,
             onTimerDecreaseComplete: () =>
             {
                 leavingScreen.gameObject.SetActive(false);
                 leavingScreen.DeactivateCamera();
                 _currentScreen = targetScreen;
                 _currentScreen.gameObject.SetActive(true);
                 _currentScreen.ActivateCamera();
                 _currentScreen.SetAlpha(0);
                 _currentScreen.SetInteractable(false);
                 isCurrentScreenInteractable = false;

                 _fadeInTimer = TimerSystem.Instance.CreateTimer(fadeDuration, TimerDirection.INCREASE,
                     onTimerIncreaseComplete: () =>
                     {
                         targetScreen.SetInteractable(true);
                         LockFocusOnButton();
                         isCurrentScreenInteractable = true;
                     },
                     onTimerIncreaseUpdate: (progress) =>
                     {
                         targetScreen.SetAlpha(progress / fadeDuration);
                     });
             },
             onTimerDecreaseUpdate: (progress) =>
             {
                 leavingScreen.SetAlpha(progress / fadeDuration);
             });
        }

        public void CloseScreen()
        {
            StopFade();
            _currentScreen?.gameObject.SetActive(false);
            _currentScreen = null;
        }

        // A fade left running threw every frame once its screen was closed, and no later timer ran.
        private void StopFade()
        {
            _fadeOutTimer?.StopTimer();
            _fadeInTimer?.StopTimer();
            _fadeOutTimer = null;
            _fadeInTimer = null;
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

        // Overload
        public void LoadScene(int sceneIndex, float time)
        {
            TimerSystem.Instance.CreateTimer(time, onTimerDecreaseComplete: () =>
            {
                LoadScene(sceneIndex);
            });
        }

        public void UseBackButton()
        {
            // No screen open (in a match, before Escape opens the pause menu): nothing to go back from.
            if (_currentScreen != null && _currentScreen.backButton != null && isCurrentScreenInteractable && _currentScreen.backButton.IsActive())
            {
                _currentScreen.backButton.onClick.Invoke();
            }
        }

        private void LockFocusOnButton()
        {
            if (_currentScreen.defaultInteractable != null)
            {
                _eventSystem.SetSelectedGameObject(_currentScreen.defaultInteractable.gameObject);
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
            return _currentScreen != null && _currentScreen.screenType == ScreensType.PAUSE;
        }
        #endregion
    }
}

