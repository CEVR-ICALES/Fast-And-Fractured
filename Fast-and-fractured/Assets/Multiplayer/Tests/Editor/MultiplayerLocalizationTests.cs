using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Assets.SimpleLocalization.Scripts;
using NUnit.Framework;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>
    /// Every "MP." key the code asks for exists in the Multiplayer sheet, in the three languages -
    /// a missing one shows its key on screen.
    /// </summary>
    public class MultiplayerLocalizationTests
    {
        private static readonly string[] Languages = { "English", "Espanol", "Catala" };
        private static readonly Regex KeyUse = new Regex("\"(MP\\.[A-Za-z0-9_.]+)\"");

        [Test]
        public void EveryKeyUsedInCode_IsTranslated()
        {
            LocalizationManager.Dictionary.Clear();
            LocalizationManager.Read();
            var used = new SortedSet<string>();
            foreach (string folder in new[] { "Assets/Multiplayer", "Assets/FastAndFractured/Progression", "Assets/Telemetry" })
                foreach (string file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
                    foreach (Match match in KeyUse.Matches(File.ReadAllText(file)))
                        if (!match.Groups[1].Value.EndsWith(".")) used.Add(match.Groups[1].Value);   // "MP.Progress.Type." + type is a prefix

            Assert.Greater(used.Count, 50, "the keys were not found in the code");
            var missing = new List<string>();
            foreach (string key in used)
                foreach (string language in Languages)
                    if (!LocalizationManager.Dictionary.TryGetValue(language, out var texts) || !texts.TryGetValue(key, out var text) || string.IsNullOrEmpty(text))
                        missing.Add($"{key} ({language})");
            CollectionAssert.IsEmpty(missing, "Add them to Multiplayer.csv (and the Multiplayer tab of the Google sheet)");
        }

        [Test]
        public void RewardTypesAndSlots_AreTranslated()
        {
            LocalizationManager.Read();
            foreach (var type in System.Enum.GetNames(typeof(Progression.RewardType)))
                Assert.IsTrue(LocalizationManager.Dictionary["English"].ContainsKey("MP.Progress.Type." + type), type);
            foreach (var slot in System.Enum.GetNames(typeof(Progression.AccessorySlot)))
                Assert.IsTrue(LocalizationManager.Dictionary["English"].ContainsKey("MP.Progress.Slot." + slot), slot);
        }
    }
}
