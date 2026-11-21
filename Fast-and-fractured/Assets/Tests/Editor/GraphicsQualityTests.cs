using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Assets.SimpleLocalization.Scripts;
using FastAndFractured.EditorTools;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Tests
{
    public sealed class GraphicsQualityTests
    {
        private const int LEVELS = 6;
        private const float BUDGET = 12.5f;
        private const float FAST = 8f;
        private const float SLOW = 20f;
        private const string SETTINGS_PREFAB = "Assets/FastAndFractured/PauseAndSettingsMenu/Prefabs/SettingsMenu.prefab";
        private const string LOCALIZATION_FOLDER = "Assets/SimpleLocalization/Resources/Localization";

        [TestCase("1", "NVIDIA GeForce RTX 3060", true)]
        [TestCase(null, "AMD Custom GPU 0405 (RADV VANGOGH)", true)]
        [TestCase(null, "AMD Custom GPU 0932", true)]
        [TestCase("0", "NVIDIA GeForce GTX 1060", false)]
        [TestCase(null, null, false)]
        public void SteamDeckIsRecognised(string steamDeckEnv, string gpuName, bool expected)
        {
            Assert.AreEqual(expected, GraphicsQuality.IsSteamDeckDevice(steamDeckEnv, gpuName));
        }

        [Test]
        public void LowerLevelsRenderAtLowerResolutionAndHighOnesAtFull()
        {
            var presets = GraphicsQualityPresetBuilder.PRESETS;
            for (int level = 1; level < presets.Length; level++)
                Assert.LessOrEqual(presets[level - 1].RenderScale, presets[level].RenderScale, presets[level].AssetName);
            Assert.Less(presets[0].RenderScale, 1f, "Very Low must render below native resolution.");
            Assert.AreEqual(1f, presets[presets.Length - 1].RenderScale);
            Assert.Greater(presets[0].RenderScale, 0.5f, "Below half resolution the image falls apart.");
        }

        private static int Search(int start, params float[] frameMsByLevel)
        {
            var search = new GraphicsQualitySearch(LEVELS, start, BUDGET);
            for (int step = 0; step < LEVELS * 2 && !search.IsDone; step++)
                search.Report(frameMsByLevel[search.Current]);
            Assert.IsTrue(search.IsDone, "The search must end within the number of levels.");
            return search.Result;
        }

        [Test]
        public void FastMachineClimbsToUltra()
        {
            Assert.AreEqual(5, Search(2, FAST, FAST, FAST, FAST, FAST, FAST));
        }

        [Test]
        public void SlowMachineDropsToVeryLow()
        {
            Assert.AreEqual(0, Search(3, SLOW, SLOW, SLOW, SLOW, SLOW, SLOW));
        }

        [Test]
        public void ClimbingStopsBelowTheFirstLevelOverBudget()
        {
            Assert.AreEqual(3, Search(2, FAST, FAST, FAST, FAST, SLOW, SLOW));
        }

        [Test]
        public void DroppingStopsAtTheFirstLevelWithinBudget()
        {
            Assert.AreEqual(1, Search(4, FAST, FAST, SLOW, SLOW, SLOW, SLOW));
        }

        [Test]
        public void ALevelOverBudgetIsNeverTriedAgain()
        {
            var search = new GraphicsQualitySearch(LEVELS, 3, BUDGET);
            search.Report(SLOW);
            Assert.AreEqual(2, search.Current);
            search.Report(FAST);
            Assert.IsTrue(search.IsDone, "Level 3 already failed: going back up would only repeat it.");
            Assert.AreEqual(2, search.Result);
        }

        [TestCase(true, 8192, 16384, 1)]
        [TestCase(false, 2048, 16384, 1)]
        [TestCase(false, 4096, 16384, 2)]
        [TestCase(false, 6144, 16384, 3)]
        [TestCase(false, 8192, 16384, 4)]
        [TestCase(false, 12288, 6144, 2)]
        public void StartLevelFollowsTheHardware(bool integrated, int vramMb, int ramMb, int expected)
        {
            Assert.AreEqual(expected, GraphicsQualitySearch.StartLevel(LEVELS, vramMb, ramMb, integrated));
        }

        [TestCase("AMD Radeon(TM) Graphics", true)]
        [TestCase("AMD Radeon Graphics", true)]
        [TestCase("AMD Radeon 780M Graphics", true)]
        [TestCase("Intel(R) UHD Graphics 620", true)]
        [TestCase("Intel(R) Iris(R) Xe Graphics", true)]
        [TestCase("NVIDIA GeForce GTX 1060", false)]
        [TestCase("AMD Radeon RX 6700 XT", false)]
        [TestCase("Intel(R) Arc(TM) A770 Graphics", false)]
        [TestCase("", false)]
        public void IntegratedGpusAreRecognisedByName(string deviceName, bool expected)
        {
            Assert.AreEqual(expected, GraphicsQualitySearch.LooksIntegrated(deviceName));
        }

        [Test]
        public void PercentileIgnoresTheRareSpikes()
        {
            var samples = new List<float>();
            for (int i = 1; i <= 100; i++) samples.Add(i);
            Assert.AreEqual(90f, GraphicsQualitySearch.Percentile(samples, 0.9f));
            Assert.AreEqual(0f, GraphicsQualitySearch.Percentile(new List<float>(), 0.9f));
        }

        [TestCase(true, false, 0, true)]
        [TestCase(true, true, GraphicsQuality.DETECTION_VERSION, false)]
        [TestCase(true, true, GraphicsQuality.DETECTION_VERSION - 1, true)]
        [TestCase(false, false, 0, false)]
        [TestCase(false, true, 0, false)]
        public void DetectionRunsOnlyWhenAutomaticAndNotYetDoneForThisVersion(bool isAuto, bool hasLevel, int version, bool expected)
        {
            Assert.AreEqual(expected, GraphicsQuality.NeedsDetectionFor(isAuto, hasLevel, version));
        }

        [Test]
        public void EveryQualityLevelHasItsOwnHdrpAsset()
        {
            Assert.AreEqual(LEVELS, QualitySettings.names.Length);
            Assert.AreEqual(LEVELS - 1, GraphicsQualityPresetBuilder.PRESETS.Length);
            var baseAsset = AssetDatabase.LoadAssetAtPath<Object>(GraphicsQualityPresetBuilder.BASE_ASSET_PATH);
            for (int level = 0; level < LEVELS; level++)
            {
                var asset = QualitySettings.GetRenderPipelineAssetAt(level);
                Assert.IsNotNull(asset, QualitySettings.names[level]);
                if (level == LEVELS - 1)
                    Assert.AreEqual(baseAsset, asset, "Ultra is the project's own HDRP asset.");
                else
                    Assert.AreEqual($"{GraphicsQualityPresetBuilder.PRESET_FOLDER}/{GraphicsQualityPresetBuilder.PRESETS[level].AssetName}.asset",
                        AssetDatabase.GetAssetPath(asset));
            }
        }

        [Test]
        public void PresetsMatchTheirTable()
        {
            for (int level = 0; level < GraphicsQualityPresetBuilder.PRESETS.Length; level++)
            {
                var preset = GraphicsQualityPresetBuilder.PRESETS[level];
                var settings = new SerializedObject(QualitySettings.GetRenderPipelineAssetAt(level));
                string name = preset.AssetName;
                Assert.AreEqual(preset.Volumetrics, Bool(settings, "supportVolumetrics"), name);
                Assert.AreEqual(preset.VolumetricClouds, Bool(settings, "supportVolumetricClouds"), name);
                Assert.AreEqual(preset.Ssao, Bool(settings, "supportSSAO"), name);
                Assert.AreEqual(preset.PunctualShadowAtlas, Int(settings, "hdShadowInitParams.punctualLightShadowAtlas.shadowAtlasResolution"), name);
                Assert.AreEqual(preset.FogBudget, Float(settings, "lightingQualitySettings.Fog_Budget.Array.data[1]"), 0.0001f, name);
            }
        }

        [Test]
        public void EveryQualityLevelKeepsTheCookieAtlasTheMapsNeed()
        {
            for (int level = 0; level < LEVELS; level++)
            {
                var settings = new SerializedObject(QualitySettings.GetRenderPipelineAssetAt(level));
                Assert.AreEqual(GraphicsQualityPresetBuilder.COOKIE_ATLAS, Int(settings, "lightLoopSettings.cookieAtlasSize"),
                    QualitySettings.names[level] + ": with less, HDRP logs \"No more space in the 2D Cookie Texture Atlas\" every frame.");
            }
        }

        [Test]
        public void NoQualityLevelReservesTheHugeReflectionAtlas()
        {
            const int MAX_PROBE_ATLAS = 4096;
            for (int level = 0; level < LEVELS; level++)
            {
                var settings = new SerializedObject(QualitySettings.GetRenderPipelineAssetAt(level));
                Assert.LessOrEqual(Int(settings, "lightLoopSettings.reflectionProbeTexCacheSize"), MAX_PROBE_ATLAS, QualitySettings.names[level]);
            }
        }

        [TestCase(1f, 1f, 1f, 0.392f, true)]
        [TestCase(1f, 1f, 1f, 1f, false)]
        [TestCase(0.07f, 0.05f, 0.09f, 0.92f, false)]
        [TestCase(0.1f, 0.1f, 0.1f, 0.3f, false)]
        public void OnlyTheWhiteVeilOfAMenuScreenIsDarkenedInAMap(float r, float g, float b, float a, bool expected)
        {
            Assert.AreEqual(expected, InMatchMenuBackdrop.IsVeil(new Color(r, g, b, a)));
        }

        [Test]
        public void EveryGraphicsQualityTextIsTranslated()
        {
            string graphics = File.ReadAllText(Path.Combine(LOCALIZATION_FOLDER, "Graphics.csv"));
            string settings = File.ReadAllText(Path.Combine(LOCALIZATION_FOLDER, "Settings.csv"));
            var keys = new List<string>(GraphicsQuality.LEVEL_NAME_KEYS)
            {
                "Graphics.Quality", "Graphics.QualityAuto", "Graphics.QualityAutoWithLevel",
                "Graphics.QualityDetecting", "Graphics.QualityDetected",
            };
            foreach (string key in keys)
                Assert.IsTrue(graphics.Contains("\n" + key + ","), key);
            Assert.IsTrue(settings.Contains("\nSettings.Tooltip.GraphicsQuality,"));
            Assert.AreEqual(LEVELS, GraphicsQuality.LEVEL_NAME_KEYS.Length);
        }

        [Test]
        public void SettingsMenuGetsAQualityRowBetweenBrightnessAndResolution()
        {
            var menu = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SETTINGS_PREFAB));
            GraphicsQualitySettingRow row = null;
            try
            {
                var settings = menu.GetComponentInChildren<SettingsMenuBehaviour>(true);
                row = settings.gameObject.AddComponent<GraphicsQualitySettingRow>();
                Invoke(row, "Awake");

                var content = settings.transform.Find("SettingsContainer/VideoContainer/Viewport/Content");
                var qualityRow = content.Find(GraphicsQualitySettingRow.ROW_NAME);
                Assert.IsNotNull(qualityRow);
                Assert.AreEqual(content.Find("Separator").GetSiblingIndex() + 1, qualityRow.GetSiblingIndex());

                var dropdown = qualityRow.GetComponentInChildren<TMP_Dropdown>(true);
                Assert.AreEqual(LEVELS + 1, dropdown.options.Count, "Automatic plus one option per level.");
                Assert.AreEqual(LocalizationManager.Localize("Graphics.Quality"), qualityRow.GetComponentInChildren<TMP_Text>(true).text);

                var brightness = content.Find("Brightness").GetComponentInChildren<Selectable>(true);
                var resolution = content.Find("Resolution").GetComponentInChildren<Selectable>(true);
                Assert.AreEqual(brightness, dropdown.navigation.selectOnUp);
                Assert.AreEqual(resolution, dropdown.navigation.selectOnDown);
                Assert.AreEqual(dropdown, brightness.navigation.selectOnDown);
                Assert.AreEqual(dropdown, resolution.navigation.selectOnUp);
            }
            finally
            {
                // Edit mode never woke the row, so Unity would not call OnDestroy and its static subscriptions would stay.
                if (row != null) Invoke(row, "OnDestroy");
                Object.DestroyImmediate(menu);
            }
        }

        [Test]
        public void LowLevelsKeepTheNearShadowCascadesAndDropTheFarOnes()
        {
            const int veryLow = 0, medium = 2, high = 3;

            Assert.IsTrue(ShadowReach.IsLimited(veryLow));
            Assert.AreEqual(2, ShadowReach.Cascades(veryLow));
            Assert.AreEqual(ShadowReach.AUTHORED_CASCADE_ENDS[0], ShadowReach.Split(veryLow, 0) * ShadowReach.Distance(veryLow), 0.01f,
                "The first cascade covers the same metres as authored: near shadows stay as sharp.");

            Assert.AreEqual(3, ShadowReach.Cascades(medium));
            Assert.AreEqual(ShadowReach.AUTHORED_CASCADE_ENDS[1], ShadowReach.Split(medium, 1) * ShadowReach.Distance(medium), 0.01f);
            Assert.LessOrEqual(ShadowReach.Distance(veryLow), ShadowReach.Distance(medium));

            Assert.IsFalse(ShadowReach.IsLimited(high), "High and above keep the authored shadows.");
        }

        private static void Invoke(GraphicsQualitySettingRow row, string method) =>
            typeof(GraphicsQualitySettingRow).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(row, null);

        private static SerializedProperty Setting(SerializedObject settings, string path) =>
            settings.FindProperty("m_RenderPipelineSettings." + path);

        private static bool Bool(SerializedObject settings, string path) => Setting(settings, path).boolValue;
        private static int Int(SerializedObject settings, string path) => Setting(settings, path).intValue;
        private static float Float(SerializedObject settings, string path) => Setting(settings, path).floatValue;
    }
}
