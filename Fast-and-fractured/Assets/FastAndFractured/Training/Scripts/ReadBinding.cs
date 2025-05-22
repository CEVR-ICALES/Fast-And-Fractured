using Enums;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.Windows;


namespace FastAndFractured
{
    [System.Serializable]
    public class ReadBinding : MonoBehaviour
    {
        [Serializable]
        public struct KeyboardIcons
        {
            public Sprite KeyA;
            public Sprite KeyB;
            public Sprite KeyC;
            public Sprite KeyD;
            public Sprite KeyE;
            public Sprite KeyF;
            public Sprite KeyG;
            public Sprite KeyH;
            public Sprite KeyI;
            public Sprite KeyJ;
            public Sprite KeyK;
            public Sprite KeyL;
            public Sprite KeyM;
            public Sprite KeyN;
            public Sprite KeyO;
            public Sprite KeyP;
            public Sprite KeyQ;
            public Sprite KeyR;
            public Sprite KeyS;
            public Sprite KeyT;
            public Sprite KeyU;
            public Sprite KeyV;
            public Sprite KeyW;
            public Sprite KeyX;
            public Sprite KeyY;
            public Sprite KeyZ;
            public Sprite Key0;
            public Sprite Key1;
            public Sprite Key2;
            public Sprite Key3;
            public Sprite Key4;
            public Sprite Key5;
            public Sprite Key6;
            public Sprite Key7;
            public Sprite Key8;
            public Sprite Key9;
            public Sprite KeyEnter;
            public Sprite KeySpace;
            public Sprite KeyShift;
            public Sprite KeyCtrl;
            public Sprite KeyAlt;
            public Sprite KeyTab;
            public Sprite KeyBackspace;
            public Sprite KeyCapsLock;
            public Sprite KeyArrowUp;
            public Sprite KeyArrowDown;
            public Sprite KeyArrowLeft;
            public Sprite KeyArrowRight;
            public Sprite KeyRightButton;
            public Sprite KeyLeftButton;
            public Sprite KeyEscape;

            public Sprite GetSprite(string controlPath)
            {
                switch (controlPath)
                {
                    case "a": return KeyA;
                    case "b": return KeyB;
                    case "c": return KeyC;
                    case "d": return KeyD;
                    case "e": return KeyE;
                    case "f": return KeyF;
                    case "g": return KeyG;
                    case "h": return KeyH;
                    case "i": return KeyI;
                    case "j": return KeyJ;
                    case "k": return KeyK;
                    case "l": return KeyL;
                    case "m": return KeyM;
                    case "n": return KeyN;
                    case "o": return KeyO;
                    case "p": return KeyP;
                    case "q": return KeyQ;
                    case "r": return KeyR;
                    case "s": return KeyS;
                    case "t": return KeyT;
                    case "u": return KeyU;
                    case "v": return KeyV;
                    case "w": return KeyW;
                    case "x": return KeyX;
                    case "y": return KeyY;
                    case "z": return KeyZ;
                    case "0": return Key0;
                    case "1": return Key1;
                    case "2": return Key2;
                    case "3": return Key3;
                    case "4": return Key4;
                    case "5": return Key5;
                    case "6": return Key6;
                    case "7": return Key7;
                    case "8": return Key8;
                    case "9": return Key9;
                    case "enter": return KeyEnter;
                    case "space": return KeySpace;
                    case "leftShift": return KeyShift;
                    case "leftCtrl": return KeyCtrl;
                    case "ctrl": return KeyCtrl;
                    case "leftAlt": return KeyAlt;
                    case "tab": return KeyTab;
                    case "backspace": return KeyBackspace;
                    case "capsLock": return KeyCapsLock;
                    case "upArrow": return KeyArrowUp;
                    case "downArrow": return KeyArrowDown;
                    case "leftArrow": return KeyArrowLeft;
                    case "rightArrow": return KeyArrowRight;
                    case "rightButton": return KeyRightButton;
                    case "leftButton": return KeyLeftButton;
                    case "escape": return KeyEscape;
                }
                return null;
            }
        }
        [Serializable]
        public struct GamepadIcons
        {
            public Sprite buttonSouth;
            public Sprite buttonNorth;
            public Sprite buttonEast;
            public Sprite buttonWest;
            public Sprite startButton;
            public Sprite selectButton;
            public Sprite leftTrigger;
            public Sprite rightTrigger;
            public Sprite leftShoulder;
            public Sprite rightShoulder;
            public Sprite dpad;
            public Sprite dpadUp;
            public Sprite dpadDown;
            public Sprite dpadLeft;
            public Sprite dpadRight;
            public Sprite leftStick;
            public Sprite rightStick;
            public Sprite leftStickPress;
            public Sprite rightStickPress;

            public Sprite GetSprite(string controlPath)
            {
                switch (controlPath)
                {
                    case "buttonSouth": return buttonSouth;
                    case "buttonNorth": return buttonNorth;
                    case "buttonEast": return buttonEast;
                    case "buttonWest": return buttonWest;
                    case "start": return startButton;
                    case "select": return selectButton;
                    case "leftTrigger": return leftTrigger;
                    case "rightTrigger": return rightTrigger;
                    case "leftShoulder": return leftShoulder;
                    case "rightShoulder": return rightShoulder;
                    case "dpad": return dpad;
                    case "dpad/up": return dpadUp;
                    case "dpad/down": return dpadDown;
                    case "dpad/left": return dpadLeft;
                    case "dpad/right": return dpadRight;
                    case "leftStick": return leftStick;
                    case "rightStick": return rightStick;
                    case "leftStickPress": return leftStickPress;
                    case "rightStickPress": return rightStickPress;
                }
                return null;
            }
        }


        public GamepadIcons xbox;
        [FormerlySerializedAs("ps4")] public GamepadIcons play;
        public KeyboardIcons keyboard;

        private PlayerInputAction _playerActions;
        public string actionName;

        public Image[] icons;

        private InputDeviceType _currentDevice;

        private InputAction _selectedAction;
        private InputBinding[] _bindings;


        private void Awake()
        {
            _playerActions = new PlayerInputAction();
            var rebinds = PlayerPrefs.GetString("rebinds");
            if (!string.IsNullOrEmpty(rebinds))
                _playerActions.LoadBindingOverridesFromJson(rebinds);
        }

        private void OnEnable()
        {
            PlayerInputController.OnInputDeviceChanged += OnDeviceChangedNotifyed;
        }

        private void OnDisable()
        {
            PlayerInputController.OnInputDeviceChanged -= OnDeviceChangedNotifyed;
        }

        private void Start()
        {
            _currentDevice = InputDeviceType.KEYBOARD_MOUSE;
            var actionPropery = _playerActions.PlayerInputActions.GetType().GetProperty(actionName);
            if (actionPropery != null && actionPropery.GetValue(_playerActions.PlayerInputActions) is InputAction action)
            {
                _selectedAction = action;
                _bindings = _selectedAction.bindings.ToArray();
            }
            else
            {
                Debug.LogError($"Action '{actionName}' not found in player actions");
            }
            HandleIconChange();
        }

        private void HandleIconChange()
        {
            int currentIconIndex = 0;
            foreach (var binding in _bindings)
            {
                
                string bindingPath = binding.effectivePath;
                if(!string.IsNullOrEmpty(bindingPath))
                {
                    string[] parts = bindingPath.Split('/');
                    if(parts.Length > 1) // avoid false null when dealing with composites inputs since they first return a effectivePath with only the name that contains the inputs
                    {
                        string deviceType = parts[0].Trim('<', '>');
                        string key = parts[1];
                        if (parts.Length > 2)
                        {
                            int slashIndex = bindingPath.IndexOf('/');

                            if (slashIndex != -1)
                            {
                                key = bindingPath.Substring(slashIndex + 1);
                            }
                        }

                        Sprite icon = GetIconSprite(deviceType, key);

                        UpdateIcons(currentIconIndex, deviceType, icon, ref currentIconIndex);
                    }
                    
                }
            }
        }

        private void OnDeviceChangedNotifyed(InputDeviceType newInputDevice) 
        {
            if (_currentDevice == newInputDevice) return;
            _currentDevice = newInputDevice;
            HandleIconChange();
        }

        private Sprite GetIconSprite(string device, string key)
        {
            if(device == "Keyboard" || device == "Mouse")
            {
                return keyboard.GetSprite(key);
            } else
            {
                if(_currentDevice == InputDeviceType.PS_CONTROLLER)
                {
                    return play.GetSprite(key);
                }

                if(_currentDevice == InputDeviceType.XBOX_CONTROLLER)
                {
                    return xbox.GetSprite(key);
                }
            }

            return null;
            
        }

        private void UpdateIcons(int iconIndex, string device, Sprite icon, ref int currentIndex)
        {
            if (currentIndex > icons.Length - 1) return;
            if (device == "Keyboard" || device == "Mouse")
            {
                if(_currentDevice == InputDeviceType.KEYBOARD_MOUSE)
                {
                    
                    icons[iconIndex].sprite = icon;
                    currentIndex++;
                }
            }
            else
            {
                if (_currentDevice == InputDeviceType.PS_CONTROLLER)
                {
                    icons[iconIndex].sprite = icon; 
                    currentIndex++;
                }

                if (_currentDevice == InputDeviceType.XBOX_CONTROLLER)
                {
                    icons[iconIndex].sprite = icon;
                    currentIndex++;
                }
            }
        }

    }
}


