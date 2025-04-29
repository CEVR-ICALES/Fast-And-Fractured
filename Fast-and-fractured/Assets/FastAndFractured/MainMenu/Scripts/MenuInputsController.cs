using FastAndFractured;
using UnityEngine;
using Enums;
using Unity.VisualScripting;

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
            _inputActions.MenuInputActions.StartGame.started += ctx => {if (CompareCurrentScreenType(ScreensType.CHARACTER_SELECTION)) LoadSceneIfReady(1);};
        }

        private void OnDisable()
        {
            _inputActions.Disable();
        }

        private bool CompareCurrentScreenType(ScreensType screenType)
        {
            return MainMenuManager.Instance.CurrentScreen.screenType == screenType;
        }

        private void LoadSceneIfReady(int sceneIndex)
        {
            if(CompareCurrentScreenType(ScreensType.CHARACTER_SELECTION) && CharacterSelectorManager.Instance.CheckIfSkinUnlocked())
            {
                MainMenuManager.Instance.LoadScene(sceneIndex);
            }
        }
}
