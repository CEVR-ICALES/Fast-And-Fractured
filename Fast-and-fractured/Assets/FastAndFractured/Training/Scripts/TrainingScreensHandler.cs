using FastAndFractured;
using UnityEngine;
using UnityEngine.UI;
using Enums;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

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
    private List<GameObject> activeScreens = new List<GameObject>();
    private bool _hasDpadBeenReleased = false;
    [SerializeField]
    private bool showInOneScreen = true;

    private InputDeviceType _currentInputDeviceType;


    private void OnEnable()
    {
        PlayerInputController.OnInputDeviceChanged += HandleInputDeviceChanged;
        if (!showInOneScreen)
        {
            SelectDeviceScreen();
        }
        else
        {
            SelectDevicesScreen();
        }
    }

    private void OnDisable()
    {
        PlayerInputController.OnInputDeviceChanged -= HandleInputDeviceChanged;
    }

    private void Update()
    {
        if (!showInOneScreen)
        {
            CheckIfNextScreenHasBeeenClicked();
        }
    }

    private void HandleInputDeviceChanged(InputDeviceType currentDecive)
    {
        if (_currentInputDeviceType == currentDecive) return;
        _currentInputDeviceType = currentDecive;
        if(_currentInputDeviceType == InputDeviceType.KEYBOARD_MOUSE)
        {
            nextScreenImagee.sprite = keyboardNextSprite;
        } else
        {
            nextScreenImagee.sprite = gamepadNextSprite;
        }
        if (!showInOneScreen)
        {
            SelectDeviceScreen();
        }
        else
        {
            SelectDevicesScreen();
        }
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
    private void SelectDevicesScreen()
    {
        if (activeScreens.Count > 0)
        {
            foreach (var screens in activeScreens)
            {
                screens.gameObject.SetActive(false);
            }
            activeScreens.Clear();
        }
            if (_currentInputDeviceType == InputDeviceType.KEYBOARD_MOUSE)
            {
                activeScreens = keyboardScreens.ToList();
            }
            else
            {
                activeScreens = gamepadScreens.ToList();
            }
        foreach (var screen in activeScreens)
        {
            screen.gameObject.SetActive(true);
        }
    }

    private void CheckIfNextScreenHasBeeenClicked()
    {
        if(Input.GetKeyDown(KeyCode.Tab))
        {
            OpenNextScreen();
        }

        if(Gamepad.current != null)
        {
            if (Gamepad.current.dpad.right.isPressed && _hasDpadBeenReleased)
            {
                _hasDpadBeenReleased = false;
                OpenNextScreen();
            } else if(!Gamepad.current.dpad.right.isPressed)
            {
                _hasDpadBeenReleased = true;
            }
        }
    }

    
}
