using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Utilities.Audio;
using Utilities.Audio.Editor;

namespace FastAndFractured.Tests
{
    public class AudioPreviewSessionTests
    {
        private Func<string, IAudioEditorPreview> _previous;
        private Func<AudioBackendReference, IAudioEditorPreview> _previousResource;
        private Func<string, AudioBackendReference> _previousResolver;
        private string _lastCue;
        private FakePreview _last;

        [SetUp]
        public void SetUp()
        {
            _previous = AudioReferenceAdapters.BeginCuePreview;
            _previousResource = AudioReferenceAdapters.BeginResourcePreview;
            _previousResolver = AudioReferenceAdapters.ResolveBackendReference;
            AudioReferenceAdapters.BeginResourcePreview = null;
            AudioReferenceAdapters.ResolveBackendReference = null;
            AudioReferenceAdapters.BeginCuePreview = id => { _lastCue = id; _last = new FakePreview(); return _last; };
        }

        [TearDown]
        public void TearDown()
        {
            AudioPreviewSession.StopAll();
            AudioReferenceAdapters.BeginCuePreview = _previous;
            AudioReferenceAdapters.BeginResourcePreview = _previousResource;
            AudioReferenceAdapters.ResolveBackendReference = _previousResolver;
        }

        [Test]
        public void AssignedCueResolvesCatalogWithoutEditingIt()
        {
            var catalog = ScriptableObject.CreateInstance<AudioCueCatalog>();
            catalog.AddMissing("CommonDash", "Dash", "backend.assigned");
            try
            {
                using (var session = new AudioPreviewSession())
                {
                    Assert.IsTrue(session.PlayCue("CommonDash", catalog));
                    Assert.AreEqual("backend.assigned", _lastCue);
                    Assert.AreEqual("backend.assigned", catalog.Resolve("CommonDash"));
                    Assert.IsTrue(session.IsPlaying);
                }
                Assert.AreEqual(1, _last.Disposals);
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }

        [Test]
        public void CandidateBindingIsNotResolvedOrAssignedToTheCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<AudioCueCatalog>();
            catalog.AddMissing("CommonDash", "Dash", "backend.assigned");
            catalog.AddMissing("backend.candidate", "An unrelated logical ID", "do.not.resolve");
            try
            {
                using (var session = new AudioPreviewSession())
                {
                    session.PlayCue("backend.candidate", catalog, true);
                    Assert.AreEqual("backend.candidate", _lastCue);
                    Assert.AreEqual("backend.assigned", catalog.Resolve("CommonDash"));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }

        [Test]
        public void NewPreviewStopsThePreviousWindowPreview()
        {
            using (var first = new AudioPreviewSession())
            using (var second = new AudioPreviewSession())
            {
                first.PlayCue("one", null, true);
                var old = _last;
                second.PlayCue("two", null, true);
                Assert.AreEqual(1, old.Disposals);
                Assert.IsFalse(first.IsPlaying);
                Assert.IsTrue(second.IsPlaying);
                second.Stop(); second.Stop();
                Assert.AreEqual(1, _last.Disposals);
            }
        }

        [Test]
        public void FinishedPreviewIsReleasedByEditorUpdate()
        {
            using (var session = new AudioPreviewSession())
            {
                session.PlayCue("one", null, true);
                _last.Playing = false;
                typeof(AudioPreviewSession).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                Assert.AreEqual(1, _last.Disposals);
                Assert.IsFalse(session.IsPlaying);
                Assert.IsNull(session.Label);
            }
        }

        [Test]
        public void UnassignedOrUnavailablePreviewHasAnEnglishError()
        {
            using (var session = new AudioPreviewSession())
            {
                Assert.IsFalse(session.PlayCue("", null, true));
                Assert.AreEqual("No audio is assigned to this reference.", session.Error);
                AudioReferenceAdapters.BeginCuePreview = null;
                Assert.IsFalse(session.PlayCue("missing", null, true));
                StringAssert.Contains("No editor preview provider", session.Error);
            }
        }

        [Test]
        public void DestroyingPickerStopsPreviewWithoutSelectingAnything()
        {
            var picker = ScriptableObject.CreateInstance<AudioCuePicker>();
            int selections = 0;
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(AudioCuePicker).GetField("_select", flags).SetValue(picker, new Action<string>(_ => selections++));
            var session = (AudioPreviewSession)typeof(AudioCuePicker).GetField("_preview", flags).GetValue(picker);
            session.PlayCue("candidate", null, true);
            var playback = _last;
            UnityEngine.Object.DestroyImmediate(picker);
            Assert.AreEqual(1, playback.Disposals);
            Assert.AreEqual(0, selections);
        }

        private sealed class FakePreview : IAudioEditorPreview
        {
            public bool Playing = true;
            public int Disposals;
            public bool IsPlaying => Playing;
            public void Dispose() { Disposals++; Playing = false; }
        }
    }
}
