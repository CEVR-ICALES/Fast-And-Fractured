using FastAndFractured;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Enums;
using UnityEngine.InputSystem;

public class TrainingScreensHandler : MonoBehaviour
{
    // this will need refactoring, due to timings had to be done this way
    public GameObject[] keyboardScreens;
    public GameObject[] gamepadScreens;
    public Sprite gamepadNextSprite;
    public Sprite keyboardNextSprite;
    public Image nextScreenImagee;
    public InputAction nextScreenController;

    private int _currentScreenIndex = 0;
    private GameObject _currentScreen;

    private InputDeviceType _currentInputDeviceType;


    private void OnEnable()
    {
        PlayerInputController.OnInputDeviceChanged += HandleInputDeviceChanged;
        SelectDeviceScreen();
    }

    private void OnDisable()
    {
        PlayerInputController.OnInputDeviceChanged -= HandleInputDeviceChanged;
    }

    private void Update()
    {
        CheckIfNextScreenHasBeeenClicked();
    }

    private void HandleInputDeviceChanged(InputDeviceType currentDecive)
    {
        _currentInputDeviceType = currentDecive;
        if(_currentInputDeviceType == InputDeviceType.KEYBOARD_MOUSE)
        {
            nextScreenImagee.sprite = keyboardNextSprite;
        } else
        {
            nextScreenImagee.sprite = gamepadNextSprite;
        }
        SelectDeviceScreen();
    }

    public void OpenNextScreen()
    {
        _currentScreen.SetActive(false);
        _currentScreenIndex++;
        if(_currentScreenIndex > keyboardScreens.Length -1)
        {
            _currentScreenIndex = 0;
        }
        SelectDeviceScreen();
    }

    private void SelectDeviceScreen()
    {
        if(_currentScreen != null)
            _currentScreen.SetActive(false);
        if(_currentInputDeviceType == InputDeviceType.KEYBOARD_MOUSE)
        {
            _currentScreen = keyboardScreens[_currentScreenIndex];
        } else
        {
            _currentScreen = gamepadScreens[_currentScreenIndex];
        }

        _currentScreen.SetActive(true);
    }

    private void CheckIfNextScreenHasBeeenClicked()
    {
        if(Input.GetKeyDown(KeyCode.Tab))
        {
            OpenNextScreen();
        }

        if(Gamepad.current != null)
        {
            if (Gamepad.current.dpad.right.isPressed)
                OpenNextScreen();
        }

        

    }

    
}
