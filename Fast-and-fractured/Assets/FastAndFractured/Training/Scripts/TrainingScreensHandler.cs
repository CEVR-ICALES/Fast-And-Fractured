using FastAndFractured;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enums;

public class TrainingScreensHandler : MonoBehaviour
{
    // this will need refactoring, due to timings had to be done this way
    public GameObject[] keyboardScreens;
    public GameObject[] gamepadScreens;
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

    private void HandleInputDeviceChanged(InputDeviceType currentDecive)
    {
        _currentInputDeviceType = currentDecive;
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
        if(_currentInputDeviceType == InputDeviceType.KEYBOARD_MOUSE)
        {
            _currentScreen = keyboardScreens[_currentScreenIndex];
        } else
        {
            _currentScreen = gamepadScreens[_currentScreenIndex];
        }

        _currentScreen.SetActive(true);
    }

    
}
