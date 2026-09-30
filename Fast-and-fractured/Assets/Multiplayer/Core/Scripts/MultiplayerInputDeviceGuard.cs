using System.Reflection;
using Enums;
using FishNet;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Completes device detection only while an online client is running. The shared controller
    /// recognizes keyboard keys and gamepad buttons, but not mouse activity; consequently a
    /// single gamepad press leaves controller glyphs visible while the player aims and fires with
    /// the mouse. This multiplayer-only bridge avoids changing single-player input code.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class MultiplayerInputDeviceGuard : MonoBehaviour
    {
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

            // ExistingInstance, never Instance: the singleton accessor auto-creates the component on a
            // bare GameObject when none is present, and PlayerInputController.Construct then reaches for
            // LevelController.Instance, spawning a second phantom. In the lobby (client started, no level)
            // this fires every frame the mouse moves, and the phantoms hold s_Instance until the real
            // controllers awake in the map scene and disable themselves as duplicates.
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
                   mouse.delta.ReadValue().sqrMagnitude > 0.01f ||
                   mouse.scroll.ReadValue().sqrMagnitude > 0.01f;
        }
    }
}
