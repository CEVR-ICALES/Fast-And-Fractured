using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Tests
{
    public sealed class TutorialPanelTests
    {
        private const string TUTORIAL_TAG = "Tutorial";
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _created) Object.DestroyImmediate(go);
            _created.Clear();
        }

        [Test]
        public void TutorialSkipsThePanelOfAnotherPlayersHiddenHud()
        {
            Image hidden = CreatePanel("RemoteHud", false);
            Image shown = CreatePanel("LocalHud", true);
            Image hiddenAgain = CreatePanel("OtherRemoteHud", false);

            var step = new GameObject(nameof(AnimatorPanelHandle)).AddComponent<AnimatorPanelHandle>();
            _created.Add(step.gameObject);

            Assert.IsTrue(FindPanel(step));
            Image picked = (Image)typeof(AnimatorPanelHandle)
                .GetField("tutorialImage", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(step);
            Assert.AreSame(shown, picked);
            Assert.AreNotSame(hidden, picked);
            Assert.AreNotSame(hiddenAgain, picked);
        }

        [Test]
        public void TutorialHasNoPanelWhenEveryHudIsHidden()
        {
            CreatePanel("RemoteHud", false);
            var step = new GameObject(nameof(AnimatorPanelHandle)).AddComponent<AnimatorPanelHandle>();
            _created.Add(step.gameObject);
            Assert.IsFalse(FindPanel(step));
        }

        private static bool FindPanel(AnimatorPanelHandle step) =>
            (bool)typeof(AnimatorPanelHandle).GetMethod("FindPanel", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(step, null);

        private Image CreatePanel(string hudName, bool canvasEnabled)
        {
            var hud = new GameObject(hudName, typeof(RectTransform), typeof(Canvas));
            hud.GetComponent<Canvas>().enabled = canvasEnabled;
            _created.Add(hud);
            var panel = new GameObject("TutorialPanel", typeof(RectTransform), typeof(Image)) { tag = TUTORIAL_TAG };
            panel.transform.SetParent(hud.transform, false);
            return panel.GetComponent<Image>();
        }
    }
}
