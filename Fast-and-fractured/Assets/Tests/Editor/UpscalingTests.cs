using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AMD;
using UnityEngine.NVIDIA;

namespace FastAndFractured.Tests
{
    public sealed class UpscalingTests
    {
        private const int MAX_QUALITY = 0;
        private const int BALANCED = 1;
        private const int PERFORMANCE = 2;
        private const int ULTRA_PERFORMANCE = 3;
        private const float LOWEST_UPSCALER_PERCENTAGE = 100f / 3f;

        [Test]
        public void EveryPresetUsesTheMatchingDlssQuality()
        {
            Assert.AreEqual((uint)DLSSQuality.MaximumQuality, Upscaling.DlssQuality(MAX_QUALITY));
            Assert.AreEqual((uint)DLSSQuality.Balanced, Upscaling.DlssQuality(BALANCED));
            Assert.AreEqual((uint)DLSSQuality.MaximumPerformance, Upscaling.DlssQuality(PERFORMANCE));
            Assert.AreEqual((uint)DLSSQuality.UltraPerformance, Upscaling.DlssQuality(ULTRA_PERFORMANCE));
        }

        [Test]
        public void EveryPresetUsesTheMatchingFsr2Quality()
        {
            Assert.AreEqual((uint)FSR2Quality.Quality, Upscaling.Fsr2Quality(MAX_QUALITY));
            Assert.AreEqual((uint)FSR2Quality.Balanced, Upscaling.Fsr2Quality(BALANCED));
            Assert.AreEqual((uint)FSR2Quality.Performance, Upscaling.Fsr2Quality(PERFORMANCE));
            Assert.AreEqual((uint)FSR2Quality.UltraPerformance, Upscaling.Fsr2Quality(ULTRA_PERFORMANCE));
        }

        [TestCase(MAX_QUALITY)]
        [TestCase(BALANCED)]
        [TestCase(PERFORMANCE)]
        [TestCase(ULTRA_PERFORMANCE)]
        public void SavedPresetComesBackAfterARestart(int preset)
        {
            Assert.AreEqual(preset, Upscaling.PresetFromDlssQuality((int)Upscaling.DlssQuality(preset)));
        }

        [TestCase(-1)]
        [TestCase(4)]
        [TestCase(99)]
        public void UnknownSavedValueFallsBackToBalanced(int savedValue)
        {
            Assert.AreEqual(Upscaling.DEFAULT_PRESET, Upscaling.PresetFromDlssQuality(savedValue));
            Assert.AreEqual(BALANCED, Upscaling.DEFAULT_PRESET);
        }

        [Test]
        public void NoUpscalingIsAlwaysAvailable()
        {
            Assert.IsTrue(Upscaling.IsAvailable(Upscaling.NONE));
            Assert.IsFalse(Upscaling.IsAvailable("TAAU"));
        }

        [Test]
        public void EveryQualityLevelLetsTheUpscalersLowerTheRenderScale()
        {
            for (int level = 0; level < QualitySettings.names.Length; level++)
            {
                var settings = new SerializedObject(QualitySettings.GetRenderPipelineAssetAt(level));
                var dynamicResolution = "m_RenderPipelineSettings.dynamicResolutionSettings.";
                Assert.IsTrue(settings.FindProperty(dynamicResolution + "enabled").boolValue, QualitySettings.names[level]);
                Assert.LessOrEqual(settings.FindProperty(dynamicResolution + "minPercentage").floatValue, LOWEST_UPSCALER_PERCENTAGE,
                    QualitySettings.names[level]);
            }
        }

        [Test]
        public void ApplyingNoneTurnsEveryUpscalerOff()
        {
            var cameraObject = new GameObject("Upscaling camera", typeof(Camera));
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                var data = cameraObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
                data.allowDynamicResolution = true;
                data.allowDeepLearningSuperSampling = true;
                data.allowFidelityFX2SuperResolution = true;

                Upscaling.Apply(camera, Upscaling.NONE, PERFORMANCE);

                Assert.IsFalse(data.allowDynamicResolution);
                Assert.IsFalse(data.allowDeepLearningSuperSampling);
                Assert.IsFalse(data.allowFidelityFX2SuperResolution);
                Assert.AreEqual(Upscaling.DlssQuality(PERFORMANCE), data.deepLearningSuperSamplingQuality);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
