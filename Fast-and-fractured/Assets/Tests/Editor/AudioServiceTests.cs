using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Utilities.Audio;

namespace FastAndFractured.Tests
{
    public class AudioServiceTests
    {
        private IAudioService _previous;
        private AudioCueCatalog _previousCatalog;
        private FakeService _fake;
        private AudioCueCatalog _testCatalog;

        [SetUp]
        public void SetUp()
        {
            _previous = AudioServices.Current;
            _previousCatalog = AudioServices.Catalog;
            _testCatalog = ScriptableObject.CreateInstance<AudioCueCatalog>();
            foreach (string id in new[] { "vehicle.engine", "loop", "test.cue" }) _testCatalog.AddMissing(id, id, id);
            AudioServices.Catalog = _testCatalog;
            _fake = new FakeService();
            AudioServices.Current = _fake;
        }

        [TearDown]
        public void TearDown()
        {
            AudioServices.Current = _previous;
            AudioServices.Catalog = _previousCatalog;
            _fake.Dispose();
            UnityEngine.Object.DestroyImmediate(_testCatalog);
        }

        [Test]
        public void CoreAssemblyHasNoBackendOrGameDependency()
        {
            var assembly = typeof(IAudioService).Assembly;
            Assert.AreEqual("Utilities.Audio", assembly.GetName().Name);
            var references = assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert.IsFalse(references.Any(n => n.Contains("FMOD") || n.Contains("FishNet") || n.Contains("Assembly-CSharp")));
        }

        [Test]
        public void CueOptionsAndParametersReachTheRegisteredBackend()
        {
            var source = new GameObject("Audio source test");
            try
            {
                var options = AudioPlaybackOptions.At(new Vector3(2f, 3f, 4f), 0.4f);
                options.Follow = source;
                options.LoopPolicy = AudioLoopPolicy.RequireLoop;
                options.Parameters = new[] { new AudioParameter { Name = "CarState", Label = "Running" } };
                var voice = AudioServices.Play(new AudioCueId("vehicle.engine"), options);
                Assert.IsNotNull(voice);
                Assert.AreEqual("vehicle.engine", _fake.LastCue);
                Assert.AreEqual(options.Position, _fake.LastOptions.Position);
                Assert.AreSame(source, _fake.LastOptions.Follow);
                Assert.AreEqual(0.4f, _fake.LastOptions.Volume);
                Assert.AreEqual(AudioLoopPolicy.RequireLoop, _fake.LastOptions.LoopPolicy);
                Assert.AreEqual("Running", _fake.LastOptions.Parameters[0].Label);
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        [Test]
        public void CatalogBindingIsPassedToTheBackendWithoutRecursiveAliases()
        {
            var catalog = ScriptableObject.CreateInstance<AudioCueCatalog>();
            try
            {
                catalog.AddMissing("CommonDash", "Dash", "backend.dash");
                catalog.AddMissing("backend.dash", "Another logical cue", "should.not.resolve.twice");
                AudioServices.Catalog = catalog;
                AudioServices.Play(new AudioCueId("CommonDash"), AudioPlaybackOptions.At(Vector3.zero));
                Assert.AreEqual("backend.dash", _fake.LastCue);
                Assert.IsFalse(catalog.AddMissing("CommonDash", "Replacement", "wrong"));
                Assert.AreEqual("backend.dash", catalog.Resolve("CommonDash"));
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }

        [Test]
        public void EmptyAndDuplicateCatalogBindingsDoNotPlay()
        {
            var catalog = ScriptableObject.CreateInstance<AudioCueCatalog>();
            try
            {
                catalog.AddMissing("cue.a", "A", "");
                catalog.AddMissing("cue.b", "B", "backend.b");
                AudioServices.Catalog = catalog;
                Assert.IsNull(AudioServices.Play(new AudioCueId("cue.a"), AudioPlaybackOptions.At(Vector3.zero)));
                catalog.Entries[1].id = "cue.a";
                catalog.Invalidate();
                Assert.IsNull(AudioServices.Play(new AudioCueId("cue.a"), AudioPlaybackOptions.At(Vector3.zero)));
                Assert.IsEmpty(_fake.Voices);
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }

        [Test]
        public void ServiceReadinessPreventsPrematurePlayback()
        {
            _fake.Ready = false;
            Assert.IsNull(AudioServices.Play(new AudioCueId("test.cue"), AudioPlaybackOptions.At(Vector3.zero)));
            Assert.IsEmpty(_fake.Voices);
        }

        [Test]
        public void StopIsPerVoiceAndSafeToRepeat()
        {
            IAudioVoice first = AudioServices.Play(new AudioCueId("loop"), AudioPlaybackOptions.At(Vector3.zero));
            var second = AudioServices.Play(new AudioCueId("loop"), AudioPlaybackOptions.At(Vector3.one));
            var original = (FakeVoice)first;
            GameplayAudio.Stop(ref first);
            GameplayAudio.Stop(ref first);
            Assert.IsNull(first);
            Assert.AreEqual(1, original.Stops);
            Assert.IsTrue(second.IsAlive);
        }

        [Test]
        public void EditModePolicyDoesNotCallBackend()
        {
            Assert.IsNull(GameplayAudio.Play(GameAudioCues.CommonDash, Vector3.zero));
            Assert.IsEmpty(_fake.Voices);
        }

        [Test]
        public void GameplayReturnsOnlyThePortableVoiceContract()
        {
            Assert.AreEqual(typeof(IAudioVoice), typeof(GameplayAudio).GetMethod(nameof(GameplayAudio.Play)).ReturnType);
            Assert.AreEqual(typeof(IAudioVoice), typeof(GameplayAudio).GetMethod(nameof(GameplayAudio.StartAttached)).ReturnType);
        }

        [TestCase("Josefino", "VehicleMovementEvents/GolfCartEngine")]
        [TestCase("MariaAntonia", "VehicleMovementEvents/GolfCartEngine")]
        [TestCase("Pepe", "VehicleMovementEvents/TractorEngineSound")]
        [TestCase("Carme", "VehicleMovementEvents/QuadBikeEngine")]
        [TestCase("Unknown", null)]
        public void EngineSelectionPreservesExistingCharacterBindings(string character, string expected)
        {
            Assert.AreEqual(expected ?? "", GameplayAudio.EngineCueFor(character).Value);
        }

        private sealed class FakeService : IAudioService
        {
            public bool Ready = true;
            public bool IsReady => Ready;
            public string LastCue;
            public AudioPlaybackOptions LastOptions;
            public readonly List<FakeVoice> Voices = new List<FakeVoice>();
            public IAudioVoice Play(AudioBackendReference cue, AudioPlaybackOptions options)
            {
                LastCue = cue.Path;
                LastOptions = options;
                var voice = new FakeVoice();
                Voices.Add(voice);
                return voice;
            }
            public void SetGroupVolume(AudioGroup group, float volume) { }
            public IDisposable SuspendCue(AudioBackendReference cueId) => null;
            public void SetPaused(bool paused) { foreach (var voice in Voices) voice.SetPaused(paused); }
            public void StopAll() { foreach (var voice in Voices) voice.Stop(); }
            public void Tick() { Voices.RemoveAll(v => !v.IsAlive); }
            public void Dispose() => StopAll();
        }

        private sealed class FakeVoice : IAudioVoice
        {
            public bool IsAlive { get; private set; } = true;
            public int Stops;
            public void SetVolume(float volume) { }
            public void SetPitch(float pitch) { }
            public void SetPaused(bool paused) { }
            public bool TrySetParameter(string name, float value) => IsAlive;
            public bool TrySetParameterLabel(string name, string label) => IsAlive;
            public void Stop()
            {
                if (!IsAlive) return;
                IsAlive = false;
                Stops++;
            }
        }
    }
}
