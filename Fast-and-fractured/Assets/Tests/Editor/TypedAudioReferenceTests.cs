using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Utilities.Audio;
using Utilities.Audio.Editor;
using Utilities.Audio.Fmod;
using Utilities.Audio.Fmod.Editor;

namespace FastAndFractured.Tests
{
    public class TypedAudioReferenceTests
    {
        private AudioCueCatalog _catalog;
        private const string EventId = "{11111111-2222-3333-4444-555555555555}";
        private const string ReplacementId = "{aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee}";

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<AudioCueCatalog>();
            _catalog.SetBackendId("fmod");
            _catalog.AddMissing("logical.dash", "CommonDash", "Old/Dash");
            _catalog.Entries[0].codeName = "CommonDash";
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_catalog);

        [Test]
        public void LogicalIdsAreTypedEquatableAndHaveNoImplicitStringConversion()
        {
            Assert.IsTrue(default(AudioCueId).IsEmpty);
            Assert.AreEqual(new AudioCueId("logical.dash"), new AudioCueId("logical.dash"));
            Assert.IsFalse(typeof(AudioCueId).GetMethods().Any(m => m.Name == "op_Implicit"));
            foreach (string method in new[] { "Play", "PlayPersonal", "StartAttached" })
                Assert.AreEqual(typeof(AudioCueId), typeof(GameplayAudio).GetMethod(method).GetParameters()[0].ParameterType);
        }

        [Test]
        public void BackendIdentityIgnoresThePathHint()
        {
            var first = new AudioBackendReference(EventId, "Old/Dash");
            var moved = new AudioBackendReference(EventId, "NewFolder/NewName");
            Assert.AreEqual(first, moved);
            Assert.AreEqual(first.GetHashCode(), moved.GetHashCode());
            Assert.AreNotEqual(first, new AudioBackendReference(ReplacementId, "Old/Dash"));
        }

        [Test]
        public void RuntimeFmodResolutionUsesGuidEvenWhenThePathIsWrong()
        {
            var reference = FmodAudioService.ResolveReference(new AudioBackendReference(EventId, "This/Path/Does/Not/Exist"));
            Assert.AreEqual(FMOD.GUID.Parse(EventId), reference.Guid);
            Assert.Throws<FormatException>(() => FmodAudioService.ResolveReference(new AudioBackendReference("invalid-guid", "GameplayAudio/Dash")));
        }

        [Test]
        public void RenamingWithSameGuidPreservesLogicalAndCodeIdentity()
        {
            _catalog.Entries[0].backendStableId = EventId;
            Assert.IsTrue(FmodCatalogSynchronizer.Synchronize(_catalog, new[] { new AudioBackendReference(EventId, "Moved/Renamed") }));
            var entry = _catalog.Entries[0];
            Assert.AreEqual("logical.dash", entry.id);
            Assert.AreEqual("CommonDash", entry.codeName);
            Assert.AreEqual(EventId, entry.backendStableId);
            Assert.AreEqual("Moved/Renamed", entry.backendCueId);
            Assert.AreEqual(EventId, _catalog.ResolveBinding(new AudioCueId("logical.dash")).StableId);
        }

        [Test]
        public void DeletedAndRecreatedEventIsNotReboundByItsOldPath()
        {
            _catalog.Entries[0].backendStableId = EventId;
            Assert.IsFalse(FmodCatalogSynchronizer.Synchronize(_catalog, new[] { new AudioBackendReference(ReplacementId, "Old/Dash") }));
            Assert.AreEqual(EventId, _catalog.Entries[0].backendStableId);
        }

        [Test]
        public void FirstMigrationPreservesTheCustomizedEventChoice()
        {
            _catalog.Entries[0].backendCueId = "Custom/Selected";
            FmodCatalogSynchronizer.Synchronize(_catalog, new[] { new AudioBackendReference(EventId, "Old/Dash"), new AudioBackendReference(ReplacementId, "Custom/Selected") });
            Assert.AreEqual(ReplacementId, _catalog.Entries[0].backendStableId);
            Assert.AreEqual("Custom/Selected", _catalog.Entries[0].backendCueId);
        }

        [Test]
        public void RenameAndDisplayChangesDoNotRegenerateKeys()
        {
            string before = AudioCueCodeGenerator.Render(_catalog);
            _catalog.Entries[0].backendCueId = "Moved/Renamed";
            _catalog.Entries[0].displayName = "Another display name";
            Assert.IsFalse(AudioCueCodeGenerator.EnsureNames(_catalog));
            Assert.AreEqual(before, AudioCueCodeGenerator.Render(_catalog));
        }

        [Test]
        public void GeneratorRejectsDuplicateOrInvalidSymbols()
        {
            _catalog.AddMissing("another", "Other", "Other/Event");
            _catalog.Entries[1].codeName = "CommonDash";
            Assert.Throws<InvalidOperationException>(() => AudioCueCodeGenerator.EnsureNames(_catalog));
            _catalog.Entries[1].codeName = "class";
            Assert.Throws<InvalidOperationException>(() => AudioCueCodeGenerator.EnsureNames(_catalog));
        }

        [Test]
        public void TypedCodeUsagesRemainSearchable()
        {
            var real = AssetDatabase.LoadAssetAtPath<AudioCueCatalog>(AudioReferencesWindow.DEFAULT_CATALOG_PATH);
            var index = new AudioReferenceIndex();
            index.Begin(real); index.Cancel();
            index.ScanSingle("Assets/FastAndFractured/!Common/Managers/Scripts/VehicleAudioController.cs");
            Assert.IsTrue(index.Records.Any(r => r.IsCode && r.CueId == GameAudioCues.CommonDash.Value));
        }

        [TestCase("Assets/FastAndFractured/Characters/Pepe/Scripts/AscendingTomatoBehaviour.cs", "TomatoLaunchVolume")]
        [TestCase("Assets/Multiplayer/UniqueAbilities/Scripts/NetworkedAscendingTomato.cs", "TomatoLaunchVolume")]
        [TestCase("Assets/FastAndFractured/Characters/Pepe/Scripts/DescendingTomatoBehaviour.cs", "TomatoImpactVolume", "TomatoSplatVolume")]
        [TestCase("Assets/Multiplayer/UniqueAbilities/Scripts/NetworkedDescendingTomato.cs", "TomatoImpactVolume", "TomatoSplatVolume")]
        public void TomatoPresentationUsesSharedSettings(string path, params string[] properties)
        {
            string code = File.ReadAllText(path);
            StringAssert.Contains("AudioPresentationProfile.Load().Abilities", code, path);
            foreach (string property in properties) StringAssert.Contains("." + property, code, path);
        }

        [Test]
        public void GameplayHasNoHardcodedPresentationOverrides()
        {
            foreach (string folder in new[] { "Assets/FastAndFractured", "Assets/Multiplayer" })
                foreach (string file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
                {
                    string code = File.ReadAllText(file);
                    Assert.IsFalse(Regex.IsMatch(code, @"GameplayAudio\.(?:Play|StartAttached)\(\s*GameAudioCues\.\w+\s*,\s*[^,\r\n]+,\s*-?\d+(?:\.\d+)?f?\s*[,)]"), file);
                }
        }

        [Test]
        public void GameplayHasNoHardcodedAudioCalls()
        {
            foreach (string folder in new[] { "Assets/FastAndFractured", "Assets/Multiplayer" })
                foreach (string file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
                {
                    if (file.Contains("/Generated/") || file.Contains("\\Generated\\") || file.Contains("/Editor/") || file.Contains("\\Editor\\")) continue;
                    string code = File.ReadAllText(file);
                    Assert.IsFalse(Regex.IsMatch(code, "(?:GameplayAudio\\.(?:Play|PlayPersonal|StartAttached)|AudioServices\\.(?:Play|SuspendCue))\\s*\\(\\s*\""), file);
                }
        }
    }
}
