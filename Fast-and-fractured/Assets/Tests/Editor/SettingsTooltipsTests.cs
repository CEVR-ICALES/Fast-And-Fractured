using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Tests
{
    /// <summary>Every setting has a tooltip (SettingsTooltips), so a new row without one fails here.</summary>
    public class SettingsTooltipsTests
    {
        private const string SETTINGS_PREFAB = "Assets/FastAndFractured/PauseAndSettingsMenu/Prefabs/SettingsMenu.prefab";

        [Test]
        public void EverySettingsRowHasATooltip()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SETTINGS_PREFAB);
            Assert.IsNotNull(prefab, SETTINGS_PREFAB);
            var missing = new List<string>();
            int rows = 0;
            foreach (var content in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (content.name != "Content") continue;
                foreach (Transform row in content)
                {
                    if (SettingsTooltips.RowOf(row) != row) continue;
                    if (row.GetComponentInChildren<Selectable>(true) == null) continue;   // separators
                    rows++;
                    if (string.IsNullOrEmpty(SettingsTooltips.TextFor(row.name))) missing.Add(row.name);
                }
            }
            Assert.Greater(rows, 20, "the settings rows were not found");
            CollectionAssert.IsEmpty(missing, "Settings rows without a tooltip (add them to the Settings localization sheet)");
        }

        [TestCase("Player names")]
        [TestCase("Bot names")]
        [TestCase("Player levels in the room")]
        [TestCase("Name distance")]
        [TestCase("Connection panel (F3)")]
        [TestCase("Host the room if the host leaves")]
        [TestCase("Share gameplay statistics")]
        public void MultiplayerRowsHaveATooltip(string row) => Assert.IsNotEmpty(SettingsTooltips.TextFor(row));

        [TestCase("Connection panel (F3)", "Settings.Tooltip.ConnectionPanelF3")]
        [TestCase("ColorblindMode (1)", "Settings.Tooltip.ColorblindMode1")]
        [TestCase("FPS Cap", "Settings.Tooltip.FPSCap")]
        [TestCase("Host the room if the host leaves", "Settings.Tooltip.HostTheRoomIfTheHostLeaves")]
        public void RowName_GivesItsSheetKey(string row, string key) => Assert.AreEqual(key, SettingsTooltips.KeyFor(row));

        [Test]
        public void RemapRows_UseTheControlsDescription_PlusTheHint()
        {
            string text = SettingsTooltips.TextFor("Dash");
            StringAssert.Contains("\n", text);
            Assert.IsNull(SettingsTooltips.TextFor("Separator"));
        }
    }
}
