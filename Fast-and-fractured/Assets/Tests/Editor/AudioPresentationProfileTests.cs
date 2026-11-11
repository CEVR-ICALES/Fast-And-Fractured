using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Tests
{
    public sealed class AudioPresentationProfileTests
    {
        private const float TOLERANCE = 0.00001f;
        private AudioPresentationProfile _profile;

        [SetUp]
        public void SetUp() => _profile = ScriptableObject.CreateInstance<AudioPresentationProfile>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_profile);

        [TestCase(0f, 0.85f)]
        [TestCase(12.5f, 1.05f)]
        [TestCase(25f, 1.25f)]
        [TestCase(50f, 1.25f)]
        public void EnginePitchPreservesTheOriginalResponse(float speed, float expected)
        {
            Assert.AreEqual(expected, _profile.Vehicle.CalculateEnginePitch(speed), TOLERANCE);
        }

        [TestCase(0f, 100f, 0.2f)]
        [TestCase(7.5f, 100f, 0.6f)]
        [TestCase(15f, 100f, 1f)]
        [TestCase(30f, 100f, 1f)]
        [TestCase(15f, 0f, 0.28f)]
        [TestCase(15f, 1f, 0.28f)]
        public void CollisionVolumePreservesMassFloorAndSaturation(float speed, float mass, float expected)
        {
            Assert.AreEqual(expected, _profile.Vehicle.CalculateCollisionVolume(speed, mass), TOLERANCE);
        }

        [Test]
        public void DefaultsPreserveGainsAndThresholds()
        {
            var vehicle = _profile.Vehicle;
            Assert.AreEqual(0.8f, vehicle.InitialEngineVolume);
            Assert.AreEqual(0.8f, vehicle.LocalEngineVolume);
            Assert.AreEqual(0.45f, vehicle.RemoteEngineVolume);
            Assert.AreEqual(0.5f, vehicle.EngineRunningSpeed);
            Assert.AreEqual(0.18f, vehicle.CollisionCooldownSeconds);
            Assert.AreEqual(2f, vehicle.MinimumCollisionSpeed);
            Assert.AreEqual(1.2f, vehicle.HurtCooldownSeconds);
            Assert.AreEqual(0.45f, vehicle.FallingDelaySeconds);
            Assert.AreEqual(-5f, vehicle.FallingVerticalSpeed);
            Assert.AreEqual(0.7f, _profile.Ui.ClickVolume);
            Assert.AreEqual(0.55f, _profile.Ui.HoverVolume);
            Assert.AreEqual(0.08f, _profile.Ui.HoverCooldownSeconds);
            Assert.AreEqual(1.25f, _profile.Abilities.TomatoLaunchVolume);
            Assert.AreEqual(1.2f, _profile.Abilities.TomatoImpactVolume);
            Assert.AreEqual(0.6f, _profile.Abilities.TomatoSplatVolume);
            Assert.AreEqual(1.25f, _profile.Abilities.ChickenLaunchVolume);
            Assert.AreEqual(1.2f, _profile.Abilities.ChickenLaunchPitch);
            Assert.AreEqual(0.4f, _profile.Chicken.VoiceVolume);
            Assert.AreEqual(0.5f, _profile.Chicken.StepsVolume);
            Assert.AreEqual(1.2f, _profile.Chicken.StepsDelaySeconds);
            Assert.AreEqual(0.6f, _profile.Chicken.MinimumWalkingSpeed);
            Assert.AreEqual(0.4f, _profile.Chicken.CryCooldownSeconds);
            Assert.AreEqual(15f, _profile.Zones.MarketPadding);
            Assert.AreEqual(35f, _profile.Zones.ConstructionPadding);
            Assert.AreEqual(1f, _profile.Zones.BridgePadding);
            Assert.AreEqual(0.4f, _profile.Zones.Volume);
        }

        [TestCase(0d, 0.94f)]
        [TestCase(0.5d, 1f)]
        [TestCase(1d, 1.06f)]
        public void UiPitchPreservesTheOriginalRange(double sample, float expected)
        {
            Assert.AreEqual(expected, _profile.Ui.CalculatePitch(sample), TOLERANCE);
        }

        [Test]
        public void EditedSettingsDriveResponsesWithoutChangingOtherProfiles()
        {
            var other = ScriptableObject.CreateInstance<AudioPresentationProfile>();
            try
            {
                using (var serialized = new SerializedObject(_profile))
                {
                    serialized.FindProperty("vehicle.minimumEnginePitch").floatValue = 0.5f;
                    serialized.FindProperty("vehicle.maximumEnginePitch").floatValue = 2f;
                    serialized.FindProperty("vehicle.fullPitchSpeed").floatValue = 10f;
                    serialized.FindProperty("ui.pitchVariation").floatValue = 0.2f;
                    serialized.FindProperty("abilities.tomatoLaunchVolume").floatValue = 1.5f;
                    serialized.FindProperty("zones.verticalExpansion").floatValue = 8f;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                Assert.AreEqual(1.25f, _profile.Vehicle.CalculateEnginePitch(5f), TOLERANCE);
                Assert.AreEqual(0.8f, _profile.Ui.CalculatePitch(0d), TOLERANCE);
                Assert.AreEqual(1.5f, _profile.Abilities.TomatoLaunchVolume);
                Assert.AreEqual(new Vector3(6f, 8f, 6f), _profile.Zones.ExpansionFor(3f));
                Assert.AreEqual(0.85f, other.Vehicle.CalculateEnginePitch(0f), TOLERANCE);
                Assert.AreEqual(1.25f, other.Abilities.TomatoLaunchVolume);
            }
            finally { Object.DestroyImmediate(other); }
        }

        [Test]
        public void ZoneExpansionIsTotalVerticalSizeRatherThanPaddingPerSide()
        {
            Assert.AreEqual(new Vector3(30f, 20f, 30f), _profile.Zones.ExpansionFor(_profile.Zones.MarketPadding));
        }

        [Test]
        public void ProjectProfileIsAvailableWithoutCreatingAGameplaySingleton()
        {
            var profile = Resources.Load<AudioPresentationProfile>(AudioPresentationProfile.RESOURCES_PATH);
            Assert.IsNotNull(profile);
            Assert.AreSame(profile, AudioPresentationProfile.Load());
            Assert.IsFalse(GameplayAudio.CanPlay);
        }
    }
}
