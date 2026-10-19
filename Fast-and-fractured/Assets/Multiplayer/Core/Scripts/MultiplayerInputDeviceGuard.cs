using System.Reflection;
using Enums;
using FishNet;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Online only: mouse activity also switches the shared controller back from gamepad glyphs.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class MultiplayerInputDeviceGuard : MonoBehaviour
    {
        private const float MIN_MOUSE_SQR = 0.01f;

        private static readonly FieldInfo CurrentDeviceField = typeof(PlayerInputController)
            .GetField("_currentInputDevice", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo UsingControllerField = typeof(PlayerInputController)
            .GetField("_isUsingController", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo DeviceChangedField = typeof(PlayerInputController)
            .GetField("OnInputDeviceChanged", BindingFlags.Static | BindingFlags.NonPublic);

        private void Update()
        {
            if (!InstanceFinder.IsClientStarted)
                return;

            Mouse mouse = Mouse.current;
            if (mouse == null || !HasMeaningfulMouseInput(mouse))
                return;

            // ExistingInstance: Instance would auto-create a phantom controller (and a phantom LevelController) in the lobby.
            PlayerInputController input = PlayerInputController.ExistingInstance;
            if (input == null || !input.IsUsingController)
                return;

            CurrentDeviceField?.SetValue(input, InputDeviceType.KEYBOARD_MOUSE);
            UsingControllerField?.SetValue(input, false);
            var changed = DeviceChangedField?.GetValue(null) as PlayerInputController.InputDeviceChanged;
            changed?.Invoke(InputDeviceType.KEYBOARD_MOUSE);
        }

        private static bool HasMeaningfulMouseInput(Mouse mouse)
        {
            return mouse.leftButton.wasPressedThisFrame ||
                   mouse.rightButton.wasPressedThisFrame ||
                   mouse.middleButton.wasPressedThisFrame ||
                   mouse.delta.ReadValue().sqrMagnitude > MIN_MOUSE_SQR ||
                   mouse.scroll.ReadValue().sqrMagnitude > MIN_MOUSE_SQR;
        }
    }
}
