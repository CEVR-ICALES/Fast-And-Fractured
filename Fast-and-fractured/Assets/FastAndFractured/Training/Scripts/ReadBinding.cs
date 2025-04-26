using Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.XInput;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Serialization;
using UnityEngine.UI;
using static UnityEngine.InputSystem.InputBinding;


namespace FastAndFractured
{
    public enum DeviceType
    {
        Keyboard,
        XboxGamepad,
        PSGamepad
    }
    [System.Serializable]
    public class InputController : MonoBehaviour
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
                    case "leftAlt": return KeyAlt;
                    case "tab": return KeyTab;
                    case "backspace": return KeyBackspace;
                    case "capsLock": return KeyCapsLock;
                    case "upArrow": return KeyArrowUp;
                    case "downArrow": return KeyArrowDown;
                    case "leftArrow": return KeyArrowLeft;
                    case "rightArrow": return KeyArrowRight;
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
        public string action;

        public List<Image> image;

        private DeviceType currentDevice;
        private DeviceType lastDevice;

        private InputAction _selectedAction;

        private void Awake()
        {
            _playerActions = new PlayerInputAction();
            var rebinds = PlayerPrefs.GetString("rebinds");
            if (!string.IsNullOrEmpty(rebinds))
                _playerActions.LoadBindingOverridesFromJson(rebinds);
        }
        private void Start()
        {
            currentDevice = DeviceType.Keyboard;
            _selectedAction = _playerActions.PlayerInputActions.Accelerate;
            InputBinding[] bindings = _selectedAction.bindings.ToArray();
            foreach (var binding in bindings)
            {
                Debug.Log(binding);

            }
            //switch (action)
            //{
            //    case "Movement":
            //        _selectedAction = _playerActions.PlayerInputActions.Movement;
            //        break;
            //    case "Accelerate":
            //        _selectedAction = _playerActions.PlayerInputActions.Accelerate;
            //        break;
            //    case "Camera Move":  
            //        _selectedAction = _playerActions.PlayerInputActions.CameraMove;
            //        break;
            //    case "Reverse":
            //        _selectedAction = _playerActions.PlayerInputActions.Reverse;
            //        break;
            //    case "Brake":
            //        _selectedAction = _playerActions.PlayerInputActions.Brake;
            //        break;
            //    case "Shooting Mode":  
            //        _selectedAction = _playerActions.PlayerInputActions.ShootingMode;
            //        break;
            //    case "Shoot":
            //        _selectedAction = _playerActions.PlayerInputActions.Shoot;
            //        break;
            //    case "Special Ability":  
            //        _selectedAction = _playerActions.PlayerInputActions.SpecialAbility;
            //        break;
            //    case "Throw Mine":  
            //        _selectedAction = _playerActions.PlayerInputActions.ThrowMine;
            //        break;
            //    case "Pause":
            //        _selectedAction = _playerActions.PlayerInputActions.Pause;
            //        break;
            //    case "Reset Camera":  
            //        _selectedAction = _playerActions.PlayerInputActions.ResetCamera;
            //        break;
            //    case "Dash":
            //        _selectedAction = _playerActions.PlayerInputActions.Dash;
            //        break;
            //    default:
            //        Debug.LogWarning($"Action not recognized: {action}");
            //        break;
            //}
            HandleDecalChange();
        }

        void Update()
        {
            CheckCurrentController();
            if (currentDevice != lastDevice)
            {
                HandleDecalChange();
            }
            lastDevice = currentDevice;
        }

        private void HandleDecalChange()
        {
            List<string> keys = ShowBoundKeys(_selectedAction, currentDevice);
            foreach(string key in keys)
            {
                Debug.Log(key);
            }
            if (keys.Count != 1 && currentDevice == DeviceType.Keyboard)
            {
                for (int i = 0; i <= keys.Count - 1; i++)
                {
                    image[i].enabled = true;
                    ChangeDecal(keys[i], image[i]);
                }
            }
            else
            {
                ChangeDecal(keys[0], image[0]);
                if (image.Count > 1)
                {
                    for (int i = 1; i < image.Count; i++)
                    {
                        image[i].enabled = false;
                    }
                }
            }
        }

        private void LateUpdate()
        {
            lastDevice = currentDevice;
        }
        private void CheckCurrentController()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.isPressed)
            {
                currentDevice = DeviceType.Keyboard;
            }

            if (Gamepad.current != null)
            {
                if (Gamepad.current is DualShockGamepad)
                {
                    currentDevice = DeviceType.PSGamepad;
                }
                else if (Gamepad.current is XInputController)
                {
                    currentDevice = DeviceType.XboxGamepad;
                }
            }
        }

        private void ChangeDecal(string key, Image imagae)
        {
            Sprite sprite = null;
            switch (currentDevice)
            {
                case DeviceType.Keyboard:
                    sprite = keyboard.GetSprite(key);
                    break;
                case DeviceType.XboxGamepad:
                    sprite = xbox.GetSprite(key);
                    break;
                case DeviceType.PSGamepad:
                    sprite = play.GetSprite(key);
                    break;
            }

            imagae.sprite = sprite;
            
        }
        private List<string> ShowBoundKeys(InputAction action, DeviceType currentDevice)
        {
            List<string> returnVal = new List<string>();
            bool isProcessingComposite = false;

            foreach (var binding in action.bindings)
            {
                if (isProcessingComposite)
                {
                    if (binding.isComposite)
                    {
                        isProcessingComposite = true;
                        continue;
                    }
                    else
                    {
                        ProcessNonCompositeBinding(currentDevice, ref returnVal, binding);
                    }
                }
                else
                {
                    if (binding.isPartOfComposite)
                    {
                        if (PathContainsDeviceKey(binding.effectivePath, currentDevice))
                        {
                            returnVal.Add(GetKeyFromBinding(binding));
                        }
                    }
                    else
                    {
                        if (binding.isComposite)
                        {
                            continue;
                        }
                        else
                        {
                            isProcessingComposite = false;
                            ProcessNonCompositeBinding(currentDevice, ref returnVal, binding);
                        }
                    }
                }
            }


            return returnVal;
        }

        private string GetKeyFromBinding(InputBinding binding)
        {

            if (currentDevice == DeviceType.Keyboard)
            {
                return binding.effectivePath.Split('/').Last();
            }
            else
            {
                string key = binding.effectivePath.Split('/')[1];
                Debug.Log(key);
                return key;
            }
        }

        private void ProcessNonCompositeBinding(DeviceType currentDevice, ref List<string> returnVal, InputBinding binding)
        {
            if (PathContainsDeviceKey(binding.effectivePath, currentDevice))
            {
                if (!string.IsNullOrEmpty(binding.effectivePath))
                {
                    returnVal.Add(GetKeyFromBinding(binding));
                }
            }
        }

        private bool PathContainsDeviceKey(string path, DeviceType deviceType)
        {
            string toCheck = "";
            switch (deviceType)
            {
                case DeviceType.Keyboard:
                    toCheck = "Keyboard";
                    break;
                case DeviceType.XboxGamepad:
                    toCheck = "Gamepad";
                    break;
                case DeviceType.PSGamepad:
                    toCheck = "Gamepad";
                    break;
            }
            return path.Contains(toCheck);
        }

    }
}


