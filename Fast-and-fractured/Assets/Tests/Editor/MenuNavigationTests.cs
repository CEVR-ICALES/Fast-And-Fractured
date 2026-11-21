using System.Collections.Generic;
using System.Reflection;
using Enums;
using NUnit.Framework;
using UnityEngine;
using Utilities;

namespace FastAndFractured.Tests
{
    public sealed class MenuNavigationTests
    {
        private const BindingFlags STATIC_PRIVATE = BindingFlags.Static | BindingFlags.NonPublic;

        [TestCase(ScreensType.MAIN_MENU, false, true, ScreensType.PAUSE)]
        [TestCase(ScreensType.MAIN_MENU, true, true, ScreensType.MAIN_MENU)]
        [TestCase(ScreensType.MAIN_MENU, true, false, ScreensType.MAIN_MENU)]
        [TestCase(ScreensType.MAIN_MENU, false, false, ScreensType.MAIN_MENU)]
        [TestCase(ScreensType.SETTINGS, false, true, ScreensType.SETTINGS)]
        [TestCase(ScreensType.HOW_TO_PLAY, false, true, ScreensType.HOW_TO_PLAY)]
        [TestCase(ScreensType.PAUSE, false, true, ScreensType.PAUSE)]
        public void BackDestinationRespectsTheAvailableScreens(ScreensType requested, bool hasMainMenu, bool hasPause, ScreensType expected)
        {
            var screens = new Dictionary<ScreensType, MenuScreen>();
            if (hasMainMenu) screens.Add(ScreensType.MAIN_MENU, null);
            if (hasPause) screens.Add(ScreensType.PAUSE, null);
            var resolve = typeof(MenuTransitionButton).GetMethod("ResolveDestination", STATIC_PRIVATE);
            Assert.IsNotNull(resolve);
            var actual = (ScreensType)resolve.Invoke(null, new object[] { requested, screens });
            Assert.AreEqual(expected, actual);
        }

        private const float INSTANT = -1f;
        private const float FADE = 0.8f;

        [Test]
        public void ScreenThatLeftWithAFadeIsVisibleWhenReopenedWithoutOne()
        {
            using (var menu = new MenuFixture())
            {
                menu.Manager.TransitionBetweenScreens(ScreensType.SETTINGS, INSTANT);
                menu.Manager.TransitionBetweenScreens(ScreensType.PAUSE, FADE);
                menu.Timers.Tick(FADE);
                menu.Timers.Tick(FADE);
                Assert.AreEqual(ScreensType.PAUSE, menu.Manager.CurrentScreen.screenType);

                menu.Manager.TransitionBetweenScreens(ScreensType.SETTINGS, INSTANT);

                Assert.IsTrue(menu.Group(ScreensType.SETTINGS).gameObject.activeSelf);
                Assert.AreEqual(1f, menu.Group(ScreensType.SETTINGS).alpha, 0.001f, "It was open and clickable, but transparent.");
                Assert.IsTrue(menu.Group(ScreensType.SETTINGS).interactable);
            }
        }

        [Test]
        public void ClosingTheMenuDuringAFadeLeavesNothingRunning()
        {
            using (var menu = new MenuFixture())
            {
                menu.Manager.TransitionBetweenScreens(ScreensType.SETTINGS, INSTANT);
                menu.Manager.TransitionBetweenScreens(ScreensType.PAUSE, FADE);
                menu.Timers.Tick(FADE);
                menu.Timers.Tick(FADE * 0.25f);
                Assert.Less(menu.Group(ScreensType.PAUSE).alpha, 1f, "The pause screen is still fading in.");

                menu.Manager.CloseScreen();

                Assert.DoesNotThrow(() => menu.Timers.Tick(FADE), "The fade kept writing to the closed screen every frame.");
                Assert.IsNull(menu.Manager.CurrentScreen);
                Assert.IsFalse(menu.Group(ScreensType.PAUSE).gameObject.activeSelf);

                menu.Manager.TransitionBetweenScreens(ScreensType.PAUSE, INSTANT);
                Assert.AreEqual(1f, menu.Group(ScreensType.PAUSE).alpha, 0.001f);
                Assert.IsTrue(menu.Group(ScreensType.PAUSE).interactable);
            }
        }

        [Test]
        public void ANewTransitionReplacesTheFadeInProgress()
        {
            using (var menu = new MenuFixture())
            {
                menu.Manager.TransitionBetweenScreens(ScreensType.SETTINGS, INSTANT);
                menu.Manager.TransitionBetweenScreens(ScreensType.PAUSE, FADE);
                menu.Timers.Tick(FADE * 0.5f);

                menu.Manager.TransitionBetweenScreens(ScreensType.HOW_TO_PLAY, INSTANT);
                menu.Timers.Tick(FADE);
                menu.Timers.Tick(FADE);

                Assert.AreEqual(ScreensType.HOW_TO_PLAY, menu.Manager.CurrentScreen.screenType, "The old fade must not swap screens later.");
                Assert.IsTrue(menu.Group(ScreensType.HOW_TO_PLAY).gameObject.activeSelf);
                Assert.IsFalse(menu.Group(ScreensType.PAUSE).gameObject.activeSelf);
            }
        }

        [TestCase(true, true, ScreensType.CHARACTER_SELECTION, true)]
        [TestCase(true, true, ScreensType.MAIN_MENU, false)]
        [TestCase(true, true, ScreensType.LOBBY_ROOM, false)]
        [TestCase(true, false, ScreensType.CHARACTER_SELECTION, false)]
        [TestCase(false, true, ScreensType.CHARACTER_SELECTION, false)]
        public void SkinsAreOnlyUnlockedFromTheCharacterSelectorWhileTheShortcutIsOn(bool enabled, bool hasScreen, ScreensType screen, bool expected)
        {
            Assert.AreEqual(expected, UnlockAllSkinsShortcut.ShouldUnlock(enabled, hasScreen, screen));
        }

        [Test]
        public void UnlockingCoversEverySkinOfEveryCharacter()
        {
            var first = ScriptableObject.CreateInstance<CharacterMenuData>();
            var second = ScriptableObject.CreateInstance<CharacterMenuData>();
            var models = new[] { new GameObject("Pepe_0"), new GameObject("Pepe_1"), new GameObject("Carme_0"), new GameObject("Carme_3") };
            try
            {
                first.Models = new[] { models[0], models[1], null };
                second.Models = new[] { models[2], models[3] };

                List<string> keys = CharacterSelectorManager.SkinKeys(new[] { first, null, second });

                CollectionAssert.AreEqual(new[] { "Pepe_0", "Pepe_1", "Carme_0", "Carme_3" }, keys);
            }
            finally
            {
                foreach (var model in models) Object.DestroyImmediate(model);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        private sealed class MenuFixture : System.IDisposable
        {
            private static readonly ScreensType[] SCREENS = { ScreensType.PAUSE, ScreensType.SETTINGS, ScreensType.HOW_TO_PLAY };
            private readonly GameObject _root = new GameObject("MenuFixture");
            private readonly GameObject _ownTimers;
            private readonly Dictionary<ScreensType, CanvasGroup> _groups = new Dictionary<ScreensType, CanvasGroup>();

            public MainMenuManager Manager { get; }
            public TimerSystem Timers { get; }

            public MenuFixture()
            {
                if (TimerSystem.ExistingInstance == null)
                    _ownTimers = new GameObject("MenuFixtureTimers", typeof(TimerSystem));
                Timers = TimerSystem.Instance;
                Manager = _root.AddComponent<MainMenuManager>();
                var groupField = typeof(MenuScreen).GetField("canvasGroup", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (ScreensType type in SCREENS)
                {
                    var go = new GameObject(type.ToString());
                    go.transform.SetParent(_root.transform);
                    var screen = go.AddComponent<MenuScreen>();
                    screen.screenType = type;
                    _groups[type] = go.AddComponent<CanvasGroup>();
                    groupField.SetValue(screen, _groups[type]);
                    go.SetActive(false);
                    Manager.menuScreens[type] = screen;
                }
            }

            public CanvasGroup Group(ScreensType type) => _groups[type];

            public void Dispose()
            {
                Manager.CloseScreen();
                Timers.Tick(0f);
                Object.DestroyImmediate(_root);
                if (_ownTimers != null) Object.DestroyImmediate(_ownTimers);
            }
        }
    }
}
