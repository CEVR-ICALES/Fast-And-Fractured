using System.Reflection;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace FastAndFractured.Tests
{
    public sealed class MenuBackInputTests
    {
        private const BindingFlags STATIC_PRIVATE = BindingFlags.Static | BindingFlags.NonPublic;
        private Keyboard _keyboard;
        private Gamepad _gamepad;
        private InputAction _pause;
        private MethodInfo _shouldUseMenuBack;

        [SetUp]
        public void SetUp()
        {
            _keyboard = InputSystem.AddDevice<Keyboard>("MenuBackTestKeyboard");
            _gamepad = InputSystem.AddDevice<Gamepad>("MenuBackTestGamepad");
            _pause = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            _pause.AddBinding("<Gamepad>/start");
            _shouldUseMenuBack = typeof(MenuInputsController).GetMethod("ShouldUseMenuBack", STATIC_PRIVATE);
            Assert.IsNotNull(_shouldUseMenuBack);
        }

        [TearDown]
        public void TearDown()
        {
            _pause?.Dispose();
            if (_gamepad != null && _gamepad.added) InputSystem.RemoveDevice(_gamepad);
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
        }

        [TestCase(true, Key.Escape, false)]
        [TestCase(false, Key.Escape, true)]
        [TestCase(true, Key.Tab, true)]
        [TestCase(false, Key.Tab, true)]
        public void OnlySharedPauseKeysAreReservedDuringPause(bool isPaused, Key key, bool expected)
        {
            Assert.AreEqual(expected, ShouldUseMenuBack(isPaused, _keyboard[key], _pause));
        }

        [TestCase(true, true, false)]
        [TestCase(false, true, true)]
        [TestCase(true, false, true)]
        [TestCase(false, false, true)]
        public void GamepadCancelStillNavigatesWhileStartBelongsToPause(bool isPaused, bool startButton, bool expected)
        {
            InputControl control = startButton ? _gamepad.startButton : _gamepad.buttonEast;
            Assert.AreEqual(expected, ShouldUseMenuBack(isPaused, control, _pause));
        }

        [Test]
        public void PauseBindingsAreReadInsteadOfHardcodingEscape()
        {
            using (var reboundPause = new InputAction("Rebound pause", InputActionType.Button, "<Keyboard>/tab"))
            {
                Assert.IsFalse(ShouldUseMenuBack(true, _keyboard.tabKey, reboundPause));
                Assert.IsTrue(ShouldUseMenuBack(true, _keyboard.escapeKey, reboundPause));
            }
        }

        [Test]
        public void MissingPauseActionDoesNotBlockMenuNavigation()
        {
            Assert.IsTrue(ShouldUseMenuBack(true, _keyboard.escapeKey, null));
        }

        private bool ShouldUseMenuBack(bool isPaused, InputControl control, InputAction pauseAction) =>
            (bool)_shouldUseMenuBack.Invoke(null, new object[] { isPaused, control, pauseAction });
    }
}
