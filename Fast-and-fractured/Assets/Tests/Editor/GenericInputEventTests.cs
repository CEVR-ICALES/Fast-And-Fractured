using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FastAndFractured.Tests
{
    public sealed class GenericInputEventTests
    {
        private const BindingFlags INSTANCE_PRIVATE = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string MISSING_ACTION_WARNING = "GenericInputEvent has no valid input action reference.";
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<InputActionReference> _references = new List<InputActionReference>();
        private InputActionAsset _asset;
        private InputAction _pause;
        private InputAction _other;
        private InputActionReference _pauseReference;
        private InputActionReference _otherReference;
        private Keyboard _keyboard;

        [SetUp]
        public void SetUp()
        {
            _keyboard = InputSystem.AddDevice<Keyboard>("GenericInputEventTestKeyboard");
            _asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = _asset.AddActionMap("Gameplay");
            _pause = map.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            _other = map.AddAction("Other", InputActionType.Button, "<Keyboard>/enter");
            _pauseReference = MakeReference(_pause);
            _otherReference = MakeReference(_other);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go == null) continue;
                Disable(go.GetComponent<GenericInputEvent>());
                Object.DestroyImmediate(go);
            }
            _objects.Clear();
            if (_asset != null) _asset.Disable();
            foreach (var reference in _references)
                if (reference != null) Object.DestroyImmediate(reference);
            _references.Clear();
            if (_asset != null) Object.DestroyImmediate(_asset);
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
        }

        [Test]
        public void DisabledReferencedActionIsEnabledAndDeliversOnePress()
        {
            int calls = 0;
            var listener = Listener(_pauseReference, () => calls++);
            Assert.IsFalse(_pause.enabled);
            Enable(listener);
            Assert.IsTrue(_pause.enabled);
            Press(Key.Escape);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void ReenablingDoesNotDuplicateCallbacksAndReleasesOwnedEnablement()
        {
            int calls = 0;
            var listener = Listener(_pauseReference, () => calls++);
            Enable(listener);
            Disable(listener);
            Assert.IsFalse(_pause.enabled);
            Enable(listener);
            Enable(listener);
            Press(Key.Escape);
            Assert.AreEqual(1, calls);
            Disable(listener);
            Assert.IsFalse(_pause.enabled);
            Press(Key.Escape);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void PreviouslyEnabledActionIsNotDisabledWhenListenerLeaves()
        {
            _pause.Enable();
            var listener = Listener(_pauseReference, () => { });
            Enable(listener);
            Disable(listener);
            Assert.IsTrue(_pause.enabled);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SharedActionRemainsEnabledUntilTheLastListenerLeaves(bool releaseFirstListenerFirst)
        {
            int firstCalls = 0;
            int secondCalls = 0;
            var first = Listener(_pauseReference, () => firstCalls++);
            var second = Listener(_pauseReference, () => secondCalls++);
            Enable(first);
            Enable(second);
            Press(Key.Escape);
            Assert.AreEqual(1, firstCalls);
            Assert.AreEqual(1, secondCalls);
            Disable(releaseFirstListenerFirst ? first : second);
            Assert.IsTrue(_pause.enabled);
            Press(Key.Escape);
            Assert.AreEqual(releaseFirstListenerFirst ? 1 : 2, firstCalls);
            Assert.AreEqual(releaseFirstListenerFirst ? 2 : 1, secondCalls);
            Disable(releaseFirstListenerFirst ? second : first);
            Assert.IsFalse(_pause.enabled);
        }

        [Test]
        public void ChangingSerializedReferenceReleasesTheActionActuallySubscribed()
        {
            int calls = 0;
            _other.Enable();
            var listener = Listener(_pauseReference, () => calls++);
            Enable(listener);
            typeof(GenericInputEvent).GetField("inputActionReference", INSTANCE_PRIVATE).SetValue(listener, _otherReference);
            Disable(listener);
            Assert.IsFalse(_pause.enabled);
            Assert.IsTrue(_other.enabled);
            _pause.Enable();
            Press(Key.Escape);
            Assert.AreEqual(0, calls);
            _pause.Disable();
            Enable(listener);
            Press(Key.Enter);
            Assert.AreEqual(1, calls);
            Disable(listener);
            Assert.IsTrue(_other.enabled);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingReferenceOrUnresolvedActionDoesNotThrow(bool unresolvedReference)
        {
            InputActionReference reference = null;
            if (unresolvedReference)
            {
                reference = ScriptableObject.CreateInstance<InputActionReference>();
                _references.Add(reference);
            }
            var listener = Listener(reference, () => { });
            LogAssert.Expect(LogType.Warning, MISSING_ACTION_WARNING);
            Assert.DoesNotThrow(() => Enable(listener));
            Assert.DoesNotThrow(() => Disable(listener));
        }

        [Test]
        public void DisablingTheGameplayCloneDoesNotDisableTheReferencedPauseAction()
        {
            int calls = 0;
            var clone = Object.Instantiate(_asset);
            try
            {
                clone.Enable();
                Assert.IsFalse(_pause.enabled);
                var listener = Listener(_pauseReference, () => calls++);
                Enable(listener);
                clone.Disable();
                Assert.IsTrue(_pause.enabled);
                Press(Key.Escape);
                Assert.AreEqual(1, calls);
            }
            finally
            {
                clone.Disable();
                Object.DestroyImmediate(clone);
            }
        }

        private InputActionReference MakeReference(InputAction action)
        {
            var reference = InputActionReference.Create(action);
            _references.Add(reference);
            return reference;
        }

        private GenericInputEvent Listener(InputActionReference reference, UnityAction callback)
        {
            var go = new GameObject("GenericInputEvent test");
            go.SetActive(false);
            _objects.Add(go);
            var listener = go.AddComponent<GenericInputEvent>();
            var performed = new UnityEvent();
            performed.AddListener(callback);
            typeof(GenericInputEvent).GetField("inputActionReference", INSTANCE_PRIVATE).SetValue(listener, reference);
            typeof(GenericInputEvent).GetField("onInputPerformedEvent", INSTANCE_PRIVATE).SetValue(listener, performed);
            return listener;
        }

        private static void Enable(GenericInputEvent listener) => InvokeLifecycle(listener, "OnEnable");
        private static void Disable(GenericInputEvent listener) => InvokeLifecycle(listener, "OnDisable");

        private static void InvokeLifecycle(GenericInputEvent listener, string method)
        {
            try { typeof(GenericInputEvent).GetMethod(method, INSTANCE_PRIVATE).Invoke(listener, null); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        private void Press(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            InputSystem.Update();
            InputSystem.QueueStateEvent(_keyboard, default(KeyboardState));
            InputSystem.Update();
        }
    }
}
