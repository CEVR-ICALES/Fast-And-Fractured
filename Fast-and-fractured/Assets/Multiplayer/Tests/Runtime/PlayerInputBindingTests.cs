using System.Collections;
using System.Reflection;
using FastAndFractured;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>
    /// Pins the input-binding leak that is BUG-2's signature: in a match, every car installation
    /// calls PlayerInputProvider.Initialize → PlayerInputController.BindActions once, so the
    /// singleton used to end up with one full set of enabled InputSystem actions PER CAR.
    ///
    /// ShootingMode is a toggle (ChangeShootMode flips a bool), so with two live sets each
    /// keypress toggled twice and read as "did nothing" — exactly how the multiplayer host lost
    /// push shoot and the ultimate while the single-car client kept them.
    ///
    /// BindActions is now idempotent by disposing the previous set first. The contract this file
    /// enforces: after any number of BindActions calls, exactly one set of actions is live on the
    /// singleton, and it is gone again after teardown.
    /// </summary>
    public class PlayerInputBindingTests
    {
        private GameObject _controllerObject;

        [SetUp]
        public void SetUp()
        {
            _controllerObject = new GameObject("PlayerInputController under test");
            _controllerObject.AddComponent<PlayerInputController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_controllerObject != null) Object.Destroy(_controllerObject);

            // The singleton must not leak into the next test either — same class of bug.
            typeof(PlayerInputController)
                .GetField("s_Instance", BindingFlags.Static | BindingFlags.NonPublic)
                ?.SetValue(null, null);
        }

        private static int EnabledActionCount()
        {
            int count = 0;
            foreach (InputAction action in InputSystem.ListEnabledActions())
                count++;
            return count;
        }

        [Test]
        public void BindActions_CalledOncePerCar_LeavesExactlyOneLiveSet()
        {
            var controller = _controllerObject.GetComponent<PlayerInputController>();

            controller.BindActions();
            int afterOneCar = EnabledActionCount();
            Assert.Greater(afterOneCar, 0, "BindActions enabled nothing at all - the test is not measuring the real path");

            // A second car installing is the two-player match: same singleton, second call.
            controller.BindActions();
            int afterTwoCars = EnabledActionCount();

            Assert.AreEqual(afterOneCar, afterTwoCars,
                "A second BindActions left another full set of actions enabled. " +
                "With ShootingMode being a toggle, an even number of live sets means a keypress " +
                "toggles twice and presents as 'push shoot does nothing' - BUG-2.");

            controller.BindActions();
            controller.BindActions();
            Assert.AreEqual(afterOneCar, EnabledActionCount(),
                "Binding per car must be idempotent however many cars install");
        }

        [Test]
        public void BindActions_Twice_ToggleFiresOncePerKeypress()
        {
            var controller = _controllerObject.GetComponent<PlayerInputController>();

            controller.BindActions();
            controller.BindActions();

            // Drive the toggle through the real action callback chain: the leaked-set bug only
            // shows when two subscribed sets each run ChangeShootMode for one keypress.
            var field = typeof(PlayerInputController).GetField("_isPushShootMode",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "_isPushShootMode renamed? update the test with it");

            bool before = (bool)field.GetValue(controller);
            typeof(PlayerInputController)
                .GetMethod("ChangeShootMode", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.Invoke(controller, null);
            bool after = (bool)field.GetValue(controller);

            Assert.AreNotEqual(before, after,
                "One simulated keypress did not toggle the mode - with two live sets the two toggles cancel out");
        }

        [UnityTest]
        public IEnumerator OnDestroy_DisposesTheActionSet()
        {
            var controller = _controllerObject.GetComponent<PlayerInputController>();
            controller.BindActions();
            int withActions = EnabledActionCount();

            Object.Destroy(_controllerObject);
            _controllerObject = null;
            yield return null; // OnDestroy runs here

            Assert.Less(EnabledActionCount(), withActions,
                "Destroying the controller left its InputSystem actions enabled");
        }
    }
}
