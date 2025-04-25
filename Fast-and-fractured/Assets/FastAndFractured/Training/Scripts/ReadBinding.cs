using Enums;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;


namespace FastAndFractured
{
    public class ReadBinding : MonoBehaviour
    {
        private PlayerInputAction _actions;

        public GamepadIcons xbox;
        public GamepadIcons ps4;
        public KeyboardIcons keyboard;

        public string actionName;
        [SerializeField] Image[] icons;

        private InputDeviceType _currentDevice;
        private InputDeviceType _lastDevice;

        private InputAction _selectedAction;

        


        // Start is called before the first frame update
        void Start()
        {
            _actions = new PlayerInputAction();
            HasRebinds();
            DetectCurrentDevice();
        }

        void HasRebinds()
        {
            var rebinds = PlayerPrefs.GetString("rebinds");
            if (!string.IsNullOrEmpty(rebinds))
                _actions.LoadBindingOverridesFromJson(rebinds);
        }

        private void DetectCurrentSelectedAction()
        {
            switch (actionName) 
            { 
                case "Movement":
                    _selectedAction = _actions.PlayerInputActions.Movement;
                    break;
                case "Accelerate":
                    _selectedAction = _actions.PlayerInputActions.Accelerate;
                    break;
                case "CameraMove":
                    _selectedAction = _actions.PlayerInputActions.CameraMove;
                    break;
                case "Reverse":
                    _selectedAction = _actions.PlayerInputActions.Reverse;
                    break;
                case "Brake":
                    _selectedAction = _actions.PlayerInputActions.Brake;
                    break;
                case "ShootingMode":
                    _selectedAction = _actions.PlayerInputActions.ShootingMode;
                    break;
                case "Shoot":
                    _selectedAction = _actions.PlayerInputActions.Shoot;
                    break;
                case "SpecialAbility":
                    _selectedAction = _actions.PlayerInputActions.SpecialAbility;
                    break;
                case "ThrowMine":
                    _selectedAction = _actions.PlayerInputActions.ThrowMine;
                    break;
                case "Pause":
                    _selectedAction = _actions.PlayerInputActions.Pause;
                    break;
                case "ResetCamera":
                    _selectedAction = _actions.PlayerInputActions.ResetCamera;
                    break;
                case "Dash":
                    _selectedAction = _actions.PlayerInputActions.Dash;
                    break;
                default:
                    Debug.LogWarning($"Action not recognized: {actionName}");
                    break;
            }
        }

        void DetectCurrentDevice()
        {
            if (Gamepad.current != null)
            {
                
            }
        }

        void HandleIconChange()
        {

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
            // From the input system, we get the path of the control on device. So we can just
            // map from that to the sprites we have for gamepads.
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
        public Sprite KeyDelete;
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
                case "Space": return KeySpace;
                case "leftShift": return KeyShift;
                case "leftCtrl": return KeyCtrl;
                case "Ctrl": return KeyCtrl;
                case "Control": return KeyCtrl;
                case "leftAlt": return KeyAlt;
                case "tab": return KeyTab;
                case "backspace": return KeyBackspace;
                case "capsLock": return KeyCapsLock;
                case "delete": return KeyDelete;
                case "upArrow": return KeyArrowUp;
                case "downArrow": return KeyArrowDown;
                case "leftArrow": return KeyArrowLeft;
                case "rightArrow": return KeyArrowRight;
            }
            return null;
        }
    }
}


