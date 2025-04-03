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
        }

        private void OnDisable()
        {
            _inputActions.Disable();
        }
}
