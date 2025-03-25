using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured
{
    public enum ScreensType
    {
        MAIN_MENU,
        SETTINGS,
        CREDITS,
        GAMEMODE_SELECTION,
        CHARACTER_SELECTION,
        LOADING,
        SPLASH_SCREEN
    }
    public class MainMenuManager : MonoBehaviour
    {
        #region Singleton

        public static MainMenuManager Instance { get; private set; }

        #endregion

        #region Private Fields

        private Dictionary<ScreensType, MenuScreen> _menuScreens = new Dictionary<ScreensType, MenuScreen>();
        private MenuScreen _currentScreen;

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

        #endregion
    }
}

