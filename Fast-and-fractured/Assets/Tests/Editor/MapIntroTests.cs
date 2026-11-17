using System.Linq;
using Assets.SimpleLocalization.Scripts;
using FastAndFractured.Multiplayer.Lobby;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace FastAndFractured.Tests
{
    public class MapIntroTests
    {
        private const string MAP_CATALOG_PATH = "Assets/Multiplayer/Lobby/Resources/MapCatalog.asset";

        private static MapIntroLibrary Library()
        {
            var library = MapIntroLibrary.Load();
            Assert.IsNotNull(library, "MapIntroLibrary missing from Resources.");
            return library;
        }

        private static MapIntro[] Intros()
        {
            var entries = new SerializedObject(Library()).FindProperty("entries");
            return Enumerable.Range(0, entries.arraySize)
                .Select(i => entries.GetArrayElementAtIndex(i).FindPropertyRelative("intro").objectReferenceValue as MapIntro)
                .ToArray();
        }

        [Test]
        public void EveryMapScene_HasAnIntro()
        {
            Assume.That(Library().PlayIntros, "Map intros are switched off (Tools > Fast And Fractured > Map Intro).");
            var catalog = AssetDatabase.LoadAssetAtPath<MapCatalog>(MAP_CATALOG_PATH);
            Assert.IsNotNull(catalog);
            foreach (var entry in catalog.Entries)
            {
                Assert.IsNotNull(MapIntroLibrary.PrefabFor(entry.SceneName), entry.SceneName);
                Assert.IsNotNull(MapIntroLibrary.PrefabFor(entry.GetSceneNameForMultiplayer()), entry.GetSceneNameForMultiplayer());
                Assert.Greater(MapIntroLibrary.DurationFor(entry.GetSceneNameForMultiplayer()), 0f);
            }
        }

        [Test]
        public void PreMatchSteps_AddTheirTimelinesToTheServerLead()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            var timelines = new[] { ScriptableObject.CreateInstance<TimelineAsset>(), ScriptableObject.CreateInstance<TimelineAsset>() };
            try
            {
                AddTimelineStep(scene, timelines[0], 2f);
                AddTimelineStep(scene, timelines[1], 3f).SetActive(false);
                Assert.AreEqual(2f, PreMatchSequence.DurationFor(scene), 0.001f, "Only active steps count");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (var timeline in timelines) Object.DestroyImmediate(timeline);
            }
        }

        private static GameObject AddTimelineStep(Scene scene, TimelineAsset timeline, float seconds)
        {
            timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
            timeline.fixedDuration = seconds;
            var go = new GameObject("Step");
            SceneManager.MoveGameObjectToScene(go, scene);
            var director = go.AddComponent<PlayableDirector>();
            director.playableAsset = timeline;
            var step = go.AddComponent<TimelinePreMatchStep>();
            var so = new SerializedObject(step);
            so.FindProperty("director").objectReferenceValue = director;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        [Test]
        public void EveryIntro_HasItsTimelineFullyBound()
        {
            LocalizationManager.Read();
            foreach (MapIntro intro in Intros())
            {
                Assert.IsNotNull(intro);
                var so = new SerializedObject(intro);
                var director = (PlayableDirector)so.FindProperty("director").objectReferenceValue;
                var overlay = (MapIntroOverlay)so.FindProperty("overlay").objectReferenceValue;
                string nameKey = so.FindProperty("mapNameKey").stringValue;
                Assert.IsNotNull(director, intro.name);
                Assert.IsNotNull(overlay, intro.name);
                Assert.IsFalse(director.playOnAwake, intro.name + " must wait for PreMatchSequence");
                Assert.IsTrue(LocalizationManager.HasKey(nameKey), intro.name + ": no localization for " + nameKey);
                Assert.Greater(so.FindProperty("playerShots").arraySize, 0, intro.name + " never shows the local player");

                var timeline = (TimelineAsset)director.playableAsset;
                Assert.IsNotNull(timeline, intro.name);
                Assert.IsTrue(timeline.GetOutputTracks().OfType<CinemachineTrack>().Any(), intro.name + " has no camera track");
                foreach (TrackAsset track in timeline.GetOutputTracks())
                {
                    if (track is MapIntroOverlayTrack)
                        Assert.AreSame(overlay, director.GetGenericBinding(track), intro.name + "/" + track.name);
                    foreach (TimelineClip clip in track.GetClips())
                    {
                        Assert.IsNotNull(clip.asset, intro.name + "/" + track.name + "/" + clip.displayName);
                        if (clip.asset is CinemachineShot camShot)
                            Assert.IsNotNull(camShot.VirtualCamera.Resolve(director), intro.name + "/" + clip.displayName);
                        if (clip.asset is MapIntroShotClip moveClip)
                            Assert.IsNotNull(moveClip.shot.Resolve(director), intro.name + "/" + clip.displayName);
                    }
                }
            }
        }
    }
}
