using System.Collections;
using System.Collections.Generic;
using FastAndFractured;
using UnityEngine;

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
            _inputActions.MenuInputActions.LeftCharacter.started += ctx => CharacterSelectorManager.Instance.SelectPreviousCharacter();
            _inputActions.MenuInputActions.RightCharacter.started += ctx => CharacterSelectorManager.Instance.SelectNextCharacter();
            _inputActions.MenuInputActions.LeftSkin.started += ctx => CharacterSelectorManager.Instance.SelectPreviousSkin();
            _inputActions.MenuInputActions.RightSkin.started += ctx => CharacterSelectorManager.Instance.SelectNextSkin();
            _inputActions.MenuInputActions.StartGame.started += ctx => LoadSceneIfReady(1);  
        }

        private void OnDisable()
        {
            _inputActions.Disable();
        }

        private void LoadSceneIfReady(int sceneIndex)
        {
            if(CharacterSelectorManager.Instance.CheckIfSkinUnlocked())
            {
                MainMenuManager.Instance.LoadScene(sceneIndex);
            }
        }
}
