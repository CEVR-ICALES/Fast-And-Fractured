using FastAndFractured;
using UnityEngine;
using Enums;

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

            _inputActions.MenuInputActions.GoBack.started += ctx => MainMenuManager.Instance.UseBackButton();      
            _inputActions.MenuInputActions.LeftCharacter.started += ctx => {if (CompareCurrentScreenType(ScreensType.CHARACTER_SELECTION)) CharacterSelectorManager.Instance.SelectPreviousCharacter();};
            _inputActions.MenuInputActions.RightCharacter.started += ctx => {if (CompareCurrentScreenType(ScreensType.CHARACTER_SELECTION)) CharacterSelectorManager.Instance.SelectNextCharacter();};
            _inputActions.MenuInputActions.LeftSkin.started += ctx => {if (CompareCurrentScreenType(ScreensType.CHARACTER_SELECTION)) CharacterSelectorManager.Instance.SelectPreviousSkin();};
            _inputActions.MenuInputActions.RightSkin.started += ctx => {if (CompareCurrentScreenType(ScreensType.CHARACTER_SELECTION)) CharacterSelectorManager.Instance.SelectNextSkin();};
        }

        private void OnDisable()
        {
            _inputActions?.Disable();
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
