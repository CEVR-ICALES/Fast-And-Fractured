using FastAndFractured;
using UnityEngine;
using Enums;
using UnityEngine.InputSystem;
using Utilities.Managers.PauseSystem;

public class MenuInputsController : MonoBehaviour
{
        public PlayerInputAction InputActions { get => _inputActions; }
        private PlayerInputAction _inputActions;
        

        void Awake()
        {
            _inputActions = new PlayerInputAction();
        }

        private void OnEnable()
        {
            _inputActions.Enable();

            _inputActions.MenuInputActions.GoBack.started -= HandleGoBack;
            _inputActions.MenuInputActions.GoBack.started += HandleGoBack;
            _inputActions.MenuInputActions.LeftCharacter.started += ctx => {if (CompareCurrentScreenType(ScreensType.CHARACTER_SELECTION)) CharacterSelectorManager.Instance.SelectPreviousCharacter();};
            _inputActions.MenuInputActions.RightCharacter.started += ctx => {if (CompareCurrentScreenType(ScreensType.CHARACTER_SELECTION)) CharacterSelectorManager.Instance.SelectNextCharacter();};
            _inputActions.MenuInputActions.LeftSkin.started += ctx => {if (CompareCurrentScreenType(ScreensType.CHARACTER_SELECTION)) CharacterSelectorManager.Instance.SelectPreviousSkin();};
            _inputActions.MenuInputActions.RightSkin.started += ctx => {if (CompareCurrentScreenType(ScreensType.CHARACTER_SELECTION)) CharacterSelectorManager.Instance.SelectNextSkin();};
        }

        private void OnDisable()
        {
            if (_inputActions == null) return;
            _inputActions.MenuInputActions.GoBack.started -= HandleGoBack;
            _inputActions.Disable();
        }

        private void HandleGoBack(InputAction.CallbackContext context)
        {
            var pauseManager = PauseManager.ExistingInstance;
            bool isPaused = pauseManager != null && pauseManager.IsGamePaused;
            if (!ShouldUseMenuBack(isPaused, context.control, _inputActions.PlayerInputActions.Pause)) return;
            MainMenuManager.ExistingInstance?.UseBackButton();
        }

        private static bool ShouldUseMenuBack(bool isPaused, InputControl control, InputAction pauseAction)
        {
            if (!isPaused || pauseAction == null) return true;
            foreach (var pauseControl in pauseAction.controls)
                if (pauseControl == control) return false;
            return true;
        }

        private void OnDestroy()
        {
            if (_inputActions != null)
            {
                _inputActions.Disable();
                _inputActions.Dispose();
                _inputActions = null;
            }
        }

        private bool CompareCurrentScreenType(ScreensType screenType)
        {
            MainMenuManager menuManager = MainMenuManager.Instance;
            MenuScreen currentScreen = menuManager != null ? menuManager.CurrentScreen : null;
            return currentScreen != null && currentScreen.screenType == screenType;
        }

        private void LoadSceneIfReady(int sceneIndex)
        {
            if(CompareCurrentScreenType(ScreensType.CHARACTER_SELECTION) && CharacterSelectorManager.Instance.CheckIfSkinUnlocked())
            {
                CharacterSelectorManager.Instance.SaveCurrentSelected();
                MainMenuManager.Instance.LoadScene(sceneIndex);
            }
        }
}
