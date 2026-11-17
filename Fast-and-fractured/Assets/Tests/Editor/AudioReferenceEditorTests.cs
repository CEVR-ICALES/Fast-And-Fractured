using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Utilities.Audio;
using Utilities.Audio.Editor;
using Object = UnityEngine.Object;

namespace FastAndFractured.Tests
{
    public class AudioReferenceEditorTests
    {
        private string _folder;
        private Scene _original;
        private Scene _scene;
        private AudioClip _a, _b;
        private AudioCueCatalog _previousCatalog;

        [SetUp]
        public void SetUp()
        {
            _previousCatalog = AudioServices.Catalog;
            _original = SceneManager.GetActiveScene();
            _scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
            foreach (var root in _scene.GetRootGameObjects()) foreach (var light in root.GetComponentsInChildren<Light>()) light.shadows = LightShadows.None;
            SceneManager.SetActiveScene(_scene);
            _folder = "Assets/Tests/Editor/AudioRefFixture_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/Tests/Editor", Path.GetFileName(_folder));
            _a = MakeClip("a"); _b = MakeClip("b");
        }

        [TearDown]
        public void TearDown()
        {
            AudioServices.Catalog = _previousCatalog;
            if (_original.IsValid() && _original.isLoaded) SceneManager.SetActiveScene(_original);
            if (_scene.IsValid() && _scene.isLoaded)
            {
                foreach (var root in _scene.GetRootGameObjects())
                {
                    Undo.ClearUndo(root);
                    foreach (var component in root.GetComponentsInChildren<Component>(true)) if (component != null) Undo.ClearUndo(component);
                }
                EditorSceneManager.CloseScene(_scene, true);
            }
            if (!string.IsNullOrEmpty(_folder))
            {
                foreach (string guid in AssetDatabase.FindAssets("", new[] { _folder }))
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid))) if (asset != null) Undo.ClearUndo(asset);
                AssetDatabase.DeleteAsset(_folder);
            }
        }

        private AudioClip MakeClip(string name)
        {
            string path = _folder + "/" + name + ".wav";
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + 200); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(8000); writer.Write(16000); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(200); for (int i = 0; i < 100; i++) writer.Write((short)(i % 10 * 100));
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private AudioReferenceFixture Holder(string name = "Holder")
        {
            var holder = ScriptableObject.CreateInstance<AudioReferenceFixture>();
            holder.clip = _a;
            AssetDatabase.CreateAsset(holder, _folder + "/" + name + ".asset");
            return holder;
        }

        private AudioReferenceRecord Find(string path, string adapter, string property = null)
        {
            var index = new AudioReferenceIndex();
            index.ScanSingle(path);
            return index.Records.First(r => r.AdapterId == adapter && (property == null || r.Location.PropertyPath == property));
        }

        private GameObject BasePrefab(out string path)
        {
            path = _folder + "/Base.prefab";
            var go = new GameObject("Holder");
            go.AddComponent<AudioSource>().clip = _a;
            var asset = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return asset;
        }

        [Test]
        public void ClipEditChangesAndPersistsTheActualAsset()
        {
            var holder = Holder();
            string path = AssetDatabase.GetAssetPath(holder);
            var row = Find(path, "unity-clip", "clip");
            AudioReferenceWriter.Apply(row, new AudioReferenceValue { Asset = _b }, false, false);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Assert.AreSame(_b, AssetDatabase.LoadAssetAtPath<AudioReferenceFixture>(path).clip);
        }

        [Test]
        public void CatalogEditChangesTheBindingUsedByAllCalls()
        {
            var catalog = ScriptableObject.CreateInstance<AudioCueCatalog>();
            catalog.AddMissing("GameplayAudio/Dash", "CommonDash", "GameplayAudio/Dash");
            string path = _folder + "/Catalog.asset";
            AssetDatabase.CreateAsset(catalog, path);
            AudioServices.Catalog = catalog;
            var row = Find(path, "cue-id");
            Assert.IsTrue(row.IsCatalog);
            AudioReferenceWriter.Apply(row, new AudioReferenceValue { Text = "GameplayAudio/PanThrow" }, false, false, "CommonDash");
            Assert.AreEqual("GameplayAudio/PanThrow", AudioServices.Resolve(new AudioCueId("GameplayAudio/Dash")).Path);
            Assert.IsTrue(File.ReadAllText(path).Contains("GameplayAudio/PanThrow"));
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            catalog.Invalidate();
            Assert.AreEqual("GameplayAudio/Dash", catalog.Resolve("GameplayAudio/Dash"));
        }

        [Test]
        public void AudioUndoDoesNotUndoAnEarlierSceneEdit()
        {
            var unrelated = new GameObject("Unrelated edit");
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Previous unrelated scene edit");
            Undo.RecordObject(unrelated.transform, "Previous unrelated scene edit");
            unrelated.transform.position = Vector3.one;
            EditorSceneManager.MarkSceneDirty(_scene);
            Undo.FlushUndoRecordObjects();
            var holder = Holder();
            var row = Find(AssetDatabase.GetAssetPath(holder), "unity-clip", "clip");
            AudioReferenceWriter.Apply(row, new AudioReferenceValue { Asset = _b }, false, false);
            Undo.PerformUndo();
            Assert.AreSame(_a, holder.clip);
            Assert.AreEqual(Vector3.one, unrelated.transform.position);
            Assert.IsTrue(_scene.isDirty);
        }

        [Test]
        public void PrefabEditPersistsInThePrefabRatherThanAnInstance()
        {
            BasePrefab(out string path);
            var row = Find(path, "unity-clip");
            AudioReferenceWriter.Apply(row, new AudioReferenceValue { Asset = _b }, false, false);
            Assert.AreSame(_b, AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<AudioSource>().clip);
        }

        [Test]
        public void VariantLocalOverrideDoesNotChangeTheBase()
        {
            var basePrefab = BasePrefab(out string basePath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            string path = _folder + "/Variant.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            var row = Find(path, "unity-clip");
            Assert.IsNotNull(row.InheritedOrigin);
            AudioReferenceWriter.Apply(row, new AudioReferenceValue { Asset = _b }, false, false);
            Assert.AreSame(_a, AssetDatabase.LoadAssetAtPath<GameObject>(basePath).GetComponent<AudioSource>().clip);
            Assert.AreSame(_b, AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<AudioSource>().clip);
        }

        [Test]
        public void ExplicitSharedOriginEditChangesTheBase()
        {
            var basePrefab = BasePrefab(out string basePath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            string path = _folder + "/Variant.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            var row = Find(path, "unity-clip");
            Assert.IsNotNull(row.InheritedOrigin);
            AudioReferenceWriter.Apply(row, new AudioReferenceValue { Asset = _b }, true, false);
            Assert.AreSame(_b, AssetDatabase.LoadAssetAtPath<GameObject>(basePath).GetComponent<AudioSource>().clip);
        }

        [Test]
        public void NestedPrefabCanReceiveItsOwnOverride()
        {
            var basePrefab = BasePrefab(out string basePath);
            var parent = new GameObject("Parent");
            var child = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            child.transform.SetParent(parent.transform);
            string path = _folder + "/Nested.prefab";
            PrefabUtility.SaveAsPrefabAsset(parent, path);
            Object.DestroyImmediate(parent);
            var row = Find(path, "unity-clip");
            Assert.IsNotNull(row.InheritedOrigin);
            AudioReferenceWriter.Apply(row, new AudioReferenceValue { Asset = _b }, false, false);
            Assert.AreSame(_b, AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<AudioSource>().clip);
            Assert.AreSame(_a, AssetDatabase.LoadAssetAtPath<GameObject>(basePath).GetComponent<AudioSource>().clip);
        }

        [Test]
        public void StaleSearchResultDoesNotOverwriteAnotherEdit()
        {
            var holder = Holder();
            string path = AssetDatabase.GetAssetPath(holder);
            var row = Find(path, "unity-clip", "clip");
            holder.clip = _b; EditorUtility.SetDirty(holder); AssetDatabase.SaveAssetIfDirty(holder);
            Assert.Throws<InvalidOperationException>(() => AudioReferenceWriter.Apply(row, new AudioReferenceValue { Asset = _a }, false, false));
            Assert.AreSame(_b, holder.clip);
        }

        [Test]
        public void FmodEditWritesGuidAndPathTogether()
        {
            var holder = Holder();
            holder.nativeSound = FMODUnity.EventReference.Find("event:/GameplayAudio/Dash");
            EditorUtility.SetDirty(holder); AssetDatabase.SaveAssetIfDirty(holder);
            var row = Find(AssetDatabase.GetAssetPath(holder), "fmod-event", "nativeSound");
            AudioReferenceWriter.Apply(row, new AudioReferenceValue { Text = "event:/GameplayAudio/PanThrow" }, false, false);
            var expected = FMODUnity.EventManager.EventFromPath("event:/GameplayAudio/PanThrow");
            Assert.AreEqual(expected.Guid, holder.nativeSound.Guid);
            Assert.AreEqual(expected.Path, holder.nativeSound.Path);
        }

        [Test]
        public void TimelineEditChangesThePlayableSubasset()
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            string path = _folder + "/Timeline.playable";
            AssetDatabase.CreateAsset(timeline, path);
            var track = timeline.CreateTrack<FMODUnity.FMODEventTrack>(null, "Audio");
            var clip = track.CreateClip<FMODUnity.FMODEventPlayable>();
            var playable = (FMODUnity.FMODEventPlayable)clip.asset;
            playable.EventReference = FMODUnity.EventReference.Find("event:/GameplayAudio/Dash");
            EditorUtility.SetDirty(playable); EditorUtility.SetDirty(timeline); AssetDatabase.SaveAssetIfDirty(timeline);
            var row = Find(path, "fmod-event", "EventReference");
            AudioReferenceWriter.Apply(row, new AudioReferenceValue { Text = "event:/GameplayAudio/PanThrow" }, false, false);
            Assert.AreEqual("event:/GameplayAudio/PanThrow", playable.EventReference.Path);
        }

        [Test]
        public void OpenSceneEditsStayDirtyWithoutSavingUnrelatedChanges()
        {
            var go = new GameObject("Audio holder"); var source = go.AddComponent<AudioSource>(); source.clip = _a;
            string path = _folder + "/Open.unity";
            EditorSceneManager.SaveScene(_scene, path);
            byte[] before = File.ReadAllBytes(path);
            go.transform.position = Vector3.one; EditorSceneManager.MarkSceneDirty(_scene);
            var row = Find(path, "unity-clip");
            AudioReferenceWriter.Apply(row, new AudioReferenceValue { Asset = _b }, false, false);
            Assert.AreSame(_b, source.clip);
            Assert.IsTrue(_scene.isDirty);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        }

        [Test]
        public void ClosedSceneSaveRequiresConsentAndPersistsOnlyItsReference()
        {
            var go = new GameObject("Audio holder"); go.AddComponent<AudioSource>().clip = _a;
            string path = _folder + "/Closed.unity";
            EditorSceneManager.SaveScene(_scene, path);
            SceneManager.SetActiveScene(_original);
            EditorSceneManager.CloseScene(_scene, true);
            var row = Find(path, "unity-clip");
            Assert.Throws<InvalidOperationException>(() => AudioReferenceWriter.Apply(row, new AudioReferenceValue { Asset = _b }, false, false));
            AudioReferenceWriter.Apply(row, new AudioReferenceValue { Asset = _b }, false, true);
            Scene reopened = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try { Assert.AreEqual(_folder + "/b.wav", AssetDatabase.GetAssetPath(reopened.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AudioSource>()).Single().clip)); }
            finally { EditorSceneManager.CloseScene(reopened, true); }
        }

        [Test]
        public void ClosingPreviewDoesNotRemovePrefabPlacementTracking()
        {
            var tracker = ScriptableObject.CreateInstance<ProfilePlacedObjectsTrackerSO>();
            tracker.placedPrefabs.Add(new PlacementData { UniqueID = "placed-object" });
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = new GameObject("Preview marker");
                SceneManager.MoveGameObjectToScene(go, preview);
                var marker = go.AddComponent<PrefabPlacerObjectMarker>();
                marker.uniqueID = "placed-object";
                marker.trackingSO = tracker;
                EditorSceneManager.ClosePreviewScene(preview);
                Assert.IsTrue(tracker.ContainsByUniqueID("placed-object"));
            }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                Object.DestroyImmediate(tracker);
            }
        }

        [Test]
        public void CancelledIndexDoesNotLoadScenes()
        {
            int before = SceneManager.sceneCount;
            var index = new AudioReferenceIndex();
            index.Begin(null);
            index.Cancel(); index.Step();
            Assert.IsFalse(index.Running);
            Assert.AreEqual(before, SceneManager.sceneCount);
        }
    }
}
