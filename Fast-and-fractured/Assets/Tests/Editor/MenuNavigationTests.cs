using System.Collections.Generic;
using System.Reflection;
using Enums;
using NUnit.Framework;

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
    }
}
