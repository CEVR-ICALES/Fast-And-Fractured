using Enums;
using FMODUnity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Tests
{
    public class GameplayAudioTests
    {
        [TestCase(Pooltype.PUSH_BULLET_GOLF, "GameplayAudio/BounceMetal")]
        [TestCase(Pooltype.PUSH_BULLET_SARTENAZO, "GameplayAudio/BounceMetal")]
        [TestCase(Pooltype.PUSH_BULLET_CHICKEN, "GameplayAudio/BounceSoft")]
        [TestCase(Pooltype.PUSH_BULLET_MELON, "GameplayAudio/BounceSoft")]
        public void BounceSoundMatchesMaterial(Pooltype type, string expected)
        {
            Assert.AreEqual(expected, GameplayAudio.BounceCue(type).Value);
        }

        [Test]
        public void NewCuesHaveContentAndBanks()
        {
            string[] cues = { "Dash", "Falling", "ShieldActivate", "ShieldImpact", "ShieldEnd", "PanThrow", "BounceMetal", "BounceSoft", "Trampoline", "Overheat", "CoolingComplete", "Heal", "BuffPickup", "Kill", "WoodCreak", "TomatoSplat", "FryingOverheat", "JosefinoHurt", "StaticCollision", "CarCollision", "CharacterBulletImpact", "WorldBulletImpact", "PlatformCrack", "PlatformSplinter", "OverheatFire", "FallingWind", "SandstormWarning", "SandstormLoop", "ChickenCry" };
            foreach (string cue in cues)
            {
                var audio = EventManager.EventFromPath("event:/GameplayAudio/" + cue);
                Assert.IsNotNull(audio, cue);
                Assert.IsFalse(audio.Guid.IsNull, cue);
                Assert.Greater(audio.Length, 0, cue);
                Assert.IsNotEmpty(audio.Banks, cue);
            }
        }

        [Test]
        public void WorldCuesAreSpatialAndPersonalCuesAreNot()
        {
            foreach (string cue in new[] { "Dash", "ShieldImpact", "BounceMetal", "PlatformSplinter", "Trampoline", "WorldBulletImpact" })
                Assert.IsTrue(EventManager.EventFromPath("event:/GameplayAudio/" + cue).Is3D, cue);
            foreach (string cue in new[] { "Overheat", "CoolingComplete", "Heal", "BuffPickup", "Kill" })
                Assert.IsFalse(EventManager.EventFromPath("event:/GameplayAudio/" + cue).Is3D, cue);
        }

        [Test]
        public void SustainedAbilityAndFryingAreLoops()
        {
            Assert.IsFalse(EventManager.EventFromPath("event:/SpecialAbilityEvents/SuperSaiyanMariaAntonia").IsOneShot);
            Assert.IsFalse(EventManager.EventFromPath("event:/GameplayAudio/FryingOverheat").IsOneShot);
            Assert.IsTrue(EventManager.EventFromPath("event:/GameplayAudio/ShieldActivate").IsOneShot);
        }

        [Test]
        public void VehicleAudioIsInheritedFromBaseCar()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FastAndFractured/Characters/!Common/BaseCar.prefab");
            Assert.IsNotNull(prefab.GetComponent<VehicleAudioController>());
        }

        [Test]
        public void StormVolumeHandlesRotationAndDisabledColliders()
        {
            var go = new GameObject("Storm audio test");
            try
            {
                var box = go.AddComponent<BoxCollider>();
                go.transform.position = new Vector3(100f, 10f, 80f);
                go.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
                box.size = new Vector3(10f, 20f, 30f);
                Assert.IsTrue(SandstormAudioController.Contains(box, go.transform.TransformPoint(Vector3.one)));
                Assert.IsFalse(SandstormAudioController.Contains(box, go.transform.TransformPoint(new Vector3(6f, 0f, 0f))));
                box.enabled = false;
                Assert.IsFalse(SandstormAudioController.Contains(box, go.transform.position));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void PepeDefeatHasVoiceWithoutExtendingTheAnimation()
        {
            var timeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Timeline.TimelineAsset>("Assets/FastAndFractured/Characters/Pepe/Animations/DefeatTimeline/NewPepeDefeat.playable");
            bool voice = false;
            foreach (var track in timeline.GetOutputTracks())
                foreach (var clip in track.GetClips())
                    if (clip.asset is FMODEventPlayable playable && !playable.EventReference.IsNull) voice = true;
            Assert.IsTrue(voice);
            Assert.AreEqual(18.23333333333, timeline.duration, 0.001);
        }

        [Test]
        public void EveryDefeatUsesItsOriginalMusicWithoutExtendingTheTimeline()
        {
            var paths = new[]
            {
                "Assets/FastAndFractured/Characters/Carme/Animations/DefeatTimeline/CarmeDefeatTimeline.playable",
                "Assets/FastAndFractured/Characters/Josefino/Animations/JosefinoDefeat/JosefinoDefeatTimeline.playable",
                "Assets/FastAndFractured/Characters/MariaAntonia/Animations/DefeatTImeline/MariaAntoniaDefeatTimeline.playable",
                "Assets/FastAndFractured/Characters/Pepe/Animations/DefeatTimeline/NewPepeDefeat.playable"
            };
            var characters = new[] { "Carme", "Josefino", "MariaAntonia", "Pepe" };
            var durations = new[] { 10.36666666666, 8.29166666666, 11.16666666666, 18.23333333333 };
            for (int i = 0; i < paths.Length; i++)
            {
                var timeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Timeline.TimelineAsset>(paths[i]);
                int count = 0;
                foreach (var track in timeline.GetOutputTracks())
                    if (track.name == "Original MUSIC")
                        foreach (var clip in track.GetClips())
                        {
                            var playable = clip.asset as FMODEventPlayable;
                            Assert.IsNotNull(playable);
                            Assert.AreEqual("event:/ResultThemes/" + characters[i] + "Defeat", playable.EventReference.Path);
                            Assert.LessOrEqual(clip.end, durations[i] + 0.001);
                            count++;
                        }
                Assert.AreEqual(1, count, characters[i]);
                Assert.AreEqual(durations[i], timeline.duration, 0.001);
            }
        }

        [Test]
        public void UiPitchVariationIsBoundedAndIndependentFromGameplayRandom()
        {
            var method = typeof(UiAudioFeedback).GetMethod("NextPitch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var before = UnityEngine.Random.state;
            var values = new System.Collections.Generic.HashSet<float>();
            for (int i = 0; i < 256; i++)
            {
                float pitch = (float)method.Invoke(null, null);
                Assert.That(pitch, Is.InRange(0.94f, 1.06f));
                values.Add(pitch);
            }
            Assert.Greater(values.Count, 32);
            Assert.AreEqual(before, UnityEngine.Random.state);
        }

        [Test]
        public void EditModeCallsDoNotCreateAudioSingletons()
        {
            Assert.IsFalse(GameplayAudio.CanPlay);
            Assert.IsFalse(GameplayAudio.IsReady);
            var voice = GameplayAudio.Play(GameAudioCues.CommonDash, Vector3.zero);
            Assert.IsNull(voice);
        }
    }
}
